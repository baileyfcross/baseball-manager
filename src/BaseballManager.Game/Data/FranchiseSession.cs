using BaseballManager.Contracts.ImportDtos;
using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Results;

namespace BaseballManager.Game.Data;

public sealed class FranchiseSession
{
    private static readonly string[] CoachRoleOrder = ["Manager", "Hitting Coach", "Pitching Coach", "Bench Coach", "Scouting Director"];
    private static readonly string[] CoachFirstNames = ["Alex", "Jordan", "Sam", "Casey", "Drew", "Taylor", "Riley", "Morgan", "Cameron", "Jamie", "Hayden", "Avery"];
    private static readonly string[] CoachLastNames = ["Maddox", "Sullivan", "Torres", "Bennett", "Foster", "Callahan", "Diaz", "Reed", "Hughes", "Alvarez", "Parker", "Watts"];
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseStateStore _stateStore;
    private readonly FranchiseSaveState _saveState;
    private readonly Dictionary<Guid, PlayerSeasonStatsState> _lastSeasonStatsCache = new();

    public FranchiseSession(ImportedLeagueData leagueData, FranchiseStateStore stateStore)
    {
        _leagueData = leagueData;
        _stateStore = stateStore;
        _saveState = _stateStore.Load();
        _saveState.PlayerRatings ??= new Dictionary<Guid, PlayerHiddenRatingsState>();
        _saveState.PlayerSeasonStats ??= new Dictionary<Guid, PlayerSeasonStatsState>();
        _saveState.PlayerAssignments ??= new Dictionary<Guid, string>();
        _saveState.CompletedScheduleGameKeys ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _saveState.CompletedScheduleGameResults ??= new Dictionary<string, CompletedScheduleGameResult>(StringComparer.OrdinalIgnoreCase);

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

        EnsureFranchiseDateInitialized();

        if (EnsurePlayerRatingsGenerated() || EnsurePlayerSeasonStatsGenerated() || EnsurePlayerAssignmentsGenerated() || EnsureCoachingStaffGenerated())
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

    public void UpdateClockVisibility(bool showRealTimeClock)
    {
        var displaySettings = GetDisplaySettings();
        if (displaySettings.ShowRealTimeClock == showRealTimeClock)
        {
            return;
        }

        displaySettings.ShowRealTimeClock = showRealTimeClock;
        Save();
    }

    public DateTime GetCurrentFranchiseDate()
    {
        EnsureFranchiseDateInitialized();
        return _saveState.CurrentFranchiseDate.Date;
    }

    public bool IsScheduledGameCompleted(ScheduleImportDto game)
    {
        return _saveState.CompletedScheduleGameKeys.Contains(BuildScheduleGameKey(game));
    }

    public string GetScheduledGameScore(ScheduleImportDto game)
    {
        if (_saveState.CompletedScheduleGameResults.TryGetValue(BuildScheduleGameKey(game), out var result))
        {
            return $"{result.AwayRuns}-{result.HomeRuns}";
        }

        return "-";
    }

    public ScheduleImportDto? GetNextScheduledGame()
    {
        return GetNextScheduledGameForSelectedTeam();
    }

    public void SelectTeam(TeamImportDto team)
    {
        SelectedTeam = team;
        PendingLiveMatchMode = LiveMatchMode.Franchise;
        _saveState.SelectedTeamName = team.Name;
        _saveState.CurrentFranchiseDate = default;
        MigrateLegacyFranchiseMatchIfNeeded(team.Name);
        EnsureFranchiseDateInitialized();
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

    public PlayerSeasonStatsState GetLastSeasonStats(Guid playerId, string fullName, string primaryPosition, string secondaryPosition, int age)
    {
        if (_lastSeasonStatsCache.TryGetValue(playerId, out var stats))
        {
            return stats;
        }

        var ratings = GetPlayerRatings(playerId, fullName, primaryPosition, secondaryPosition, age);
        stats = GenerateLastSeasonStats(playerId, primaryPosition, age, ratings);
        _lastSeasonStatsCache[playerId] = stats;
        return stats;
    }

    public IReadOnlyList<CoachProfileView> GetCoachingStaff()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureCoachingStaffInitialized(teamState, SelectedTeam.Name);

        return teamState.CoachingStaff
            .OrderBy(coach => GetCoachRoleSortOrder(coach.Role))
            .Select(coach => new CoachProfileView(coach.Role, coach.Name, coach.Specialty, coach.Voice))
            .ToList();
    }

    public bool ChangeCoach(string role, int direction, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before changing your staff.";
            return false;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureCoachingStaffInitialized(teamState, SelectedTeam.Name);

        var coach = teamState.CoachingStaff.FirstOrDefault(entry =>
            string.Equals(entry.Role, role, StringComparison.OrdinalIgnoreCase));

        if (coach == null)
        {
            statusMessage = "That coaching role is not available right now.";
            return false;
        }

        var candidatePool = BuildCoachCandidatePool(SelectedTeam.Name, role);
        if (candidatePool.Count == 0)
        {
            statusMessage = "No replacement coaches are available right now.";
            return false;
        }

        var currentIndex = candidatePool.FindIndex(candidate =>
            string.Equals(candidate.Name, coach.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Specialty, coach.Specialty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Voice, coach.Voice, StringComparison.OrdinalIgnoreCase));

        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var step = direction >= 0 ? 1 : -1;
        var nextIndex = (currentIndex + step + candidatePool.Count) % candidatePool.Count;
        var replacement = candidatePool[nextIndex];

        coach.Name = replacement.Name;
        coach.Specialty = replacement.Specialty;
        coach.Voice = replacement.Voice;
        Save();

        statusMessage = $"{coach.Name} is now your {coach.Role.ToLowerInvariant()}.";
        return true;
    }

    public IReadOnlyList<ScoutingPlayerCard> GetScoutingBoardPlayers()
    {
        var selectedTeamName = SelectedTeam?.Name;
        var teamsByName = _leagueData.Teams.ToDictionary(team => team.Name, team => team, StringComparer.OrdinalIgnoreCase);

        return _leagueData.Players
            .Select(player =>
            {
                var assignedTeamName = GetAssignedTeamName(player.PlayerId, player.TeamName);
                teamsByName.TryGetValue(assignedTeamName, out var team);

                return new ScoutingPlayerCard(
                    player.PlayerId,
                    player.FullName,
                    assignedTeamName,
                    team?.Abbreviation ?? "FA",
                    player.PrimaryPosition,
                    player.SecondaryPosition,
                    player.Age,
                    selectedTeamName != null && string.Equals(assignedTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase));
            })
            .OrderBy(card => card.IsOnSelectedTeam)
            .ThenBy(card => card.TeamName)
            .ThenBy(card => card.PrimaryPosition)
            .ThenBy(card => card.PlayerName)
            .ToList();
    }

    public IReadOnlyList<ScoutingPlayerCard> GetTradeChipPlayers()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        return GetSelectedTeamRoster()
            .OrderBy(player => player.RotationSlot ?? 99)
            .ThenBy(player => player.LineupSlot ?? 99)
            .ThenBy(player => player.PlayerName)
            .Select(player => new ScoutingPlayerCard(
                player.PlayerId,
                player.PlayerName,
                SelectedTeam.Name,
                SelectedTeam.Abbreviation,
                player.PrimaryPosition,
                player.SecondaryPosition,
                player.Age,
                true))
            .ToList();
    }

