using BaseballManager.Contracts.ImportDtos;

namespace BaseballManager.Game.Data;

public sealed class FranchiseSession
{
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseStateStore _stateStore;
    private readonly FranchiseSaveState _saveState;

    public FranchiseSession(ImportedLeagueData leagueData, FranchiseStateStore stateStore)
    {
        _leagueData = leagueData;
        _stateStore = stateStore;
        _saveState = _stateStore.Load();

        if (!string.IsNullOrWhiteSpace(_saveState.SelectedTeamName))
        {
            SelectedTeam = _leagueData.Teams.FirstOrDefault(team =>
                string.Equals(team.Name, _saveState.SelectedTeamName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public TeamImportDto? SelectedTeam { get; private set; }

    public string SelectedTeamName => SelectedTeam?.Name ?? "No Team Selected";

    public void SelectTeam(TeamImportDto team)
    {
        SelectedTeam = team;
        _saveState.SelectedTeamName = team.Name;
        _stateStore.Save(_saveState);
    }

    public IReadOnlyList<FranchiseRosterEntry> GetSelectedTeamRoster()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamName = SelectedTeam.Name;
        var playersById = _leagueData.Players.ToDictionary(player => player.PlayerId, player => player);
        var lineupSlots = BuildLineupSlots(teamName);
        var rotationSlots = BuildRotationSlots(teamName);
        var lineupMap = BuildSlotMap(lineupSlots);
        var rotationMap = BuildSlotMap(rotationSlots);

        return _leagueData.Rosters
            .Where(roster => string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase))
            .Select(roster =>
            {
                playersById.TryGetValue(roster.PlayerId, out var player);
                return new FranchiseRosterEntry(
                    roster.PlayerId,
                    roster.PlayerName,
                    roster.PrimaryPosition,
                    roster.SecondaryPosition,
                    player?.Age ?? 0,
                    lineupMap.TryGetValue(roster.PlayerId, out var lineupSlot) ? lineupSlot : null,
                    rotationMap.TryGetValue(roster.PlayerId, out var rotationSlot) ? rotationSlot : null);
            })
            .OrderBy(entry => entry.PlayerName)
            .ToList();
    }

    public IReadOnlyList<FranchiseRosterEntry> GetLineupPlayers()
    {
        return GetSelectedTeamRoster()
            .Where(entry => entry.LineupSlot.HasValue)
            .OrderBy(entry => entry.LineupSlot)
            .ThenBy(entry => entry.PlayerName)
            .ToList();
    }

    public IReadOnlyList<FranchiseRosterEntry> GetBenchPlayers()
    {
        return GetSelectedTeamRoster()
            .Where(entry => !entry.LineupSlot.HasValue && entry.PrimaryPosition is not "SP" and not "RP")
            .OrderBy(entry => entry.PrimaryPosition)
            .ThenBy(entry => entry.PlayerName)
            .ToList();
    }

    public IReadOnlyList<FranchiseRosterEntry> GetRotationPlayers()
    {
        return GetSelectedTeamRoster()
            .Where(entry => entry.RotationSlot.HasValue)
            .OrderBy(entry => entry.RotationSlot)
            .ThenBy(entry => entry.PlayerName)
            .ToList();
    }

    public IReadOnlyList<FranchiseRosterEntry> GetBullpenPlayers()
    {
        return GetSelectedTeamRoster()
            .Where(entry => !entry.RotationSlot.HasValue && entry.PrimaryPosition is "SP" or "RP")
            .OrderBy(entry => entry.PrimaryPosition)
            .ThenBy(entry => entry.PlayerName)
            .ToList();
    }

    public void AssignLineupSlot(Guid playerId, int slotNumber)
    {
        if (SelectedTeam == null || slotNumber < 1 || slotNumber > 9)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        InitializeLineupSlots(teamState, SelectedTeam.Name);
        ClearPlayerFromSlots(teamState.LineupSlots, playerId);
        teamState.LineupSlots[slotNumber - 1] = playerId;
        Save();
    }

    public void ClearLineupSlot(int slotNumber)
    {
        if (SelectedTeam == null || slotNumber < 1 || slotNumber > 9)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        InitializeLineupSlots(teamState, SelectedTeam.Name);
        teamState.LineupSlots[slotNumber - 1] = null;
        Save();
    }

    public void AssignRotationSlot(Guid playerId, int slotNumber)
    {
        if (SelectedTeam == null || slotNumber < 1 || slotNumber > 5)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        InitializeRotationSlots(teamState, SelectedTeam.Name);
        ClearPlayerFromSlots(teamState.RotationSlots, playerId);
        teamState.RotationSlots[slotNumber - 1] = playerId;
        Save();
    }

    public void ClearRotationSlot(int slotNumber)
    {
        if (SelectedTeam == null || slotNumber < 1 || slotNumber > 5)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        InitializeRotationSlots(teamState, SelectedTeam.Name);
        teamState.RotationSlots[slotNumber - 1] = null;
        Save();
    }

    private TeamFranchiseState GetOrCreateTeamState(string teamName)
    {
        if (!_saveState.Teams.TryGetValue(teamName, out var teamState))
        {
            teamState = new TeamFranchiseState();
            _saveState.Teams[teamName] = teamState;
        }

        return teamState;
    }

    private List<Guid?> BuildLineupSlots(string teamName)
    {
        var teamState = GetOrCreateTeamState(teamName);
        InitializeLineupSlots(teamState, teamName);
        return teamState.LineupSlots;
    }

    private List<Guid?> BuildRotationSlots(string teamName)
    {
        var teamState = GetOrCreateTeamState(teamName);
        InitializeRotationSlots(teamState, teamName);
        return teamState.RotationSlots;
    }

    private void InitializeLineupSlots(TeamFranchiseState teamState, string teamName)
    {
        if (teamState.LineupSlots.Count == 9)
        {
            return;
        }

        teamState.LineupSlots = Enumerable.Repeat<Guid?>(null, 9).ToList();
        foreach (var roster in _leagueData.Rosters.Where(roster =>
                     string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase) &&
                     roster.LineupSlot is >= 1 and <= 9))
        {
            teamState.LineupSlots[roster.LineupSlot!.Value - 1] = roster.PlayerId;
        }
    }

    private void InitializeRotationSlots(TeamFranchiseState teamState, string teamName)
    {
        if (teamState.RotationSlots.Count == 5)
        {
            return;
        }

        teamState.RotationSlots = Enumerable.Repeat<Guid?>(null, 5).ToList();
        foreach (var roster in _leagueData.Rosters.Where(roster =>
                     string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase) &&
                     roster.RotationSlot is >= 1 and <= 5))
        {
            teamState.RotationSlots[roster.RotationSlot!.Value - 1] = roster.PlayerId;
        }
    }

    private static void ClearPlayerFromSlots(List<Guid?> slots, Guid playerId)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] == playerId)
            {
                slots[i] = null;
            }
        }
    }

    private static Dictionary<Guid, int> BuildSlotMap(List<Guid?> slots)
    {
        var map = new Dictionary<Guid, int>();
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].HasValue)
            {
                map[slots[i]!.Value] = i + 1;
            }
        }

        return map;
    }

    private void Save()
    {
        _stateStore.Save(_saveState);
    }
}
