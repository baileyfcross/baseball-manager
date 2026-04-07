using BaseballManager.Contracts.ImportDtos;
using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Results;

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
        _saveState.PlayerRatings ??= new Dictionary<Guid, PlayerHiddenRatingsState>();
        _saveState.PlayerSeasonStats ??= new Dictionary<Guid, PlayerSeasonStatsState>();

        if (!string.IsNullOrWhiteSpace(_saveState.SelectedTeamName))
        {
            SelectedTeam = _leagueData.Teams.FirstOrDefault(team =>
                string.Equals(team.Name, _saveState.SelectedTeamName, StringComparison.OrdinalIgnoreCase));
        }

        PendingLiveMatchMode = SelectedTeam != null ? LiveMatchMode.Franchise : LiveMatchMode.QuickMatch;

        if (SelectedTeam != null)
        {
            MigrateLegacyFranchiseMatchIfNeeded(SelectedTeam.Name);
        }

        if (EnsurePlayerRatingsGenerated() || EnsurePlayerSeasonStatsGenerated())
        {
            Save();
        }
    }

    public TeamImportDto? SelectedTeam { get; private set; }

    public string SelectedTeamName => SelectedTeam?.Name ?? "No Team Selected";

    public LiveMatchMode PendingLiveMatchMode { get; private set; }

    public bool HasFranchiseSaveData => SelectedTeam != null;

    public bool HasQuickMatchSaveData => _saveState.QuickMatchLiveMatch != null;

    public bool HasAnySaveData =>
        !string.IsNullOrWhiteSpace(_saveState.SelectedTeamName) ||
        _saveState.Teams.Count > 0 ||
        _saveState.CurrentLiveMatch != null ||
        _saveState.QuickMatchLiveMatch != null;

    public DisplaySettingsState GetDisplaySettings()
    {
        _saveState.DisplaySettings ??= new DisplaySettingsState();
        return _saveState.DisplaySettings;
    }

    public void UpdateDisplaySettings(int screenWidth, int screenHeight, int refreshRate, DisplayWindowMode windowMode)
    {
        var displaySettings = GetDisplaySettings();
        displaySettings.ScreenWidth = Math.Max(800, screenWidth);
        displaySettings.ScreenHeight = Math.Max(600, screenHeight);
        displaySettings.RefreshRate = Math.Clamp(refreshRate, 30, 240);
        displaySettings.WindowMode = windowMode;
        Save();
    }

    public void SelectTeam(TeamImportDto team)
    {
        SelectedTeam = team;
        PendingLiveMatchMode = LiveMatchMode.Franchise;
        _saveState.SelectedTeamName = team.Name;
        MigrateLegacyFranchiseMatchIfNeeded(team.Name);
        _stateStore.Save(_saveState);
    }

    public PlayerHiddenRatingsState GetPlayerRatings(Guid playerId, string fullName, string primaryPosition, string secondaryPosition, int age)
    {
        if (_saveState.PlayerRatings.TryGetValue(playerId, out var ratings))
        {
            ratings.RecalculateDerivedRatings();
            return ratings;
        }

        var generated = PlayerRatingsGenerator.Generate(playerId, fullName, primaryPosition, secondaryPosition, age);
        _saveState.PlayerRatings[playerId] = generated;
        Save();
        return generated;
    }

    public PlayerSeasonStatsState GetPlayerSeasonStats(Guid playerId)
    {
        if (_saveState.PlayerSeasonStats.TryGetValue(playerId, out var stats))
        {
            return stats;
        }

        stats = new PlayerSeasonStatsState();
        _saveState.PlayerSeasonStats[playerId] = stats;
        return stats;
    }

    public void ApplyPerformanceDevelopment(MatchPlayerSnapshot batter, MatchPlayerSnapshot pitcher, MatchTeamState defensiveTeam, ResultEvent result)
    {
        if (!result.EndsPlateAppearance)
        {
            return;
        }

        ApplySeasonStats(batter, pitcher, result);

        switch (result.Code)
        {
            case "Walk":
                AdjustPlayerRatings(batter.Id, ratings =>
                {
                    ratings.DisciplineRating += 1;
                    if (result.RunsScored > 0)
                    {
                        ratings.ContactRating += 1;
                    }
                });
                break;

            case "Single":
                AdjustPlayerRatings(batter.Id, ratings =>
                {
                    ratings.ContactRating += 1;
                    if (result.RunsScored > 0)
                    {
                        ratings.SpeedRating += 1;
                    }
                });
                break;

            case "Double":
                AdjustPlayerRatings(batter.Id, ratings =>
                {
                    ratings.ContactRating += 1;
                    ratings.PowerRating += 1;
                });
                break;

            case "Triple":
                AdjustPlayerRatings(batter.Id, ratings =>
                {
                    ratings.ContactRating += 1;
                    ratings.SpeedRating += 2;
                });
                break;

            case "HomeRun":
                AdjustPlayerRatings(batter.Id, ratings =>
                {
                    ratings.PowerRating += 2;
                    ratings.ContactRating += 1;
                });
                break;
        }

        if (result.OutsRecorded > 0)
        {
            AdjustPlayerRatings(pitcher.Id, ratings =>
            {
                ratings.PitchingRating += result.Code == "Strikeout" ? 2 : 1;
                ratings.DurabilityRating += 1;
            });

            var fielder = defensiveTeam.FindFielder(result.Fielder);
            if (fielder != null && fielder.Id != pitcher.Id)
            {
                AdjustPlayerRatings(fielder.Id, ratings =>
                {
                    ratings.FieldingRating += 1;
                    if (result.Code is "Groundout" or "Flyout")
                    {
                        ratings.ArmRating += 1;
                    }
                });
            }
        }
    }

    public void RecordCompletedGame(MatchState finalState)
    {
        var awayWon = finalState.AwayTeam.Runs > finalState.HomeTeam.Runs;
        ApplyCompletedGame(finalState.AwayTeam, awayWon);
        ApplyCompletedGame(finalState.HomeTeam, !awayWon);
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
            .Where(entry => !entry.RotationSlot.HasValue && entry.PrimaryPosition is ("SP" or "RP"))
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
        AssignPlayerToSlotWithSwap(teamState.LineupSlots, playerId, slotNumber - 1);
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
        AssignPlayerToSlotWithSwap(teamState.RotationSlots, playerId, slotNumber - 1);
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

    public void PrepareQuickMatch()
    {
        PendingLiveMatchMode = LiveMatchMode.QuickMatch;
    }

    public void PrepareFranchiseMatch()
    {
        PendingLiveMatchMode = LiveMatchMode.Franchise;
    }

    public LiveMatchSaveState? GetLiveMatchState()
    {
        return GetLiveMatchState(PendingLiveMatchMode);
    }

    public LiveMatchSaveState? GetLiveMatchState(LiveMatchMode mode)
    {
        if (mode == LiveMatchMode.QuickMatch)
        {
            return _saveState.QuickMatchLiveMatch;
        }

        if (SelectedTeam == null)
        {
            return _saveState.CurrentLiveMatch;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        return teamState.CurrentLiveMatch ?? _saveState.CurrentLiveMatch;
    }

    public void SaveLiveMatchState(LiveMatchSaveState liveMatchState)
    {
        SaveLiveMatchState(PendingLiveMatchMode, liveMatchState);
    }

    public void SaveLiveMatchState(LiveMatchMode mode, LiveMatchSaveState liveMatchState)
    {
        var savedState = liveMatchState.IsGameOver ? null : liveMatchState;

        if (mode == LiveMatchMode.QuickMatch)
        {
            _saveState.QuickMatchLiveMatch = savedState;
        }
        else if (SelectedTeam == null)
        {
            _saveState.CurrentLiveMatch = savedState;
        }
        else
        {
            var teamState = GetOrCreateTeamState(SelectedTeam.Name);
            teamState.CurrentLiveMatch = savedState;
            _saveState.CurrentLiveMatch = null;
        }

        Save();
    }

    public void ClearLiveMatchState()
    {
        ClearLiveMatchState(PendingLiveMatchMode);
    }

    public void ClearLiveMatchState(LiveMatchMode mode)
    {
        var hasChanges = false;

        if (mode == LiveMatchMode.QuickMatch)
        {
            hasChanges = _saveState.QuickMatchLiveMatch != null;
            _saveState.QuickMatchLiveMatch = null;
        }
        else if (SelectedTeam == null)
        {
            hasChanges = _saveState.CurrentLiveMatch != null;
            _saveState.CurrentLiveMatch = null;
        }
        else
        {
            var teamState = GetOrCreateTeamState(SelectedTeam.Name);
            hasChanges = teamState.CurrentLiveMatch != null || _saveState.CurrentLiveMatch != null;
            teamState.CurrentLiveMatch = null;
            _saveState.CurrentLiveMatch = null;
        }

        if (hasChanges)
        {
            Save();
        }
    }

    public bool DeleteQuickMatchSave()
    {
        var hadQuickMatchSave = _saveState.QuickMatchLiveMatch != null;
        _saveState.QuickMatchLiveMatch = null;

        if (hadQuickMatchSave)
        {
            Save();
        }

        return hadQuickMatchSave;
    }

    public bool DeleteCurrentTeamSave()
    {
        if (SelectedTeam == null)
        {
            return false;
        }

        var teamName = SelectedTeam.Name;
        var removedTeamState = _saveState.Teams.Remove(teamName);
        var removedSelection = string.Equals(_saveState.SelectedTeamName, teamName, StringComparison.OrdinalIgnoreCase);

        if (removedSelection)
        {
            _saveState.SelectedTeamName = null;
        }

        SelectedTeam = null;
        PendingLiveMatchMode = LiveMatchMode.QuickMatch;
        _saveState.CurrentLiveMatch = null;

        if (removedTeamState || removedSelection)
        {
            Save();
            return true;
        }

        return false;
    }

    public bool DeleteAllSaveData()
    {
        var hadSaveData = HasAnySaveData || _saveState.PlayerRatings.Count > 0;

        _saveState.SelectedTeamName = null;
        _saveState.CurrentLiveMatch = null;
        _saveState.QuickMatchLiveMatch = null;
        _saveState.PlayerRatings.Clear();
        _saveState.PlayerSeasonStats.Clear();
        _saveState.Teams.Clear();
        SelectedTeam = null;
        PendingLiveMatchMode = LiveMatchMode.QuickMatch;

        if (hadSaveData)
        {
            Save();
        }

        return hadSaveData;
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

    private static void AssignPlayerToSlotWithSwap(List<Guid?> slots, Guid playerId, int targetSlotIndex)
    {
        if (targetSlotIndex < 0 || targetSlotIndex >= slots.Count)
        {
            return;
        }

        var sourceSlotIndex = FindPlayerSlotIndex(slots, playerId);
        if (sourceSlotIndex == targetSlotIndex)
        {
            return;
        }

        var displacedPlayerId = slots[targetSlotIndex];
        if (sourceSlotIndex >= 0)
        {
            slots[sourceSlotIndex] = displacedPlayerId;
        }

        slots[targetSlotIndex] = playerId;
    }

    private static int FindPlayerSlotIndex(List<Guid?> slots, Guid playerId)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] == playerId)
            {
                return i;
            }
        }

        return -1;
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

    private void MigrateLegacyFranchiseMatchIfNeeded(string teamName)
    {
        if (_saveState.CurrentLiveMatch == null)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(teamName);
        teamState.CurrentLiveMatch ??= _saveState.CurrentLiveMatch;
        _saveState.CurrentLiveMatch = null;
    }

    private void AdjustPlayerRatings(Guid playerId, Action<PlayerHiddenRatingsState> updateRatings)
    {
        if (!_saveState.PlayerRatings.TryGetValue(playerId, out var ratings))
        {
            return;
        }

        updateRatings(ratings);
        ClampRatings(ratings);
        ratings.RecalculateDerivedRatings();
    }

    private static void ClampRatings(PlayerHiddenRatingsState ratings)
    {
        ratings.ContactRating = Math.Clamp(ratings.ContactRating, 1, 99);
        ratings.PowerRating = Math.Clamp(ratings.PowerRating, 1, 99);
        ratings.DisciplineRating = Math.Clamp(ratings.DisciplineRating, 1, 99);
        ratings.SpeedRating = Math.Clamp(ratings.SpeedRating, 1, 99);
        ratings.FieldingRating = Math.Clamp(ratings.FieldingRating, 1, 99);
        ratings.ArmRating = Math.Clamp(ratings.ArmRating, 1, 99);
        ratings.PitchingRating = Math.Clamp(ratings.PitchingRating, 1, 99);
        ratings.DurabilityRating = Math.Clamp(ratings.DurabilityRating, 1, 99);
    }

    private void ApplySeasonStats(MatchPlayerSnapshot batter, MatchPlayerSnapshot pitcher, ResultEvent result)
    {
        var batterStats = GetPlayerSeasonStats(batter.Id);
        batterStats.PlateAppearances++;

        if (result.CountsAsAtBat)
        {
            batterStats.AtBats++;
        }

        if (result.CountsAsHit)
        {
            batterStats.Hits++;
        }

        if (result.IsWalk)
        {
            batterStats.Walks++;
        }

        if (result.IsStrikeout)
        {
            batterStats.Strikeouts++;
        }

        batterStats.RunsBattedIn += Math.Max(0, result.RunsScored);

        switch (result.Code)
        {
            case "Double":
                batterStats.Doubles++;
                break;
            case "Triple":
                batterStats.Triples++;
                break;
            case "HomeRun":
                batterStats.HomeRuns++;
                break;
        }

        var pitcherStats = GetPlayerSeasonStats(pitcher.Id);
        pitcherStats.InningsPitchedOuts += Math.Max(0, result.OutsRecorded);
        pitcherStats.RunsAllowed += Math.Max(0, result.RunsScored);
        pitcherStats.EarnedRuns += Math.Max(0, result.RunsScored);

        if (result.CountsAsHit)
        {
            pitcherStats.HitsAllowed++;
        }

        if (result.IsWalk)
        {
            pitcherStats.WalksAllowed++;
        }

        if (result.IsStrikeout)
        {
            pitcherStats.StrikeoutsPitched++;
        }
    }

    private void ApplyCompletedGame(MatchTeamState team, bool wonGame)
    {
        var countedPlayers = new HashSet<Guid>();
        foreach (var player in team.Lineup.Append(team.StartingPitcher))
        {
            if (!countedPlayers.Add(player.Id))
            {
                continue;
            }

            GetPlayerSeasonStats(player.Id).GamesPlayed++;
        }

        var pitcherStats = GetPlayerSeasonStats(team.StartingPitcher.Id);
        pitcherStats.GamesPitched++;
        if (wonGame)
        {
            pitcherStats.Wins++;
        }
        else
        {
            pitcherStats.Losses++;
        }
    }

    private bool EnsurePlayerRatingsGenerated()
    {
        var hasChanges = false;

        foreach (var player in _leagueData.Players)
        {
            if (_saveState.PlayerRatings.ContainsKey(player.PlayerId))
            {
                _saveState.PlayerRatings[player.PlayerId].RecalculateDerivedRatings();
                continue;
            }

            _saveState.PlayerRatings[player.PlayerId] = PlayerRatingsGenerator.Generate(player);
            hasChanges = true;
        }

        return hasChanges;
    }

    private bool EnsurePlayerSeasonStatsGenerated()
    {
        var hasChanges = false;

        foreach (var player in _leagueData.Players)
        {
            if (_saveState.PlayerSeasonStats.ContainsKey(player.PlayerId))
            {
                continue;
            }

            _saveState.PlayerSeasonStats[player.PlayerId] = new PlayerSeasonStatsState();
            hasChanges = true;
        }

        return hasChanges;
    }

    private void Save()
    {
        _stateStore.Save(_saveState);
    }
}