    public CoachScoutingReport GetCoachScoutingReport(Guid playerId, string coachRole)
    {
        var player = FindPlayerImport(playerId);
        if (player == null)
        {
            return new CoachScoutingReport(
                "Staff Report",
                coachRole,
                "Unknown Player",
                "I need a few more looks before I can give you a real read.",
                "",
                "",
                "Check back after another series.");
        }

        var availableCoaches = GetCoachingStaff();
        var coach = availableCoaches.FirstOrDefault(entry => string.Equals(entry.Role, coachRole, StringComparison.OrdinalIgnoreCase))
            ?? availableCoaches.FirstOrDefault()
            ?? new CoachProfileView("Scouting Director", "Staff Report", "league coverage", "balanced");

        var assignedTeamName = GetAssignedTeamName(player.PlayerId, player.TeamName);
        var isPitcher = player.PrimaryPosition is "SP" or "RP";
        var ratings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var strengths = GetTopAttributesForCoach(ratings, coach.Role, isPitcher).Take(2).ToList();
        var concern = GetConcernAttribute(ratings, coach.Role, isPitcher);

        return new CoachScoutingReport(
            coach.Name,
            coach.Role,
            player.FullName,
            BuildOverallSummary(ratings.OverallRating, player.Age, coach, isPitcher, assignedTeamName),
            BuildStrengthsLine(strengths, coach.Role, isPitcher),
            BuildConcernLine(concern, coach.Role, isPitcher),
            BuildTransferRecommendation(ratings.OverallRating, player.Age, assignedTeamName, isPitcher));
    }

    public string GetQuickScoutNote(Guid playerId)
    {
        var report = GetCoachScoutingReport(playerId, "Scouting Director");
        return $"{report.Summary} {report.Strengths}".Trim();
    }

    public bool TryTradeForPlayer(Guid targetPlayerId, Guid offeredPlayerId, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before trying to make a move.";
            return false;
        }

        if (targetPlayerId == offeredPlayerId)
        {
            statusMessage = "Choose two different players for the swap.";
            return false;
        }

        var targetPlayer = FindPlayerImport(targetPlayerId);
        var offeredPlayer = FindPlayerImport(offeredPlayerId);
        if (targetPlayer == null || offeredPlayer == null)
        {
            statusMessage = "One of those players could not be found.";
            return false;
        }

        var selectedTeamName = SelectedTeam.Name;
        var targetTeamName = GetAssignedTeamName(targetPlayerId, targetPlayer.TeamName);
        var offeredTeamName = GetAssignedTeamName(offeredPlayerId, offeredPlayer.TeamName);

        if (string.Equals(targetTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = $"{targetPlayer.FullName} is already on your club.";
            return false;
        }

        if (!string.Equals(offeredTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = "Your outgoing player has to come from your current roster.";
            return false;
        }

        var selectedTeamState = GetOrCreateTeamState(selectedTeamName);
        var targetTeamState = GetOrCreateTeamState(targetTeamName);
        InitializeLineupSlots(selectedTeamState, selectedTeamName);
        InitializeRotationSlots(selectedTeamState, selectedTeamName);
        InitializeLineupSlots(targetTeamState, targetTeamName);
        InitializeRotationSlots(targetTeamState, targetTeamName);

        var offeredLineupIndex = FindPlayerSlotIndex(selectedTeamState.LineupSlots, offeredPlayerId);
        var offeredRotationIndex = FindPlayerSlotIndex(selectedTeamState.RotationSlots, offeredPlayerId);
        var targetLineupIndex = FindPlayerSlotIndex(targetTeamState.LineupSlots, targetPlayerId);
        var targetRotationIndex = FindPlayerSlotIndex(targetTeamState.RotationSlots, targetPlayerId);

        _saveState.PlayerAssignments[targetPlayerId] = selectedTeamName;
        _saveState.PlayerAssignments[offeredPlayerId] = targetTeamName;

        ClearPlayerFromSlots(selectedTeamState.LineupSlots, offeredPlayerId);
        ClearPlayerFromSlots(selectedTeamState.RotationSlots, offeredPlayerId);
        ClearPlayerFromSlots(selectedTeamState.LineupSlots, targetPlayerId);
        ClearPlayerFromSlots(selectedTeamState.RotationSlots, targetPlayerId);
        ClearPlayerFromSlots(targetTeamState.LineupSlots, targetPlayerId);
        ClearPlayerFromSlots(targetTeamState.RotationSlots, targetPlayerId);
        ClearPlayerFromSlots(targetTeamState.LineupSlots, offeredPlayerId);
        ClearPlayerFromSlots(targetTeamState.RotationSlots, offeredPlayerId);

        TryAssignPlayerToPreviousSlots(selectedTeamState, targetPlayerId, targetPlayer.PrimaryPosition, offeredLineupIndex, offeredRotationIndex);
        TryAssignPlayerToPreviousSlots(targetTeamState, offeredPlayerId, offeredPlayer.PrimaryPosition, targetLineupIndex, targetRotationIndex);

        AddTransferRecord(selectedTeamName, $"Acquired {targetPlayer.FullName} from {targetTeamName} for {offeredPlayer.FullName}.");
        AddTransferRecord(targetTeamName, $"Sent {targetPlayer.FullName} to {selectedTeamName} for {offeredPlayer.FullName}.");
        Save();

        statusMessage = $"Deal complete: {targetPlayer.FullName} joins {selectedTeamName} and {offeredPlayer.FullName} heads to {targetTeamName}.";
        return true;
    }

    public IReadOnlyList<string> GetRecentTransferSummaries(int maxCount = 4)
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        return teamState.TransferHistory
            .OrderByDescending(entry => entry.EffectiveDate)
            .Take(Math.Max(1, maxCount))
            .Select(entry => $"{entry.EffectiveDate:MM/dd}: {entry.Description}")
            .ToList();
    }

    public bool SimulateNextScheduledGame(out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before simulating games.";
            return false;
        }

        var nextGame = GetNextScheduledGameForSelectedTeam();
        if (nextGame == null)
        {
            statusMessage = "No remaining scheduled games found.";
            return false;
        }

        var awayTeam = FindTeamByName(nextGame.AwayTeamName);
        var homeTeam = FindTeamByName(nextGame.HomeTeamName);
        if (awayTeam == null || homeTeam == null)
        {
            statusMessage = "Could not resolve teams for the scheduled game.";
            return false;
        }

        _saveState.CurrentFranchiseDate = nextGame.Date.Date;

        var useFranchiseSelectionsForAway = string.Equals(SelectedTeam.Name, awayTeam.Name, StringComparison.OrdinalIgnoreCase);
        var useFranchiseSelectionsForHome = string.Equals(SelectedTeam.Name, homeTeam.Name, StringComparison.OrdinalIgnoreCase);
        var awaySnapshot = BuildTeamSnapshot(awayTeam, useFranchiseSelectionsForAway);
        var homeSnapshot = BuildTeamSnapshot(homeTeam, useFranchiseSelectionsForHome);
        var engine = new MatchEngine(awaySnapshot, homeSnapshot);

        while (!engine.CurrentState.IsGameOver)
        {
            var batter = engine.CurrentState.CurrentBatter;
            var pitcher = engine.CurrentState.CurrentPitcher;
            var defensiveTeam = engine.CurrentState.DefensiveTeam;
            var result = engine.Tick();
            ApplyPerformanceDevelopment(batter, pitcher, defensiveTeam, result);
        }

        RecordCompletedGame(engine.CurrentState);
        FinalizeFranchiseScheduledGame(engine.CurrentState, nextGame);
        ClearLiveMatchState(LiveMatchMode.Franchise);
        Save();

        statusMessage = $"Simulated {awayTeam.Abbreviation} {engine.CurrentState.AwayTeam.Runs} - {engine.CurrentState.HomeTeam.Runs} {homeTeam.Abbreviation} on {nextGame.Date:yyyy-MM-dd}.";
        return true;
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
        return SelectedTeam == null ? [] : GetTeamRoster(SelectedTeam.Name);
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
        _saveState.CurrentFranchiseDate = DateTime.Today;
        _saveState.CompletedScheduleGameKeys.Clear();
        _saveState.CompletedScheduleGameResults.Clear();
        _saveState.PlayerAssignments.Clear();

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
        _saveState.PlayerAssignments.Clear();
        _saveState.CompletedScheduleGameKeys.Clear();
        _saveState.CompletedScheduleGameResults.Clear();
        _saveState.CurrentFranchiseDate = DateTime.Today;
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

    private IReadOnlyList<FranchiseRosterEntry> GetTeamRoster(string teamName)
    {
        var rostersByPlayerId = _leagueData.Rosters
            .GroupBy(roster => roster.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());
        var lineupSlots = BuildLineupSlots(teamName);
        var rotationSlots = BuildRotationSlots(teamName);
        var lineupMap = BuildSlotMap(lineupSlots);
        var rotationMap = BuildSlotMap(rotationSlots);

        return _leagueData.Players
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .Select(player =>
            {
                rostersByPlayerId.TryGetValue(player.PlayerId, out var roster);
                var playerName = roster?.PlayerName ?? player.FullName;
                var primaryPosition = string.IsNullOrWhiteSpace(roster?.PrimaryPosition) ? player.PrimaryPosition : roster.PrimaryPosition;
                var secondaryPosition = string.IsNullOrWhiteSpace(roster?.SecondaryPosition) ? player.SecondaryPosition : roster.SecondaryPosition;

                return new FranchiseRosterEntry(
                    player.PlayerId,
                    playerName,
                    primaryPosition,
                    secondaryPosition,
                    player.Age,
                    lineupMap.TryGetValue(player.PlayerId, out var lineupSlot) ? lineupSlot : null,
                    rotationMap.TryGetValue(player.PlayerId, out var rotationSlot) ? rotationSlot : null);
            })
            .OrderBy(entry => entry.PlayerName)
            .ToList();
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

    private string GetAssignedTeamName(Guid playerId, string fallbackTeamName)
    {
        if (_saveState.PlayerAssignments.TryGetValue(playerId, out var assignedTeamName) && !string.IsNullOrWhiteSpace(assignedTeamName))
        {
            return assignedTeamName;
        }

        return fallbackTeamName;
    }

    private PlayerImportDto? FindPlayerImport(Guid playerId)
    {
        return _leagueData.Players.FirstOrDefault(player => player.PlayerId == playerId);
    }

    private bool EnsurePlayerAssignmentsGenerated()
    {
        var hasChanges = false;

        foreach (var player in _leagueData.Players)
        {
            if (_saveState.PlayerAssignments.TryGetValue(player.PlayerId, out var assignedTeamName) && !string.IsNullOrWhiteSpace(assignedTeamName))
            {
                continue;
            }

            var fallbackTeamName = !string.IsNullOrWhiteSpace(player.TeamName)
                ? player.TeamName
                : _leagueData.Rosters.FirstOrDefault(roster => roster.PlayerId == player.PlayerId)?.TeamName ?? string.Empty;

            _saveState.PlayerAssignments[player.PlayerId] = fallbackTeamName;
            hasChanges = true;
        }

        return hasChanges;
    }

    private bool EnsureCoachingStaffGenerated()
    {
        var hasChanges = false;
        foreach (var team in _leagueData.Teams)
        {
            hasChanges |= EnsureCoachingStaffInitialized(GetOrCreateTeamState(team.Name), team.Name);
        }

        return hasChanges;
    }

    private bool EnsureCoachingStaffInitialized(TeamFranchiseState teamState, string teamName)
    {
        var hasChanges = false;
        teamState.CoachingStaff ??= new List<CoachAssignmentState>();

        foreach (var role in CoachRoleOrder)
        {
            if (teamState.CoachingStaff.Any(coach => string.Equals(coach.Role, role, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var candidate = BuildCoachCandidatePool(teamName, role).First();
            teamState.CoachingStaff.Add(new CoachAssignmentState
            {
                Role = role,
                Name = candidate.Name,
                Specialty = candidate.Specialty,
                Voice = candidate.Voice
            });
            hasChanges = true;
        }

        teamState.CoachingStaff = teamState.CoachingStaff
            .OrderBy(coach => GetCoachRoleSortOrder(coach.Role))
            .ToList();

        return hasChanges;
    }

    private List<CoachAssignmentState> BuildCoachCandidatePool(string teamName, string role)
    {
        var specialties = GetRoleSpecialties(role);
        var voices = GetRoleVoices(role);
        var seed = GetStableHash($"{teamName}|{role}");
        var candidates = new List<CoachAssignmentState>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < 6; i++)
        {
            var firstName = CoachFirstNames[(seed + (i * 3)) % CoachFirstNames.Length];
            var lastName = CoachLastNames[(seed / 3 + (i * 5)) % CoachLastNames.Length];
            var specialty = specialties[(seed / 5 + i) % specialties.Length];
            var voice = voices[(seed / 7 + i) % voices.Length];
            var name = $"{firstName} {lastName}";
            var key = $"{name}|{specialty}|{voice}";

            if (!seen.Add(key))
            {
                continue;
            }

            candidates.Add(new CoachAssignmentState
            {
                Role = role,
                Name = name,
                Specialty = specialty,
                Voice = voice
            });
        }

        return candidates;
    }

    private void InitializeLineupSlots(TeamFranchiseState teamState, string teamName)
    {
        if (teamState.LineupSlots.Count != 9)
        {
            teamState.LineupSlots = Enumerable.Repeat<Guid?>(null, 9).ToList();
            foreach (var roster in _leagueData.Rosters.Where(roster =>
                         string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase) &&
                         roster.LineupSlot is >= 1 and <= 9))
            {
                teamState.LineupSlots[roster.LineupSlot!.Value - 1] = roster.PlayerId;
            }
        }

        SanitizeSlotsForCurrentRoster(teamState.LineupSlots, teamName);
    }

    private void InitializeRotationSlots(TeamFranchiseState teamState, string teamName)
    {
        if (teamState.RotationSlots.Count != 5)
        {
            teamState.RotationSlots = Enumerable.Repeat<Guid?>(null, 5).ToList();
            foreach (var roster in _leagueData.Rosters.Where(roster =>
                         string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase) &&
                         roster.RotationSlot is >= 1 and <= 5))
            {
                teamState.RotationSlots[roster.RotationSlot!.Value - 1] = roster.PlayerId;
            }
        }

        SanitizeSlotsForCurrentRoster(teamState.RotationSlots, teamName);
    }

    private void SanitizeSlotsForCurrentRoster(List<Guid?> slots, string teamName)
    {
        var validIds = _leagueData.Players
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .Select(player => player.PlayerId)
            .ToHashSet();
        var seen = new HashSet<Guid>();

        for (var i = 0; i < slots.Count; i++)
        {
            if (!slots[i].HasValue)
            {
                continue;
            }

            var playerId = slots[i]!.Value;
            if (!validIds.Contains(playerId) || !seen.Add(playerId))
            {
                slots[i] = null;
            }
        }
    }

    private void TryAssignPlayerToPreviousSlots(TeamFranchiseState teamState, Guid playerId, string primaryPosition, int lineupIndex, int rotationIndex)
    {
        var isPitcher = primaryPosition is "SP" or "RP";

        if (isPitcher)
        {
            if (rotationIndex >= 0)
            {
                AssignPlayerToSlotWithSwap(teamState.RotationSlots, playerId, rotationIndex);
                return;
            }

            var firstOpenRotation = teamState.RotationSlots.FindIndex(slot => !slot.HasValue);
            if (firstOpenRotation >= 0)
            {
                AssignPlayerToSlotWithSwap(teamState.RotationSlots, playerId, firstOpenRotation);
            }

            return;
        }

        if (lineupIndex >= 0)
        {
            AssignPlayerToSlotWithSwap(teamState.LineupSlots, playerId, lineupIndex);
            return;
        }

        var firstOpenLineup = teamState.LineupSlots.FindIndex(slot => !slot.HasValue);
        if (firstOpenLineup >= 0)
        {
            AssignPlayerToSlotWithSwap(teamState.LineupSlots, playerId, firstOpenLineup);
        }
    }

    private void AddTransferRecord(string teamName, string description)
    {
        var teamState = GetOrCreateTeamState(teamName);
        teamState.TransferHistory ??= new List<TransferRecordState>();
        teamState.TransferHistory.Insert(0, new TransferRecordState
        {
            EffectiveDate = GetCurrentFranchiseDate(),
            Description = description
        });

        if (teamState.TransferHistory.Count > 12)
        {
            teamState.TransferHistory = teamState.TransferHistory.Take(12).ToList();
        }
    }

    private List<AttributeInsight> GetTopAttributesForCoach(PlayerHiddenRatingsState ratings, string coachRole, bool isPitcher)
    {
        var focusKeys = GetFocusKeysForCoach(coachRole, isPitcher);
        return GetAttributeInsights(ratings)
            .Where(attribute => focusKeys.Contains(attribute.Key, StringComparer.Ordinal))
            .OrderByDescending(attribute => attribute.Rating)
            .ThenBy(attribute => attribute.Key)
            .ToList();
    }

    private AttributeInsight GetConcernAttribute(PlayerHiddenRatingsState ratings, string coachRole, bool isPitcher)
    {
        var focusKeys = GetFocusKeysForCoach(coachRole, isPitcher);
        return GetAttributeInsights(ratings)
            .Where(attribute => focusKeys.Contains(attribute.Key, StringComparer.Ordinal))
            .OrderBy(attribute => attribute.Rating)
            .ThenBy(attribute => attribute.Key)
            .FirstOrDefault() ?? GetAttributeInsights(ratings).OrderBy(attribute => attribute.Rating).First();
    }

    private static string BuildOverallSummary(int overallRating, int age, CoachProfileView coach, bool isPitcher, string teamName)
    {
        var random = CreateStableRandom(coach.Name, coach.Role, teamName, age.ToString(), overallRating.ToString());

        var ceilingLine = overallRating switch
        {
            >= 72 => Pick(random, isPitcher
                ? ["That's a tone-setting arm.", "He looks like a front-line mound piece.", "That is the kind of pitcher you trust in big spots."]
                : ["That looks like a true difference-maker.", "He's the sort of player who changes a lineup.", "That's an impact everyday piece."]),
            >= 62 => Pick(random, isPitcher
                ? ["I see a reliable rotation arm there.", "He looks like a pitcher who can hold a staff together.", "That's a useful arm you can win with."]
                : ["He looks like a solid regular to me.", "There's real everyday value in that player.", "I can see him helping a good club quite a bit."]),
            >= 52 => Pick(random, isPitcher
                ? ["There's enough there to help a staff.", "He feels like a workable innings arm.", "I see useful depth with a little upside."]
                : ["He feels like a playable contributor.", "There's a steady role player in there.", "I can see him helping in the right setup."]),
            >= 44 => Pick(random, isPitcher
                ? ["More of a depth look right now.", "I see a back-end option at the moment.", "He'd be fighting for innings more than leading the group."]
                : ["This looks more like depth than a locked-in starter.", "I'd call him a support piece right now.", "He's more useful than flashy at this stage."]),
            _ => Pick(random, isPitcher
                ? ["That's still a project arm.", "He needs plenty more polish on the mound.", "Right now it is more upside than certainty."]
                : ["He still looks pretty raw to me.", "There is a long way to go before he's a sure thing.", "I would call that a developmental piece for now."])
        };

        var ageLine = age switch
        {
            <= 23 => Pick(random, ["He's young enough to still take another jump.", "There is room for more growth there.", "The best version of him may still be ahead."]),
            >= 34 => Pick(random, ["At this age, you're mostly buying the finished product.", "You know more or less what you're getting now.", "The veteran knows his game, even if the ceiling is set."]),
            _ => Pick(random, ["He looks like a fairly known quantity.", "You're getting a player who knows what he is.", "This version of him feels pretty settled."])
        };

        return $"{ceilingLine} {ageLine}";
    }

    private static string BuildStrengthsLine(IReadOnlyList<AttributeInsight> strengths, string coachRole, bool isPitcher)
    {
        if (strengths.Count == 0)
        {
            return "Nothing jumps off the page yet, but there is something to work with.";
        }

        var random = CreateStableRandom(coachRole, string.Join('|', strengths.Select(attribute => attribute.Key)), strengths.Sum(attribute => attribute.Rating).ToString(), isPitcher.ToString());
        var firstPraise = BuildAttributePraise(strengths[0], isPitcher, random);

        if (strengths.Count == 1)
        {
            return $"The first thing I notice is that {firstPraise}.";
        }

        var secondPraise = BuildAttributePraise(strengths[1], isPitcher, random);
        return $"The first thing I notice is that {firstPraise}, and {secondPraise}.";
    }

    private static string BuildConcernLine(AttributeInsight concern, string coachRole, bool isPitcher)
    {
        var random = CreateStableRandom(coachRole, concern.Key, concern.Rating.ToString(), isPitcher.ToString());
        return concern.Rating >= 58
            ? Pick(random, ["There really is not a glaring hole in his game right now.", "I do not see a major red flag at first glance.", "You can nitpick, but there is not an obvious weak spot."])
            : $"My only hesitation is that {BuildAttributeConcern(concern, isPitcher, random)}.";
    }

    private string BuildTransferRecommendation(int overallRating, int age, string teamName, bool isPitcher)
    {
        if (SelectedTeam != null && string.Equals(teamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
        {
            return "He is already in your room, so this is more about how you use him than whether you chase him.";
        }

        var random = CreateStableRandom(teamName, overallRating.ToString(), age.ToString(), isPitcher.ToString());
        return overallRating switch
        {
            >= 68 => Pick(random, ["If the price stays sane, I would push to make the move.", "That is the kind of player worth making a real offer for.", "I would be comfortable being aggressive there."]),
            >= 58 => Pick(random, ["I would explore it if the deal does not gut your depth.", "Worth a serious call if you can keep the cost reasonable.", "That is a move I would listen on, not force."]),
            >= 48 => Pick(random, ["Only do it if the price stays light.", "I would treat him as a secondary target, not the headliner.", "That feels more like a depth move than a splash."]),
            _ => Pick(random, ["I would not force that one unless you just need organizational depth.", "That is more of a wait-and-see target for me.", "I would save your bigger chips for someone else."])
        };
    }

    private static IReadOnlyList<AttributeInsight> GetAttributeInsights(PlayerHiddenRatingsState ratings)
    {
        return
        [
            new AttributeInsight("contact", ratings.ContactRating),
            new AttributeInsight("power", ratings.PowerRating),
            new AttributeInsight("discipline", ratings.DisciplineRating),
            new AttributeInsight("speed", ratings.SpeedRating),
            new AttributeInsight("fielding", ratings.FieldingRating),
            new AttributeInsight("arm", ratings.ArmRating),
            new AttributeInsight("pitching", ratings.PitchingRating),
            new AttributeInsight("durability", ratings.DurabilityRating)
        ];
    }

    private static string[] GetFocusKeysForCoach(string coachRole, bool isPitcher)
    {
        return coachRole switch
        {
            "Hitting Coach" when isPitcher => ["pitching", "durability", "arm", "discipline"],
            "Hitting Coach" => ["contact", "power", "discipline", "speed"],
            "Pitching Coach" when isPitcher => ["pitching", "durability", "arm", "discipline"],
            "Pitching Coach" => ["arm", "fielding", "speed", "durability"],
            "Bench Coach" when isPitcher => ["durability", "arm", "fielding", "discipline"],
            "Bench Coach" => ["fielding", "speed", "arm", "discipline"],
            "Manager" when isPitcher => ["pitching", "durability", "discipline", "arm"],
            "Manager" => ["contact", "discipline", "durability", "fielding"],
            _ when isPitcher => ["pitching", "durability", "arm", "discipline"],
            _ => ["contact", "power", "speed", "fielding"]
        };
    }

    private static string BuildAttributePraise(AttributeInsight attribute, bool isPitcher, Random random)
    {
        return attribute.Key switch
        {
            "contact" => attribute.Rating >= 70
                ? Pick(random, ["he squares balls up all over the zone", "the barrel keeps finding the baseball", "he makes a lot of clean contact"])
                : Pick(random, ["there is some usable bat-to-ball feel", "he can still put together a competitive at-bat", "there is enough contact skill to work with"]),
            "power" => attribute.Rating >= 70
                ? Pick(random, ["the pop really jumps off the bat", "he can do damage when he gets one to his pull side", "the raw thump is pretty clear"])
                : Pick(random, ["there is a little bit of carry in the bat", "you can see flashes of extra-base juice", "he has enough strength to sting one now and then"]),
            "discipline" => attribute.Rating >= 70
                ? Pick(random, ["he usually knows which pitches to leave alone", "the strike-zone judgment is advanced", "he does not give away many at-bats"])
                : Pick(random, ["there is some feel for the zone", "he can grind through a plate appearance", "he is not up there guessing every swing"]),
            "speed" => attribute.Rating >= 70
                ? Pick(random, ["he covers a lot of ground in a hurry", "the quickness shows up right away", "he moves like someone who can pressure a defense"])
                : Pick(random, ["there is enough athleticism to help", "he moves well enough to be useful", "the legs are playable"]),
            "fielding" => attribute.Rating >= 70
                ? Pick(random, ["the glove work looks steady and trustworthy", "he handles himself cleanly in the field", "the defensive instincts stand out"])
                : Pick(random, ["he can hold his own with the glove", "the defense should not hurt you much", "there is enough steadiness in the field"]),
            "arm" => attribute.Rating >= 70
                ? Pick(random, ["the arm has real carry on it", "there is legitimate strength in the throw", "the ball comes out of his hand with life"])
                : Pick(random, ["the arm is passable", "he has enough arm to get by", "there is some utility in the arm strength"]),
            "pitching" => attribute.Rating >= 70
                ? Pick(random, ["the mound stuff is good enough to miss bats", "his stuff can drive the action on the mound", "the pitch quality is what gets your attention first"])
                : Pick(random, ["there is enough on the mound to compete", "he can survive on the hill with that mix", "the arm talent gives him a chance"]),
            _ => attribute.Rating >= 70
                ? Pick(random, ["he looks built to hold up over a long stretch", "the body looks ready for a workload", "he should be able to handle regular action"])
                : Pick(random, ["he can handle a fair amount of work", "the durability looks serviceable", "he should be able to stay in the mix"])
        };
    }

    private static string BuildAttributeConcern(AttributeInsight attribute, bool isPitcher, Random random)
    {
        return attribute.Key switch
        {
            "contact" => Pick(random, ["the contact consistency still comes and goes", "he can get a bit swing-and-miss heavy", "I do not fully trust the barrel every trip"]),
            "power" => Pick(random, ["the raw power is light", "he is not bringing much true thump right now", "there is not a lot of extra-base impact yet"]),
            "discipline" => Pick(random, ["he can chase more than you would like", "the at-bat quality gets loose at times", "the zone awareness is still pretty spotty"]),
            "speed" => Pick(random, ["the foot speed is ordinary", "he is not going to pressure many defenses with his legs", "there is not much extra quickness there"]),
            "fielding" => Pick(random, ["the glove can get a little shaky", "the defensive reliability is not fully there yet", "I would not call the fielding a strength"]),
            "arm" => Pick(random, ["the arm is a little light for impact work", "the throw does not always carry the way you want", "there is only modest arm strength there"]),
            "pitching" => Pick(random, ["the mound quality still needs work", "I am not fully sold on the pure stuff yet", "there is not enough weaponry there right now"]),
            _ => Pick(random, ["I would want to watch how his body holds up over time", "the long-haul durability still worries me a little", "there is some risk if you ask for a heavy workload"])
        };
    }

    private static string[] GetRoleSpecialties(string role)
    {
        return role switch
        {
            "Manager" => ["clubhouse balance", "game management", "steady leadership", "big-moment calm"],
            "Hitting Coach" => ["barrel control", "plate discipline", "pull-side juice", "situational hitting"],
            "Pitching Coach" => ["mound command", "durability planning", "velocity work", "finishing secondary pitches"],
            "Bench Coach" => ["defensive positioning", "baserunning pressure", "late-game matchups", "versatility work"],
            _ => ["long-range projection", "ceiling reads", "makeup and instincts", "league coverage"]
        };
    }

    private static string[] GetRoleVoices(string role)
    {
        return role switch
        {
            "Manager" => ["steady", "direct", "measured"],
            "Hitting Coach" => ["bat-first", "plain-spoken", "detail-heavy"],
            "Pitching Coach" => ["grinder", "calm", "old-school"],
            "Bench Coach" => ["practical", "sharp-eyed", "situational"],
            _ => ["balanced", "optimistic", "cautious"]
        };
    }

    private static int GetCoachRoleSortOrder(string role)
    {
        var index = Array.FindIndex(CoachRoleOrder, entry => string.Equals(entry, role, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? CoachRoleOrder.Length : index;
    }

    private static Random CreateStableRandom(params string[] parts)
    {
        return new Random(GetStableHash(string.Join('|', parts)));
    }

    private static int GetStableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + char.ToUpperInvariant(character);
            }

            return hash & int.MaxValue;
        }
    }

    private static string Pick(Random random, string[] options)
    {
        return options.Length == 0 ? string.Empty : options[random.Next(options.Length)];
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

    private void EnsureFranchiseDateInitialized()
    {
        if (_saveState.CurrentFranchiseDate != default)
        {
            return;
        }

        var firstScheduled = SelectedTeam == null
            ? _leagueData.Schedule.OrderBy(game => game.Date).FirstOrDefault()?.Date
            : _leagueData.Schedule
                .Where(game => string.Equals(game.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(game.AwayTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(game => game.Date)
                .ThenBy(game => game.GameNumber)
                .FirstOrDefault()?.Date;

        _saveState.CurrentFranchiseDate = (firstScheduled ?? DateTime.Today).Date;
    }

    private ScheduleImportDto? GetNextScheduledGameForSelectedTeam()
    {
        if (SelectedTeam == null)
        {
            return null;
        }

        EnsureFranchiseDateInitialized();
        var teamName = SelectedTeam.Name;
        var today = _saveState.CurrentFranchiseDate.Date;

        var next = _leagueData.Schedule
            .Where(game =>
                (string.Equals(game.HomeTeamName, teamName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(game.AwayTeamName, teamName, StringComparison.OrdinalIgnoreCase)) &&
                game.Date.Date >= today &&
                !IsScheduledGameCompleted(game))
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameNumber)
            .FirstOrDefault();

        if (next != null)
        {
            return next;
        }

        return _leagueData.Schedule
            .Where(game =>
                (string.Equals(game.HomeTeamName, teamName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(game.AwayTeamName, teamName, StringComparison.OrdinalIgnoreCase)) &&
                !IsScheduledGameCompleted(game))
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameNumber)
            .FirstOrDefault();
    }

    private void AdvanceFranchiseDateToNextUnplayedGame()
    {
        var nextGame = GetNextScheduledGameForSelectedTeam();
        if (nextGame != null)
        {
            _saveState.CurrentFranchiseDate = nextGame.Date.Date;
            return;
        }

        _saveState.CurrentFranchiseDate = _saveState.CurrentFranchiseDate.Date.AddDays(1);
    }

    private MatchTeamState BuildTeamSnapshot(TeamImportDto team, bool preferFranchiseSelections)
    {
        var lineup = preferFranchiseSelections
            ? BuildSelectedTeamLineup()
            : BuildImportedTeamLineup(team.Name);

        if (lineup.Count == 0)
        {
            lineup = BuildPlaceholderLineup(team.Name);
        }

        var pitcher = preferFranchiseSelections
            ? BuildSelectedTeamPitcher() ?? lineup.First()
            : BuildImportedPitcher(team.Name) ?? lineup.First();

        return new MatchTeamState(team.Name, team.Abbreviation, lineup, pitcher);
    }

    private List<MatchPlayerSnapshot> BuildSelectedTeamLineup()
    {
        var lineup = GetLineupPlayers()
            .OrderBy(player => player.LineupSlot)
            .Select(player => CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age))
            .ToList();

        var remainingPlayers = GetSelectedTeamRoster()
            .Where(player => lineup.All(existing => existing.Id != player.PlayerId) && player.PrimaryPosition is not "SP" and not "RP")
            .OrderBy(player => player.PlayerName)
            .ToList();

        foreach (var player in remainingPlayers)
        {
            if (lineup.Count >= 9)
            {
                break;
            }

            lineup.Add(CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age));
        }

        while (lineup.Count < 9)
        {
            lineup.Add(CreatePlaceholderSnapshot($"Bench Fill {lineup.Count + 1}", lineup.Count + 1));
        }

        return lineup;
    }

    private MatchPlayerSnapshot? BuildSelectedTeamPitcher()
    {
        var pitcher = GetRotationPlayers().OrderBy(player => player.RotationSlot).FirstOrDefault()
                     ?? GetSelectedTeamRoster().FirstOrDefault(player => player.PrimaryPosition is "SP" or "RP");

        return pitcher == null
            ? null
            : CreatePlayerSnapshot(pitcher.PlayerId, pitcher.PlayerName, pitcher.PrimaryPosition, pitcher.SecondaryPosition, pitcher.Age);
    }

    private List<MatchPlayerSnapshot> BuildImportedTeamLineup(string teamName)
    {
        var lineup = GetTeamRoster(teamName)
            .Where(player => player.LineupSlot.HasValue)
            .OrderBy(player => player.LineupSlot)
            .Select(player => CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age))
            .ToList();

        var fillPlayers = GetTeamRoster(teamName)
            .Where(player => lineup.All(existing => existing.Id != player.PlayerId) && player.PrimaryPosition is not "SP" and not "RP")
            .OrderBy(player => player.PlayerName)
            .ToList();

        foreach (var player in fillPlayers)
        {
            if (lineup.Count >= 9)
            {
                break;
            }

            lineup.Add(CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age));
        }

        while (lineup.Count < 9)
        {
            lineup.Add(CreatePlaceholderSnapshot($"{teamName} Fill {lineup.Count + 1}", lineup.Count + 1));
        }

        return lineup;
    }

    private MatchPlayerSnapshot? BuildImportedPitcher(string teamName)
    {
        var pitcher = GetTeamRoster(teamName)
            .OrderBy(player => player.RotationSlot ?? 99)
            .ThenBy(player => player.PrimaryPosition is "SP" ? 0 : 1)
            .FirstOrDefault(player => player.PrimaryPosition is "SP" or "RP");

        return pitcher == null
            ? null
            : CreatePlayerSnapshot(pitcher.PlayerId, pitcher.PlayerName, pitcher.PrimaryPosition, pitcher.SecondaryPosition, pitcher.Age);
    }

    private MatchPlayerSnapshot CreatePlayerSnapshot(Guid playerId, string name, string primaryPosition, string secondaryPosition, int age)
    {
        var ratings = GetPlayerRatings(playerId, name, primaryPosition, secondaryPosition, age);

        return new MatchPlayerSnapshot(
            playerId,
            name,
            primaryPosition,
            secondaryPosition,
            age,
            ratings.ContactRating,
            ratings.PowerRating,
            ratings.DisciplineRating,
            ratings.SpeedRating,
            ratings.PitchingRating,
            ratings.FieldingRating,
            ratings.ArmRating,
            ratings.DurabilityRating,
            ratings.OverallRating);
    }

    private MatchPlayerSnapshot CreatePlaceholderSnapshot(string name, int lineupSlot)
    {
        return CreatePlayerSnapshot(Guid.NewGuid(), name, lineupSlot % 2 == 0 ? "IF" : "OF", string.Empty, 27);
    }

    private List<MatchPlayerSnapshot> BuildPlaceholderLineup(string teamName)
    {
        return Enumerable.Range(1, 9)
            .Select(slot => CreatePlaceholderSnapshot($"{teamName} Batter {slot}", slot))
            .ToList();
    }

    private TeamImportDto? FindTeamByName(string teamName)
    {
        return _leagueData.Teams.FirstOrDefault(team => string.Equals(team.Name, teamName, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildScheduleGameKey(ScheduleImportDto game)
    {
        return $"{game.Date:yyyyMMdd}|{game.HomeTeamName}|{game.AwayTeamName}|{game.GameNumber ?? 1}";
    }

    public void FinalizeFranchiseScheduledGame(MatchState finalState)
    {
        FinalizeFranchiseScheduledGame(finalState, preferredGame: null);
    }

    private void FinalizeFranchiseScheduledGame(MatchState finalState, ScheduleImportDto? preferredGame)
    {
        if (SelectedTeam == null)
        {
            return;
        }

        var gameToMark = preferredGame;
        if (gameToMark == null)
        {
            gameToMark = GetNextScheduledGameForSelectedTeam();
        }

        if (gameToMark == null ||
            !string.Equals(gameToMark.AwayTeamName, finalState.AwayTeam.Name, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(gameToMark.HomeTeamName, finalState.HomeTeam.Name, StringComparison.OrdinalIgnoreCase))
        {
            gameToMark = _leagueData.Schedule
                .Where(game =>
                    !IsScheduledGameCompleted(game) &&
                    string.Equals(game.AwayTeamName, finalState.AwayTeam.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(game.HomeTeamName, finalState.HomeTeam.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(game => game.Date)
                .ThenBy(game => game.GameNumber)
                .FirstOrDefault();
        }

        if (gameToMark == null)
        {
            return;
        }

        var key = BuildScheduleGameKey(gameToMark);
        _saveState.CompletedScheduleGameKeys.Add(key);
        _saveState.CompletedScheduleGameResults[key] = new CompletedScheduleGameResult
        {
            AwayRuns = finalState.AwayTeam.Runs,
            HomeRuns = finalState.HomeTeam.Runs
        };

        AdvanceFranchiseDateToNextUnplayedGame();
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

    private static PlayerSeasonStatsState GenerateLastSeasonStats(Guid playerId, string primaryPosition, int age, PlayerHiddenRatingsState ratings)
    {
        var random = CreateStableRandom(playerId.ToString(), "last-season", primaryPosition, age.ToString());
        var isPitcher = primaryPosition is "SP" or "RP";

        if (isPitcher)
        {
            var gamesPitched = primaryPosition == "RP"
                ? random.Next(42, 71)
                : random.Next(24, 35);
            var inningsPitched = primaryPosition == "RP"
                ? random.Next(48, 86)
                : random.Next(128, 216);
            var era = Math.Clamp(MapRating(100 - ratings.PitchingRating, 5.60, 2.35) + ((random.NextDouble() - 0.5d) * 0.45d), 1.90d, 6.80d);
            var earnedRuns = Math.Max(0, (int)Math.Round((inningsPitched / 9d) * era));
            var hitsAllowed = Math.Max(0, (int)Math.Round(inningsPitched * MapRating(100 - ratings.PitchingRating, 1.20, 0.78)));
            var walksAllowed = Math.Max(0, (int)Math.Round(inningsPitched * MapRating(100 - ratings.DisciplineRating, 0.48, 0.18)));
            var pitcherStrikeouts = Math.Max(0, (int)Math.Round(inningsPitched * MapRating(ratings.PitchingRating, 0.68, 1.34)));
            var wins = primaryPosition == "RP"
                ? random.Next(2, 9)
                : Math.Max(4, (int)Math.Round(MapRating(ratings.PitchingRating, 4, 18)) + random.Next(-1, 2));
            var losses = primaryPosition == "RP"
                ? random.Next(1, 6)
                : Math.Max(3, (int)Math.Round(MapRating(100 - ratings.PitchingRating, 4, 12)) + random.Next(-1, 2));

            return new PlayerSeasonStatsState
            {
                GamesPlayed = gamesPitched,
                GamesPitched = gamesPitched,
                InningsPitchedOuts = inningsPitched * 3,
                HitsAllowed = hitsAllowed,
                RunsAllowed = earnedRuns + Math.Max(0, random.Next(0, 7)),
                EarnedRuns = earnedRuns,
                WalksAllowed = walksAllowed,
                StrikeoutsPitched = pitcherStrikeouts,
                Wins = wins,
                Losses = losses
            };
        }

        var gamesPlayed = random.Next(96, 156);
        var plateAppearances = gamesPlayed * random.Next(3, 6);
        var walks = Math.Max(8, (int)Math.Round(plateAppearances * MapRating(ratings.DisciplineRating, 0.04, 0.14)));
        var atBats = Math.Max(1, plateAppearances - walks);
        var battingAverage = Math.Clamp(MapRating(ratings.ContactRating, 0.195, 0.338) + ((random.NextDouble() - 0.5d) * 0.020d), 0.180d, 0.360d);
        var hits = Math.Max(0, (int)Math.Round(atBats * battingAverage));
        var homeRuns = Math.Clamp((int)Math.Round(atBats * MapRating(ratings.PowerRating, 0.012, 0.085)), 0, hits);
        var doubles = Math.Clamp((int)Math.Round(hits * MapRating(ratings.PowerRating, 0.14, 0.28)), 0, Math.Max(0, hits - homeRuns));
        var triples = Math.Clamp((int)Math.Round(hits * MapRating(ratings.SpeedRating, 0.01, 0.05)), 0, Math.Max(0, hits - homeRuns - doubles));
        var strikeouts = Math.Clamp((int)Math.Round(atBats * MapRating(100 - ratings.ContactRating, 0.10, 0.30)), 0, atBats);
        var runsBattedIn = Math.Max(homeRuns, (int)Math.Round((hits + homeRuns) * MapRating((ratings.ContactRating + ratings.PowerRating) / 2d, 0.42, 0.88)));

        return new PlayerSeasonStatsState
        {
            GamesPlayed = gamesPlayed,
            PlateAppearances = plateAppearances,
            AtBats = atBats,
            Hits = hits,
            Doubles = doubles,
            Triples = triples,
            HomeRuns = homeRuns,
            RunsBattedIn = runsBattedIn,
            Walks = walks,
            Strikeouts = strikeouts
        };
    }

    private static double MapRating(double rating, double lowValue, double highValue)
    {
        var normalized = Math.Clamp((rating - 1d) / 98d, 0d, 1d);
        return lowValue + ((highValue - lowValue) * normalized);
    }

    private void Save()
    {
        _stateStore.Save(_saveState);
    }

    private sealed record AttributeInsight(string Key, int Rating);
}
