using BaseballManager.Application.Transactions;
using BaseballManager.Contracts.ImportDtos;
using BaseballManager.Core.Economy;
using BaseballManager.Sim.Economy;
using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Results;

namespace BaseballManager.Game.Data;

public sealed class FranchiseSession
{
    private enum PracticeDevelopmentAttribute
    {
        Contact,
        Power,
        Discipline,
        Speed,
        Fielding,
        Arm,
        Pitching,
        Stamina,
        Durability
    }

    private static readonly string[] CoachRoleOrder = ["Manager", "Hitting Coach", "Pitching Coach", "Bench Coach", "Scouting Director", "Team Doctor", "Physiologist"];
    private static readonly string[] CoachFirstNames = ["Alex", "Jordan", "Sam", "Casey", "Drew", "Taylor", "Riley", "Morgan", "Cameron", "Jamie", "Hayden", "Avery"];
    private static readonly string[] CoachLastNames = ["Maddox", "Sullivan", "Torres", "Bennett", "Foster", "Callahan", "Diaz", "Reed", "Hughes", "Alvarez", "Parker", "Watts"];
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseStateStore _stateStore;
    private readonly FranchiseSaveState _saveState;
    private readonly ProcessGameRevenueUseCase _processGameRevenueUseCase = new();
    private readonly ProcessMonthlyFinanceUseCase _processMonthlyFinanceUseCase = new();
    private readonly SetBudgetAllocationUseCase _setBudgetAllocationUseCase = new();
    private readonly SignPlayerContractUseCase _signPlayerContractUseCase = new();
    private readonly ReleasePlayerUseCase _releasePlayerUseCase = new();
    private readonly HireCoachUseCase _hireCoachUseCase = new();
    private readonly Dictionary<Guid, PlayerSeasonStatsState> _lastSeasonStatsCache = new();

    public FranchiseSession(ImportedLeagueData leagueData, FranchiseStateStore stateStore)
    {
        _leagueData = leagueData;
        _stateStore = stateStore;
        _saveState = _stateStore.Load();
        _saveState.PlayerRatings ??= new Dictionary<Guid, PlayerHiddenRatingsState>();
        _saveState.PlayerSeasonStats ??= new Dictionary<Guid, PlayerSeasonStatsState>();
        _saveState.PlayerRecentGameStats ??= new Dictionary<Guid, List<PlayerRecentGameStatState>>();
        _saveState.PlayerRecentTrackingTotals ??= new Dictionary<Guid, PlayerRecentTotalsState>();
        _saveState.PlayerHealth ??= new Dictionary<Guid, PlayerHealthState>();
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

        if (EnsurePlayerRatingsGenerated() || EnsurePlayerSeasonStatsGenerated() || EnsureRecentGameTrackingGenerated() || EnsurePlayerHealthGenerated() || EnsurePlayerAssignmentsGenerated() || EnsureCoachingStaffGenerated() || EnsureTeamEconomyGenerated())
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

    public TeamPracticeFocus GetPracticeFocus(DateTime? date = null)
    {
        return SelectedTeam == null
            ? TeamPracticeFocus.Balanced
            : GetPracticeFocus(SelectedTeam.Name, date);
    }

    private TeamPracticeFocus GetPracticeFocus(string teamName, DateTime? date = null)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return TeamPracticeFocus.Balanced;
        }

        var teamState = GetOrCreateTeamState(teamName);
        if (date.HasValue && teamState.PracticeFocusOverrides.TryGetValue(BuildPracticeDateKey(date.Value), out var dateSpecificFocus))
        {
            return dateSpecificFocus;
        }

        return teamState.PracticeFocus;
    }

    public bool HasCustomPracticeFocus(DateTime date)
    {
        return SelectedTeam != null && HasCustomPracticeFocus(SelectedTeam.Name, date);
    }

    private bool HasCustomPracticeFocus(string teamName, DateTime date)
    {
        return !string.IsNullOrWhiteSpace(teamName) &&
               GetOrCreateTeamState(teamName).PracticeFocusOverrides.ContainsKey(BuildPracticeDateKey(date));
    }

    public string CyclePracticeFocus(int direction, DateTime? targetDate = null)
    {
        if (SelectedTeam == null)
        {
            return "Select a franchise team before changing the practice plan.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        var focusOptions = Enum.GetValues<TeamPracticeFocus>();
        var currentFocus = targetDate.HasValue ? GetPracticeFocus(targetDate.Value) : teamState.PracticeFocus;
        var currentIndex = Array.IndexOf(focusOptions, currentFocus);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + direction % focusOptions.Length + focusOptions.Length) % focusOptions.Length;
        var nextFocus = focusOptions[nextIndex];

        if (targetDate.HasValue)
        {
            var practiceDateKey = BuildPracticeDateKey(targetDate.Value);
            if (nextFocus == teamState.PracticeFocus)
            {
                teamState.PracticeFocusOverrides.Remove(practiceDateKey);
            }
            else
            {
                teamState.PracticeFocusOverrides[practiceDateKey] = nextFocus;
            }

            Save();
            return $"Practice plan for {targetDate.Value:ddd, MMM d} set to {GetPracticeFocusLabel(nextFocus)}.";
        }

        teamState.PracticeFocus = nextFocus;
        foreach (var practiceDateKey in teamState.PracticeFocusOverrides.Where(entry => entry.Value == nextFocus).Select(entry => entry.Key).ToList())
        {
            teamState.PracticeFocusOverrides.Remove(practiceDateKey);
        }

        Save();
        return $"Default practice focus set to {GetPracticeFocusLabel(teamState.PracticeFocus)}.";
    }

    public static string GetPracticeFocusLabel(TeamPracticeFocus focus)
    {
        return focus switch
        {
            TeamPracticeFocus.Hitting => "Hitting",
            TeamPracticeFocus.Pitching => "Pitching",
            TeamPracticeFocus.Defense => "Defense",
            TeamPracticeFocus.Baserunning => "Baserunning",
            TeamPracticeFocus.Recovery => "Recovery",
            _ => "Balanced"
        };
    }

    public static string GetPracticeFocusDescription(TeamPracticeFocus focus, bool lightWorkout)
    {
        return focus switch
        {
            TeamPracticeFocus.Hitting => lightWorkout
                ? "Short cage work, timing drills, and situational swings before the next series."
                : "Extended batting practice, situational hitting, and lineup timing work for the whole club.",
            TeamPracticeFocus.Pitching => lightWorkout
                ? "Bullpen touch work, command check-ins, and scouting prep for the staff."
                : "Bullpens, pitch-design work, and command sessions for starters and relievers.",
            TeamPracticeFocus.Defense => lightWorkout
                ? "Crisp infield reps, cutoff reminders, and glove-work refreshers."
                : "Full defensive workout with relay drills, positioning reps, and team fundamentals.",
            TeamPracticeFocus.Baserunning => lightWorkout
                ? "Lead timing, jump reads, and first-step acceleration work."
                : "Aggressive baserunning practice focused on reads, secondary leads, and taking extra bases.",
            TeamPracticeFocus.Recovery => "Mobility, treatment, film review, and a lighter maintenance workload.",
            _ => lightWorkout
                ? "A short all-around tune-up with cage work, defense, and bullpen maintenance."
                : "A balanced full-team workout covering hitting, defense, bullpen work, and conditioning."
        };
    }

    public DateTime GetCurrentFranchiseDate()
    {
        EnsureFranchiseDateInitialized();
        return _saveState.CurrentFranchiseDate.Date;
    }

    public DateTime GetSeasonCalendarStartDate()
    {
        var seasonYear = GetSeasonScheduleYear();
        return new DateTime(seasonYear, 2, 1);
    }

    public DateTime GetSpringTrainingStartDate()
    {
        var seasonYear = GetSeasonScheduleYear();
        return new DateTime(seasonYear, 3, 1);
    }

    public DateTime GetSeasonCalendarEndDate()
    {
        var seasonSchedule = GetSeasonSchedule();
        if (seasonSchedule.Count == 0)
        {
            return new DateTime(GetSeasonScheduleYear(), 10, 31);
        }

        return seasonSchedule[^1].Date.Date;
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
            if (ratings.StaminaRating <= 0)
            {
                ratings.StaminaRating = PlayerRatingsGenerator.Generate(playerId, fullName, primaryPosition, secondaryPosition, age).StaminaRating;
                Save();
            }

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

    public PlayerHealthState GetPlayerHealth(Guid playerId)
    {
        if (_saveState.PlayerHealth.TryGetValue(playerId, out var health))
        {
            return health;
        }

        health = new PlayerHealthState();
        _saveState.PlayerHealth[playerId] = health;
        return health;
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

    public IReadOnlyList<MedicalPlayerStatus> GetMedicalRiskBoard(int maxCount = 8)
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        return GetSelectedTeamRoster()
            .Select(player => BuildMedicalPlayerStatus(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age))
            .Where(status => status.IsInjured || status.DaysUntilAvailable > 0 || status.Fatigue >= 25)
            .OrderByDescending(status => status.IsInjured)
            .ThenByDescending(status => status.DaysUntilAvailable)
            .ThenByDescending(status => status.Fatigue)
            .ThenBy(status => status.PlayerName)
            .Take(Math.Max(1, maxCount))
            .ToList();
    }

    public string GetPlayerMedicalReport(Guid playerId, string playerName, string primaryPosition, string secondaryPosition, int age)
    {
        return BuildMedicalPlayerStatus(playerId, playerName, primaryPosition, secondaryPosition, age).Report;
    }

    public string GetPlayerMedicalStatus(Guid playerId, string playerName, string primaryPosition, string secondaryPosition, int age)
    {
        return BuildMedicalPlayerStatus(playerId, playerName, primaryPosition, secondaryPosition, age).Status;
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

    public IReadOnlyList<CoachProfileView> GetCoachCandidates(string role)
    {
        if (SelectedTeam == null || string.IsNullOrWhiteSpace(role))
        {
            return [];
        }

        return BuildCoachCandidatePool(SelectedTeam.Name, role)
            .Select(candidate => new CoachProfileView(candidate.Role, candidate.Name, candidate.Specialty, candidate.Voice))
            .ToList();
    }

    public bool AssignCoach(string role, string coachName, string specialty, string voice, out string statusMessage)
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

        var replacement = BuildCoachCandidatePool(SelectedTeam.Name, role).FirstOrDefault(candidate =>
            string.Equals(candidate.Name, coachName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Specialty, specialty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Voice, voice, StringComparison.OrdinalIgnoreCase));

        if (replacement == null)
        {
            statusMessage = "That coach is not available for this role right now.";
            return false;
        }

        coach.Name = replacement.Name;
        coach.Specialty = replacement.Specialty;
        coach.Voice = replacement.Voice;
        _hireCoachUseCase.Execute(teamState.Economy, Guid.NewGuid(), coach.Name, coach.Role, EstimateCoachSalary(coach.Role, SelectedTeam.Name), GetCurrentFranchiseDate());
        Save();

        statusMessage = $"{coach.Name} is now your {coach.Role.ToLowerInvariant()}.";
        return true;
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
        return AssignCoach(role, replacement.Name, replacement.Specialty, replacement.Voice, out statusMessage);
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
        var ratings = GetScoutedRatings(player, coach.Role);
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
        MovePlayerContract(targetPlayerId, targetTeamState.Economy, selectedTeamState.Economy, targetPlayer.FullName);
        MovePlayerContract(offeredPlayerId, selectedTeamState.Economy, targetTeamState.Economy, offeredPlayer.FullName);
        ApplyTradeFanInterestShift(selectedTeamState.Economy, targetPlayer, offeredPlayer);
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

    public int GetCurrentTrainingReportSeason()
    {
        if (SelectedTeam == null)
        {
            return GetCurrentFranchiseDate().Year;
        }

        var teamSchedule = _leagueData.Schedule
            .Where(game =>
                string.Equals(game.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.AwayTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(game => game.Date)
            .ToList();

        if (teamSchedule.Count == 0)
        {
            return GetCurrentFranchiseDate().Year;
        }

        var nextUnplayed = teamSchedule.FirstOrDefault(game => !IsScheduledGameCompleted(game));
        return (nextUnplayed?.Date ?? teamSchedule[^1].Date).Year;
    }

    public IReadOnlyList<TrainingReportView> GetTrainingReportsForCurrentSeason()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        ClearTrainingReportsIfSeasonComplete();
        var seasonYear = GetCurrentTrainingReportSeason();
        return GetOrCreateTeamState(SelectedTeam.Name).TrainingReports
            .Where(report => report.SeasonYear == seasonYear)
            .OrderByDescending(report => report.ReportDate)
            .Select(report => new TrainingReportView(
                report.ReportDate,
                report.SeasonYear,
                report.Title,
                report.FocusLabel,
                report.Summary,
                report.CoachNotes))
            .ToList();
    }

    public TeamEconomy GetSelectedTeamEconomy()
    {
        return SelectedTeam == null
            ? new TeamEconomy()
            : GetOrCreateTeamState(SelectedTeam.Name).Economy;
    }

    public IReadOnlyList<FinancialSnapshot> GetRecentFinancialSnapshots(int maxCount = 8)
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        return GetOrCreateTeamState(SelectedTeam.Name).Economy.FinancialHistory
            .OrderByDescending(snapshot => snapshot.EffectiveDate)
            .Take(Math.Max(1, maxCount))
            .ToList();
    }

    public IReadOnlyList<TeamStandingView> GetStandings()
    {
        return _leagueData.Teams
            .Select(team =>
            {
                var summary = GetTeamRecordSummary(team.Name);
                return new TeamStandingView(
                    team.Name,
                    team.Abbreviation,
                    team.League,
                    team.Division,
                    summary.Wins,
                    summary.Losses,
                    summary.Streak);
            })
            .OrderBy(team => team.League)
            .ThenBy(team => team.Division)
            .ThenByDescending(team => team.Wins)
            .ThenBy(team => team.Losses)
            .ThenBy(team => team.TeamName)
            .ToList();
    }

    public string AdjustBudgetAllocation(string budgetKey, int direction)
    {
        if (SelectedTeam == null)
        {
            return "Select a team before changing the budget.";
        }

        var economy = GetOrCreateTeamState(SelectedTeam.Name).Economy;
        var updatedAllocation = new BudgetAllocation
        {
            ScoutingBudget = economy.BudgetAllocation.ScoutingBudget,
            PlayerDevelopmentBudget = economy.BudgetAllocation.PlayerDevelopmentBudget,
            MedicalBudget = economy.BudgetAllocation.MedicalBudget,
            FacilitiesBudget = economy.BudgetAllocation.FacilitiesBudget
        };

        const decimal step = 50_000m;
        switch (budgetKey.ToLowerInvariant())
        {
            case "scouting":
                updatedAllocation.ScoutingBudget += direction * step;
                break;
            case "development":
                updatedAllocation.PlayerDevelopmentBudget += direction * step;
                break;
            case "medical":
                updatedAllocation.MedicalBudget += direction * step;
                break;
            case "facilities":
                updatedAllocation.FacilitiesBudget += direction * step;
                break;
            default:
                return "That budget line is not available right now.";
        }

        _setBudgetAllocationUseCase.Execute(economy, updatedAllocation);
        Save();
        return $"{budgetKey} budget adjusted to {GetBudgetDisplay(GetBudgetValue(economy.BudgetAllocation, budgetKey))} per month.";
    }

    public RecentPlayerStatsView GetRecentPlayerStats(Guid playerId, int maxGames = 10)
    {
        if (!_saveState.PlayerRecentGameStats.TryGetValue(playerId, out var gameLogs) || gameLogs.Count == 0)
        {
            return new RecentPlayerStatsView(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var sample = gameLogs
            .OrderByDescending(log => log.GameDate)
            .Take(Math.Max(1, maxGames))
            .ToList();

        return new RecentPlayerStatsView(
            sample.Count,
            sample.Sum(log => log.GamesPlayed),
            sample.Sum(log => log.InningsPitchedOuts),
            sample.Sum(log => log.EarnedRuns),
            sample.Sum(log => log.Wins),
            sample.Sum(log => log.Losses),
            sample.Sum(log => log.AtBats),
            sample.Sum(log => log.Hits),
            sample.Sum(log => log.Doubles),
            sample.Sum(log => log.Triples),
            sample.Sum(log => log.HomeRuns),
            sample.Sum(log => log.Walks),
            sample.Sum(log => log.Strikeouts),
            sample.Sum(log => log.PitcherStrikeouts));
    }

    private List<ScheduleImportDto> GetRemainingScheduledGamesForDate(DateTime date)
    {
        return _leagueData.Schedule
            .Where(game =>
                !IsScheduledGameCompleted(game) &&
                game.Date.Date == date.Date)
            .OrderBy(game => game.GameNumber ?? 1)
            .ThenBy(game => game.HomeTeamName)
            .ToList();
    }

    public bool SimulateCurrentDay(out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before simulating the day.";
            return false;
        }

        var currentDate = GetCurrentFranchiseDate().Date;
        var todaysGames = GetRemainingScheduledGamesForDate(currentDate);
        var gameSummaries = new List<string>();

        foreach (var game in todaysGames)
        {
            if (!TrySimulateScheduledGame(game, advanceDateAfterGame: false, out var gameSummary))
            {
                statusMessage = gameSummary;
                return false;
            }

            if (string.Equals(game.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.AwayTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
            {
                gameSummaries.Add(gameSummary);
            }
        }

        var hasPractice = TryGetPracticeSessionInfo(currentDate, out var practiceSession);
        var developmentResults = ApplyPracticeDevelopmentAcrossLeague(currentDate, SelectedTeam.Name);

        AdvanceFranchiseDateTo(currentDate.AddDays(1));
        ClearLiveMatchState(LiveMatchMode.Franchise);
        Save();

        if (gameSummaries.Count > 0)
        {
            var otherGameCount = Math.Max(0, todaysGames.Count - gameSummaries.Count);
            statusMessage = otherGameCount > 0
                ? $"{currentDate:ddd, MMM d}: {string.Join(" ", gameSummaries)} Around the league, {otherGameCount} other game(s) were played."
                : $"{currentDate:ddd, MMM d}: {string.Join(" ", gameSummaries)}";
            return true;
        }

        if (hasPractice)
        {
            var practiceSummary = BuildPracticeDevelopmentReport(currentDate, practiceSession, developmentResults);
            statusMessage = todaysGames.Count > 0
                ? $"{practiceSummary} Around the league, {todaysGames.Count} game(s) were played."
                : practiceSummary;
            return true;
        }

        statusMessage = todaysGames.Count > 0
            ? $"Advanced through {currentDate:ddd, MMM d}. {todaysGames.Count} league game(s) were played."
            : $"Advanced through {currentDate:ddd, MMM d}. No game or full-team workout was on the calendar.";
        return true;
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

        if (!TrySimulateScheduledGame(nextGame, advanceDateAfterGame: true, out var gameSummary))
        {
            statusMessage = gameSummary;
            return false;
        }

        ClearLiveMatchState(LiveMatchMode.Franchise);
        Save();
        statusMessage = $"Simulated {gameSummary} on {nextGame.Date:yyyy-MM-dd}.";
        return true;
    }

    private bool TrySimulateScheduledGame(ScheduleImportDto scheduledGame, bool advanceDateAfterGame, out string summary)
    {
        var awayTeam = FindTeamByName(scheduledGame.AwayTeamName);
        var homeTeam = FindTeamByName(scheduledGame.HomeTeamName);
        if (awayTeam == null || homeTeam == null)
        {
            summary = "Could not resolve teams for the scheduled game.";
            return false;
        }

        var useFranchiseSelectionsForAway = SelectedTeam != null && string.Equals(SelectedTeam.Name, awayTeam.Name, StringComparison.OrdinalIgnoreCase);
        var useFranchiseSelectionsForHome = SelectedTeam != null && string.Equals(SelectedTeam.Name, homeTeam.Name, StringComparison.OrdinalIgnoreCase);
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
        FinalizeFranchiseScheduledGame(engine.CurrentState, scheduledGame, advanceDateAfterGame);
        summary = $"{awayTeam.Abbreviation} {engine.CurrentState.AwayTeam.Runs} - {engine.CurrentState.HomeTeam.Runs} {homeTeam.Abbreviation}.";
        return true;
    }

    public void ApplyPerformanceDevelopment(MatchPlayerSnapshot batter, MatchPlayerSnapshot pitcher, MatchTeamState defensiveTeam, ResultEvent result)
    {
        TrackPitcherUsage(pitcher);

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
        RecordRecentGameStats(finalState);
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

    public FranchiseRosterEntry? GetScheduledStartingPitcher(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return null;
        }

        return SelectStartingPitcher(GetTeamRoster(teamName), teamName);
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
        _saveState.PlayerRecentGameStats.Clear();
        _saveState.PlayerRecentTrackingTotals.Clear();
        _saveState.PlayerHealth.Clear();
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

        teamState.PracticeFocusOverrides ??= new Dictionary<string, TeamPracticeFocus>(StringComparer.OrdinalIgnoreCase);
        teamState.TrainingReports ??= new List<TrainingReportState>();
        teamState.Economy ??= BuildDefaultTeamEconomy(teamName);
        EnsureTeamEconomyInitialized(teamState, teamName);
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

    private bool EnsurePlayerHealthGenerated()
    {
        var hasChanges = false;

        foreach (var player in _leagueData.Players)
        {
            if (_saveState.PlayerHealth.ContainsKey(player.PlayerId))
            {
                continue;
            }

            _saveState.PlayerHealth[player.PlayerId] = new PlayerHealthState();
            hasChanges = true;
        }

        return hasChanges;
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

    private bool EnsureTeamEconomyGenerated()
    {
        var hasChanges = false;
        foreach (var team in _leagueData.Teams)
        {
            hasChanges |= EnsureTeamEconomyInitialized(GetOrCreateTeamState(team.Name), team.Name);
        }

        return hasChanges;
    }

    private bool EnsureTeamEconomyInitialized(TeamFranchiseState teamState, string teamName)
    {
        var hasChanges = false;
        if (teamState.Economy == null)
        {
            teamState.Economy = BuildDefaultTeamEconomy(teamName);
            hasChanges = true;
        }

        teamState.Economy.BudgetAllocation ??= BuildDefaultBudgetAllocation(teamState.Economy.MarketSize);
        teamState.Economy.PlayerContracts ??= [];
        teamState.Economy.CoachContracts ??= [];
        teamState.Economy.FinancialHistory ??= [];

        EnsureCoachingStaffInitialized(teamState, teamName);
        hasChanges |= EnsurePlayerContractsInitialized(teamState.Economy, teamName);
        hasChanges |= EnsureCoachContractsInitialized(teamState.Economy, teamState.CoachingStaff, teamName);

        teamState.Economy.FanInterest = Math.Clamp(teamState.Economy.FanInterest, 10, 100);
        teamState.Economy.MerchStrength = Math.Clamp(teamState.Economy.MerchStrength, 20, 100);
        teamState.Economy.SponsorStrength = Math.Clamp(teamState.Economy.SponsorStrength, 20, 100);
        teamState.Economy.FacilitiesLevel = Math.Clamp(teamState.Economy.FacilitiesLevel, 1, 5);
        teamState.Economy.TicketPrice = Math.Clamp(teamState.Economy.TicketPrice, 12m, 80m);
        teamState.Economy.StadiumCapacity = Math.Clamp(teamState.Economy.StadiumCapacity, 20_000, 55_000);
        teamState.Economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(teamState.Economy);
        return hasChanges;
    }

    private TeamEconomy BuildDefaultTeamEconomy(string teamName)
    {
        var team = FindTeamByName(teamName);
        var marketSize = DetermineMarketSize(team);
        var random = CreateStableRandom(teamName, "economy");
        var economy = new TeamEconomy
        {
            CashOnHand = marketSize switch
            {
                MarketSize.Small => 16_000_000m + (random.Next(0, 7) * 350_000m),
                MarketSize.Large => 32_000_000m + (random.Next(0, 10) * 500_000m),
                _ => 23_000_000m + (random.Next(0, 8) * 400_000m)
            },
            MarketSize = marketSize,
            FanInterest = Math.Clamp(46 + random.Next(-6, 9), 25, 80),
            TicketPrice = marketSize switch
            {
                MarketSize.Small => 22m + random.Next(0, 5),
                MarketSize.Large => 32m + random.Next(0, 7),
                _ => 27m + random.Next(0, 6)
            },
            StadiumCapacity = marketSize switch
            {
                MarketSize.Small => 28_000 + random.Next(0, 4_000),
                MarketSize.Large => 40_000 + random.Next(0, 6_000),
                _ => 34_000 + random.Next(0, 5_000)
            },
            MerchStrength = Math.Clamp(44 + random.Next(-4, 16), 25, 90),
            SponsorStrength = Math.Clamp(45 + random.Next(-4, 18), 25, 92),
            FacilitiesLevel = Math.Clamp(2 + random.Next(0, 3), 1, 5),
            BudgetAllocation = BuildDefaultBudgetAllocation(marketSize)
        };

        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
        return economy;
    }

    private static BudgetAllocation BuildDefaultBudgetAllocation(MarketSize marketSize)
    {
        return marketSize switch
        {
            MarketSize.Small => new BudgetAllocation
            {
                ScoutingBudget = 180_000m,
                PlayerDevelopmentBudget = 220_000m,
                MedicalBudget = 175_000m,
                FacilitiesBudget = 125_000m
            },
            MarketSize.Large => new BudgetAllocation
            {
                ScoutingBudget = 325_000m,
                PlayerDevelopmentBudget = 400_000m,
                MedicalBudget = 280_000m,
                FacilitiesBudget = 225_000m
            },
            _ => new BudgetAllocation
            {
                ScoutingBudget = 240_000m,
                PlayerDevelopmentBudget = 300_000m,
                MedicalBudget = 220_000m,
                FacilitiesBudget = 165_000m
            }
        };
    }

    private bool EnsurePlayerContractsInitialized(TeamEconomy economy, string teamName)
    {
        var hasChanges = false;
        var assignedPlayers = _leagueData.Players
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var assignedPlayerIds = assignedPlayers.Select(player => player.PlayerId).ToHashSet();

        var removedContracts = economy.PlayerContracts.RemoveAll(contract => !assignedPlayerIds.Contains(contract.SubjectId));
        hasChanges |= removedContracts > 0;

        foreach (var player in assignedPlayers)
        {
            if (economy.PlayerContracts.Any(contract => contract.SubjectId == player.PlayerId))
            {
                continue;
            }

            var salary = EstimatePlayerSalary(player);
            var years = player.Age switch
            {
                <= 24 => 3,
                <= 29 => 4,
                <= 33 => 3,
                _ => 2
            };
            _signPlayerContractUseCase.Execute(economy, player.PlayerId, player.FullName, salary, years, GetCurrentFranchiseDate());
            hasChanges = true;
        }

        return hasChanges;
    }

    private bool EnsureCoachContractsInitialized(TeamEconomy economy, IReadOnlyList<CoachAssignmentState> coaches, string teamName)
    {
        var hasChanges = false;
        var activeRoles = coaches.Select(coach => coach.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removedContracts = economy.CoachContracts.RemoveAll(contract => !activeRoles.Contains(contract.Role));
        hasChanges |= removedContracts > 0;

        foreach (var coach in coaches)
        {
            if (economy.CoachContracts.Any(contract => string.Equals(contract.Role, coach.Role, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _hireCoachUseCase.Execute(economy, Guid.NewGuid(), coach.Name, coach.Role, EstimateCoachSalary(coach.Role, teamName), GetCurrentFranchiseDate());
            hasChanges = true;
        }

        return hasChanges;
    }

    private decimal EstimatePlayerSalary(PlayerImportDto player)
    {
        var ratings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var random = CreateStableRandom(player.PlayerId.ToString(), "salary");
        var salary = 650_000m + (Math.Max(0, ratings.OverallRating - 45) * 170_000m);
        salary *= player.Age switch
        {
            <= 24 => 0.78m,
            >= 33 => 1.12m,
            _ => 1.00m
        };

        salary *= 0.92m + (random.Next(0, 17) / 100m);
        return decimal.Round(Math.Clamp(salary, 650_000m, 28_000_000m), 2);
    }

    private static decimal EstimateCoachSalary(string role, string teamName)
    {
        var baseSalary = role switch
        {
            "Manager" => 2_200_000m,
            "Scouting Director" => 1_100_000m,
            "Hitting Coach" or "Pitching Coach" => 850_000m,
            "Team Doctor" => 950_000m,
            _ => 650_000m
        };

        return decimal.Round(baseSalary + ((GetStableHash($"{teamName}|{role}|coach-salary") % 7) * 35_000m), 2);
    }

    private static MarketSize DetermineMarketSize(TeamImportDto? team)
    {
        var city = team?.City ?? string.Empty;
        if (city is "New York" or "Boston" or "Seattle" or "Toronto" or "San Francisco" or "Washington")
        {
            return MarketSize.Large;
        }

        if (city is "Nashville" or "Portland" or "St. Louis" or "Cleveland" or "Pittsburgh" or "Tampa")
        {
            return MarketSize.Small;
        }

        return MarketSize.Medium;
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

        for (var i = 0; i < 12 && candidates.Count < 8; i++)
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

    private PlayerHiddenRatingsState GetScoutedRatings(PlayerImportDto player, string coachRole)
    {
        var actualRatings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        if (SelectedTeam == null)
        {
            return actualRatings;
        }

        var economy = GetOrCreateTeamState(SelectedTeam.Name).Economy;
        var variance = FranchiseEconomyEffects.GetScoutingVariance(economy.BudgetAllocation.ScoutingBudget);
        if (string.Equals(coachRole, "Scouting Director", StringComparison.OrdinalIgnoreCase))
        {
            variance = Math.Max(1, variance - 1);
        }

        if (variance <= 1)
        {
            return actualRatings;
        }

        var random = CreateStableRandom(player.PlayerId.ToString(), coachRole, "scouting-budget", economy.BudgetAllocation.ScoutingBudget.ToString("0"));
        var scoutedRatings = new PlayerHiddenRatingsState
        {
            ContactRating = ApplyScoutingVariance(actualRatings.EffectiveContactRating, variance, random),
            PowerRating = ApplyScoutingVariance(actualRatings.EffectivePowerRating, variance, random),
            DisciplineRating = ApplyScoutingVariance(actualRatings.EffectiveDisciplineRating, variance, random),
            SpeedRating = ApplyScoutingVariance(actualRatings.EffectiveSpeedRating, variance, random),
            FieldingRating = ApplyScoutingVariance(actualRatings.EffectiveFieldingRating, variance, random),
            ArmRating = ApplyScoutingVariance(actualRatings.EffectiveArmRating, variance, random),
            PitchingRating = ApplyScoutingVariance(actualRatings.EffectivePitchingRating, variance, random),
            StaminaRating = ApplyScoutingVariance(actualRatings.EffectiveStaminaRating, variance, random),
            DurabilityRating = ApplyScoutingVariance(actualRatings.EffectiveDurabilityRating, variance, random)
        };
        scoutedRatings.RecalculateDerivedRatings();
        return scoutedRatings;
    }

    private static int ApplyScoutingVariance(int rating, int variance, Random random)
    {
        return Math.Clamp(rating + random.Next(-variance, variance + 1), 1, 99);
    }

    private static IReadOnlyList<AttributeInsight> GetAttributeInsights(PlayerHiddenRatingsState ratings)
    {
        return
        [
            new AttributeInsight("contact", ratings.EffectiveContactRating),
            new AttributeInsight("power", ratings.EffectivePowerRating),
            new AttributeInsight("discipline", ratings.EffectiveDisciplineRating),
            new AttributeInsight("speed", ratings.EffectiveSpeedRating),
            new AttributeInsight("fielding", ratings.EffectiveFieldingRating),
            new AttributeInsight("arm", ratings.EffectiveArmRating),
            new AttributeInsight("pitching", ratings.EffectivePitchingRating),
            new AttributeInsight("stamina", ratings.EffectiveStaminaRating),
            new AttributeInsight("durability", ratings.EffectiveDurabilityRating)
        ];
    }

    private static string[] GetFocusKeysForCoach(string coachRole, bool isPitcher)
    {
        return coachRole switch
        {
            "Hitting Coach" when isPitcher => ["pitching", "stamina", "durability", "arm"],
            "Hitting Coach" => ["contact", "power", "discipline", "speed"],
            "Pitching Coach" when isPitcher => ["pitching", "stamina", "durability", "arm"],
            "Pitching Coach" => ["arm", "fielding", "speed", "durability"],
            "Bench Coach" when isPitcher => ["stamina", "durability", "arm", "discipline"],
            "Bench Coach" => ["fielding", "speed", "arm", "discipline"],
            "Manager" when isPitcher => ["pitching", "stamina", "durability", "discipline"],
            "Manager" => ["contact", "discipline", "durability", "fielding"],
            "Team Doctor" when isPitcher => ["durability", "stamina", "pitching", "discipline"],
            "Team Doctor" => ["durability", "speed", "fielding", "discipline"],
            "Physiologist" when isPitcher => ["stamina", "durability", "arm", "pitching"],
            "Physiologist" => ["speed", "durability", "fielding", "contact"],
            _ when isPitcher => ["pitching", "stamina", "durability", "arm"],
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
            "stamina" => attribute.Rating >= 70
                ? Pick(random, ["he looks built to carry a heavy workload", "the tank should hold up deep into games", "his recovery profile looks strong for regular use"])
                : Pick(random, ["he can get through a reasonable workload", "there is enough gas there for steady use", "the stamina should be manageable with normal rest"]),
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
            "stamina" => Pick(random, ["the tank may run a little light under a starter's workload", "I would watch how long he holds his stuff", "he may need careful workload management once the pitch count climbs"]),
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
            "Team Doctor" => ["injury prevention", "return-to-play timing", "soft-tissue care", "arm-health monitoring"],
            "Physiologist" => ["recovery planning", "workload balance", "mobility work", "energy management"],
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
            "Team Doctor" => ["clinical", "calm", "protective"],
            "Physiologist" => ["measured", "recovery-focused", "upbeat"],
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
        ratings.StaminaRating = Math.Clamp(ratings.StaminaRating, 1, 99);
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
            if (player.Id != team.StartingPitcher.Id)
            {
                ApplyPositionPlayerGameFatigue(player);
            }
        }

        ApplyPitcherGameFatigue(team.StartingPitcher, team.PitchCount);

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

    private void RecordRecentGameStats(MatchState finalState)
    {
        foreach (var player in finalState.AwayTeam.Lineup.Append(finalState.AwayTeam.StartingPitcher)
                     .Concat(finalState.HomeTeam.Lineup.Append(finalState.HomeTeam.StartingPitcher))
                     .GroupBy(player => player.Id)
                     .Select(group => group.First()))
        {
            var currentStats = GetPlayerSeasonStats(player.Id);
            var previousTotals = GetTrackedRecentTotals(player.Id);
            var recentGameLine = new PlayerRecentGameStatState
            {
                GameDate = GetCurrentFranchiseDate().Date,
                GamesPlayed = Math.Max(0, currentStats.GamesPlayed - previousTotals.GamesPlayed),
                InningsPitchedOuts = Math.Max(0, currentStats.InningsPitchedOuts - previousTotals.InningsPitchedOuts),
                EarnedRuns = Math.Max(0, currentStats.EarnedRuns - previousTotals.EarnedRuns),
                Wins = Math.Max(0, currentStats.Wins - previousTotals.Wins),
                Losses = Math.Max(0, currentStats.Losses - previousTotals.Losses),
                AtBats = Math.Max(0, currentStats.AtBats - previousTotals.AtBats),
                Hits = Math.Max(0, currentStats.Hits - previousTotals.Hits),
                Doubles = Math.Max(0, currentStats.Doubles - previousTotals.Doubles),
                Triples = Math.Max(0, currentStats.Triples - previousTotals.Triples),
                HomeRuns = Math.Max(0, currentStats.HomeRuns - previousTotals.HomeRuns),
                Walks = Math.Max(0, currentStats.Walks - previousTotals.Walks),
                Strikeouts = Math.Max(0, currentStats.Strikeouts - previousTotals.Strikeouts),
                PitcherStrikeouts = Math.Max(0, currentStats.StrikeoutsPitched - previousTotals.PitcherStrikeouts)
            };

            if (recentGameLine.GamesPlayed > 0 || recentGameLine.InningsPitchedOuts > 0 || recentGameLine.Hits > 0 || recentGameLine.HomeRuns > 0 || recentGameLine.Wins > 0 || recentGameLine.Losses > 0)
            {
                if (!_saveState.PlayerRecentGameStats.TryGetValue(player.Id, out var log))
                {
                    log = [];
                    _saveState.PlayerRecentGameStats[player.Id] = log;
                }

                log.Add(recentGameLine);
                if (log.Count > 20)
                {
                    var excessCount = log.Count - 20;
                    log.RemoveRange(0, excessCount);
                }
            }

            _saveState.PlayerRecentTrackingTotals[player.Id] = new PlayerRecentTotalsState
            {
                GamesPlayed = currentStats.GamesPlayed,
                GamesPitched = currentStats.GamesPitched,
                InningsPitchedOuts = currentStats.InningsPitchedOuts,
                EarnedRuns = currentStats.EarnedRuns,
                Wins = currentStats.Wins,
                Losses = currentStats.Losses,
                AtBats = currentStats.AtBats,
                Hits = currentStats.Hits,
                Doubles = currentStats.Doubles,
                Triples = currentStats.Triples,
                HomeRuns = currentStats.HomeRuns,
                Walks = currentStats.Walks,
                Strikeouts = currentStats.Strikeouts,
                PitcherStrikeouts = currentStats.StrikeoutsPitched
            };
        }
    }

    private PlayerRecentTotalsState GetTrackedRecentTotals(Guid playerId)
    {
        if (_saveState.PlayerRecentTrackingTotals.TryGetValue(playerId, out var totals))
        {
            return totals;
        }

        return new PlayerRecentTotalsState();
    }

    private void EnsureFranchiseDateInitialized()
    {
        if (_saveState.CurrentFranchiseDate != default)
        {
            return;
        }

        _saveState.CurrentFranchiseDate = GetFranchiseStartDate().Date;
    }

    private List<ScheduleImportDto> GetSeasonSchedule()
    {
        var query = SelectedTeam == null
            ? _leagueData.Schedule.AsEnumerable()
            : _leagueData.Schedule.Where(game =>
                string.Equals(game.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.AwayTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase));

        return query
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameNumber)
            .ToList();
    }

    private int GetSeasonScheduleYear()
    {
        return GetSeasonSchedule().FirstOrDefault()?.Date.Year ?? DateTime.Today.Year;
    }

    private DateTime GetFranchiseStartDate()
    {
        return GetSpringTrainingStartDate();
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

    private void AdvanceFranchiseDateTo(DateTime targetDate)
    {
        var currentDate = _saveState.CurrentFranchiseDate.Date;
        var nextDate = targetDate.Date;
        if (nextDate <= currentDate)
        {
            return;
        }

        SimulateLeagueGamesBetweenDates(currentDate, nextDate);
        ApplyRecoveryBetweenDates(currentDate, nextDate);
        ProcessMonthlyFinanceBetweenDates(currentDate, nextDate);
        _saveState.CurrentFranchiseDate = nextDate;
        ClearTrainingReportsIfSeasonComplete();
    }

    private void AdvanceFranchiseDateToNextUnplayedGame()
    {
        var currentDate = _saveState.CurrentFranchiseDate.Date;
        var nextGame = GetNextScheduledGameForSelectedTeam();
        var targetDate = nextGame?.Date.Date ?? currentDate.AddDays(1);
        AdvanceFranchiseDateTo(targetDate);
    }

    private void ProcessMonthlyFinanceBetweenDates(DateTime currentDate, DateTime targetDate)
    {
        var nextMonth = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1);
        while (nextMonth <= targetDate.Date)
        {
            foreach (var team in _leagueData.Teams)
            {
                var economy = GetOrCreateTeamState(team.Name).Economy;
                _processMonthlyFinanceUseCase.Execute(economy, nextMonth, $"{nextMonth:MMMM yyyy} operating cycle");
            }

            nextMonth = nextMonth.AddMonths(1);
        }
    }

    private void SimulateLeagueGamesBetweenDates(DateTime currentDate, DateTime targetDate)
    {
        for (var date = currentDate.Date.AddDays(1); date < targetDate.Date; date = date.AddDays(1))
        {
            SimulateRemainingLeagueGamesForDate(date);
        }
    }

    private void SimulateRemainingLeagueGamesForDate(DateTime date)
    {
        foreach (var game in GetRemainingScheduledGamesForDate(date))
        {
            TrySimulateScheduledGame(game, advanceDateAfterGame: false, out _);
        }
    }

    private void ProcessEconomyForCompletedGame(MatchState finalState, ScheduleImportDto scheduledGame)
    {
        var homeWon = finalState.HomeTeam.Runs > finalState.AwayTeam.Runs;
        var awayWon = finalState.AwayTeam.Runs > finalState.HomeTeam.Runs;
        var runDifferential = Math.Abs(finalState.HomeTeam.Runs - finalState.AwayTeam.Runs);

        var homeEconomy = GetOrCreateTeamState(scheduledGame.HomeTeamName).Economy;
        var homeWinningPercentage = GetTeamWinningPercentage(scheduledGame.HomeTeamName);
        _processGameRevenueUseCase.Execute(homeEconomy, homeWinningPercentage, scheduledGame.Date.Date, $"Home gate vs {scheduledGame.AwayTeamName}");

        ApplyFanInterestAfterGame(scheduledGame.HomeTeamName, homeWon, runDifferential);
        ApplyFanInterestAfterGame(scheduledGame.AwayTeamName, awayWon, runDifferential);
    }

    private void ApplyFanInterestAfterGame(string teamName, bool wonGame, int runDifferential)
    {
        var economy = GetOrCreateTeamState(teamName).Economy;
        economy.FanInterest = Math.Clamp(economy.FanInterest + FranchiseEconomyEffects.GetFanInterestDelta(wonGame, runDifferential), 10, 100);
        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
    }

    private double GetTeamWinningPercentage(string teamName)
    {
        var summary = GetTeamRecordSummary(teamName);
        var totalGames = summary.Wins + summary.Losses;
        return totalGames == 0 ? 0.5d : summary.Wins / (double)totalGames;
    }

    private TeamRecordSummary GetTeamRecordSummary(string teamName)
    {
        var results = new List<TeamGameResult>();

        foreach (var entry in _saveState.CompletedScheduleGameResults)
        {
            var parts = entry.Key.Split('|');
            if (parts.Length < 4)
            {
                continue;
            }

            var homeTeamName = parts[1];
            var awayTeamName = parts[2];
            var isHomeTeam = string.Equals(homeTeamName, teamName, StringComparison.OrdinalIgnoreCase);
            var isAwayTeam = string.Equals(awayTeamName, teamName, StringComparison.OrdinalIgnoreCase);
            if (!isHomeTeam && !isAwayTeam)
            {
                continue;
            }

            var result = entry.Value;
            var wonGame = isHomeTeam
                ? result.HomeRuns >= result.AwayRuns
                : result.AwayRuns >= result.HomeRuns;

            var gameNumber = int.TryParse(parts[3], out var parsedGameNumber) ? parsedGameNumber : 1;
            var gameDate = DateTime.TryParseExact(
                parts[0],
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsedDate)
                ? parsedDate
                : DateTime.MinValue;

            results.Add(new TeamGameResult(gameDate, gameNumber, wonGame));
        }

        var wins = results.Count(result => result.WonGame);
        var losses = results.Count - wins;
        var streak = GetStreakLabel(results);
        return new TeamRecordSummary(wins, losses, streak);
    }

    private static string GetStreakLabel(IReadOnlyList<TeamGameResult> results)
    {
        if (results.Count == 0)
        {
            return "-";
        }

        var ordered = results
            .OrderByDescending(result => result.Date)
            .ThenByDescending(result => result.GameNumber)
            .ToList();

        var latestWonGame = ordered[0].WonGame;
        var streakCount = 0;
        foreach (var result in ordered)
        {
            if (result.WonGame != latestWonGame)
            {
                break;
            }

            streakCount++;
        }

        return $"{(latestWonGame ? "W" : "L")}{streakCount}";
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
            .Where(player => !ShouldRestPlayer(player.PlayerId, player.PrimaryPosition))
            .OrderBy(player => player.LineupSlot)
            .Select(player => CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age))
            .ToList();

        var remainingPlayers = GetSelectedTeamRoster()
            .Where(player => lineup.All(existing => existing.Id != player.PlayerId) && player.PrimaryPosition is not "SP" and not "RP")
            .OrderBy(player => GetAvailabilityPriority(player.PlayerId, player.PrimaryPosition))
            .ThenBy(player => player.PlayerName)
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
        if (SelectedTeam == null)
        {
            return null;
        }

        var pitcher = SelectStartingPitcher(GetSelectedTeamRoster(), SelectedTeam.Name);
        return pitcher == null
            ? null
            : CreatePlayerSnapshot(pitcher.PlayerId, pitcher.PlayerName, pitcher.PrimaryPosition, pitcher.SecondaryPosition, pitcher.Age);
    }

    private List<MatchPlayerSnapshot> BuildImportedTeamLineup(string teamName)
    {
        var lineup = GetTeamRoster(teamName)
            .Where(player => player.LineupSlot.HasValue && !ShouldRestPlayer(player.PlayerId, player.PrimaryPosition))
            .OrderBy(player => player.LineupSlot)
            .Select(player => CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age))
            .ToList();

        var fillPlayers = GetTeamRoster(teamName)
            .Where(player => lineup.All(existing => existing.Id != player.PlayerId) && player.PrimaryPosition is not "SP" and not "RP")
            .OrderBy(player => GetAvailabilityPriority(player.PlayerId, player.PrimaryPosition))
            .ThenBy(player => player.PlayerName)
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
        var pitcher = SelectStartingPitcher(GetTeamRoster(teamName), teamName);
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
            ratings.EffectiveContactRating,
            ratings.EffectivePowerRating,
            ratings.EffectiveDisciplineRating,
            ratings.EffectiveSpeedRating,
            ratings.EffectivePitchingRating,
            ratings.EffectiveFieldingRating,
            ratings.EffectiveArmRating,
            ratings.EffectiveStaminaRating,
            ratings.EffectiveDurabilityRating,
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
        FinalizeFranchiseScheduledGame(finalState, preferredGame: null, advanceDateAfterGame: true);
    }

    private void FinalizeFranchiseScheduledGame(MatchState finalState, ScheduleImportDto? preferredGame, bool advanceDateAfterGame)
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

        ProcessEconomyForCompletedGame(finalState, gameToMark);

        if (advanceDateAfterGame)
        {
            SimulateRemainingLeagueGamesForDate(gameToMark.Date.Date);
            AdvanceFranchiseDateToNextUnplayedGame();
        }
    }

    private bool EnsurePlayerRatingsGenerated()
    {
        var hasChanges = false;

        foreach (var player in _leagueData.Players)
        {
            if (_saveState.PlayerRatings.TryGetValue(player.PlayerId, out var existingRatings))
            {
                if (existingRatings.StaminaRating <= 0)
                {
                    existingRatings.StaminaRating = PlayerRatingsGenerator.Generate(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age).StaminaRating;
                    hasChanges = true;
                }

                existingRatings.RecalculateDerivedRatings();
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

    private bool EnsureRecentGameTrackingGenerated()
    {
        var hasChanges = false;

        foreach (var player in _leagueData.Players)
        {
            if (!_saveState.PlayerRecentGameStats.ContainsKey(player.PlayerId))
            {
                _saveState.PlayerRecentGameStats[player.PlayerId] = [];
                hasChanges = true;
            }

            if (_saveState.PlayerRecentTrackingTotals.ContainsKey(player.PlayerId))
            {
                continue;
            }

            var stats = GetPlayerSeasonStats(player.PlayerId);
            _saveState.PlayerRecentTrackingTotals[player.PlayerId] = new PlayerRecentTotalsState
            {
                GamesPlayed = stats.GamesPlayed,
                GamesPitched = stats.GamesPitched,
                InningsPitchedOuts = stats.InningsPitchedOuts,
                EarnedRuns = stats.EarnedRuns,
                Wins = stats.Wins,
                Losses = stats.Losses,
                AtBats = stats.AtBats,
                Hits = stats.Hits,
                Doubles = stats.Doubles,
                Triples = stats.Triples,
                HomeRuns = stats.HomeRuns,
                Walks = stats.Walks,
                Strikeouts = stats.Strikeouts,
                PitcherStrikeouts = stats.StrikeoutsPitched
            };
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

    private void TrackPitcherUsage(MatchPlayerSnapshot pitcher)
    {
        if (!_saveState.PlayerHealth.ContainsKey(pitcher.Id) || pitcher.PrimaryPosition is not ("SP" or "RP"))
        {
            return;
        }

        var health = GetPlayerHealth(pitcher.Id);
        health.PitchCountToday = Math.Min(180, health.PitchCountToday + 1);
    }

    private void ApplyPositionPlayerGameFatigue(MatchPlayerSnapshot player)
    {
        if (!_saveState.PlayerHealth.ContainsKey(player.Id) || player.PrimaryPosition is "SP" or "RP")
        {
            return;
        }

        var health = GetPlayerHealth(player.Id);
        var fatigueGain = player.PrimaryPosition == "C" ? 8 : 6;
        if (player.Age >= 32)
        {
            fatigueGain += 1;
        }

        if (player.DurabilityRating >= 70)
        {
            fatigueGain = Math.Max(4, fatigueGain - 1);
        }

        health.Fatigue = Math.Clamp(health.Fatigue + fatigueGain, 0, 100);
        if (health.Fatigue >= 90)
        {
            health.DaysUntilAvailable = Math.Max(health.DaysUntilAvailable, 1);
        }

        MaybeApplyFatigueInjury(player, health);
    }

    private void ApplyPitcherGameFatigue(MatchPlayerSnapshot pitcher, int matchPitchCount)
    {
        if (!_saveState.PlayerHealth.ContainsKey(pitcher.Id) || pitcher.PrimaryPosition is not ("SP" or "RP"))
        {
            return;
        }

        var health = GetPlayerHealth(pitcher.Id);
        var pitchCount = Math.Max(matchPitchCount, health.PitchCountToday);
        health.LastPitchCount = pitchCount;
        health.PitchCountToday = 0;
        health.Fatigue = Math.Clamp(health.Fatigue + CalculatePitcherFatigueGain(pitchCount, pitcher.StaminaRating, pitcher.DurabilityRating), 0, 100);
        health.DaysUntilAvailable = Math.Max(health.DaysUntilAvailable, CalculatePitcherRecoveryDays(pitchCount, pitcher.StaminaRating, pitcher.DurabilityRating));
        MaybeApplyFatigueInjury(pitcher, health);
    }

    private void ApplyPracticeDevelopmentBetweenDates(DateTime currentDate, DateTime targetDate)
    {
        var reportTeamName = SelectedTeam?.Name;
        foreach (var practiceDate in Enumerable.Range(1, Math.Max(0, (targetDate.Date - currentDate.Date).Days - 1))
                     .Select(offset => currentDate.Date.AddDays(offset)))
        {
            ApplyPracticeDevelopmentAcrossLeague(practiceDate, reportTeamName);
        }
    }

    private IReadOnlyList<string> GetLeagueTeamNames()
    {
        return _leagueData.Teams
            .Select(team => team.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<PracticeDevelopmentResult> ApplyPracticeDevelopmentAcrossLeague(DateTime practiceDate, string? reportTeamName = null)
    {
        List<PracticeDevelopmentResult> reportResults = [];

        foreach (var teamName in GetLeagueTeamNames())
        {
            if (!TryGetPracticeSessionInfo(teamName, practiceDate, out var practiceSession))
            {
                continue;
            }

            var developmentResults = ApplyPracticeDevelopmentForDate(teamName, practiceDate, practiceSession.Focus);
            if (!string.IsNullOrWhiteSpace(reportTeamName) && string.Equals(teamName, reportTeamName, StringComparison.OrdinalIgnoreCase))
            {
                reportResults = developmentResults.ToList();
                StoreTrainingReport(practiceDate, practiceSession, reportResults);
            }
        }

        return reportResults;
    }

    private IReadOnlyList<PracticeDevelopmentResult> ApplyPracticeDevelopmentForDate(string teamName, DateTime practiceDate, TeamPracticeFocus focus)
    {
        var roster = GetTeamRoster(teamName);
        if (roster.Count == 0)
        {
            return [];
        }

        var results = new List<PracticeDevelopmentResult>();
        foreach (var player in roster)
        {
            var result = ApplyPracticeDevelopment(player, teamName, focus, practiceDate);
            if (result.HasValue)
            {
                results.Add(result.Value);
            }
        }

        return results;
    }

    private PracticeDevelopmentResult? ApplyPracticeDevelopment(FranchiseRosterEntry player, string teamName, TeamPracticeFocus focus, DateTime practiceDate)
    {
        if (!IsPlayerEligibleForPracticeDevelopment(player.PrimaryPosition, focus))
        {
            return null;
        }

        var random = CreateStableRandom(player.PlayerId.ToString(), "practice-growth", practiceDate.ToString("yyyyMMdd"), focus.ToString(), player.PrimaryPosition);
        var ratingGain = RollPracticeDevelopmentGain(random);
        var economy = GetOrCreateTeamState(teamName).Economy;
        var developmentMultiplier = FranchiseEconomyEffects.GetDevelopmentMultiplier(economy.BudgetAllocation.PlayerDevelopmentBudget, economy.FacilitiesLevel);
        ratingGain = RoundToHalfPoint(Math.Clamp(ratingGain * developmentMultiplier, 0d, 1.5d));

        if (ratingGain <= 0d)
        {
            return null;
        }

        var attribute = PickPracticeDevelopmentAttribute(random, focus, player.PrimaryPosition);
        var coachRole = GetPracticeCoachRole(focus, player.PrimaryPosition, attribute);
        AdjustPlayerRatings(player.PlayerId, ratings => ApplyPracticeDevelopmentGain(ratings, attribute, ratingGain));
        return new PracticeDevelopmentResult(coachRole, player.PlayerName, player.PrimaryPosition, attribute, ratingGain);
    }

    private bool TryGetPracticeSessionInfo(DateTime practiceDate, out PracticeSessionInfo practiceSession)
    {
        practiceSession = default;
        return SelectedTeam != null && TryGetPracticeSessionInfo(SelectedTeam.Name, practiceDate, out practiceSession);
    }

    private bool TryGetPracticeSessionInfo(string teamName, DateTime practiceDate, out PracticeSessionInfo practiceSession)
    {
        practiceSession = default;
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return false;
        }

        var teamGames = _leagueData.Schedule
            .Where(game =>
                string.Equals(game.HomeTeamName, teamName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.AwayTeamName, teamName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameNumber)
            .ToList();

        if (teamGames.Count == 0 || teamGames.Any(game => game.Date.Date == practiceDate.Date))
        {
            return false;
        }

        var firstGameDate = teamGames.Min(game => game.Date.Date);
        var lastGameDate = teamGames.Max(game => game.Date.Date);
        var springTrainingStartDate = GetSpringTrainingStartDate().Date;
        if (practiceDate.Date < springTrainingStartDate || practiceDate.Date > lastGameDate)
        {
            return false;
        }

        var focus = GetPracticeFocus(teamName, practiceDate);
        if (practiceDate.Date < firstGameDate)
        {
            practiceSession = new PracticeSessionInfo(focus, false, true);
            return true;
        }

        var hasCustomPracticeFocus = HasCustomPracticeFocus(teamName, practiceDate);
        var previousGame = teamGames.Where(game => game.Date.Date < practiceDate.Date).OrderByDescending(game => game.Date).FirstOrDefault();
        var nextGame = teamGames.Where(game => game.Date.Date > practiceDate.Date).OrderBy(game => game.Date).FirstOrDefault();
        var daysSinceLastGame = previousGame == null ? 99 : (practiceDate.Date - previousGame.Date.Date).Days;
        var daysUntilNextGame = nextGame == null ? 99 : (nextGame.Date.Date - practiceDate.Date).Days;

        if (focus == TeamPracticeFocus.Recovery || (!hasCustomPracticeFocus && daysSinceLastGame == 1 && daysUntilNextGame == 1))
        {
            practiceSession = new PracticeSessionInfo(TeamPracticeFocus.Recovery, true, false);
            return true;
        }

        var isLightWorkout = daysSinceLastGame == 1 || daysUntilNextGame == 1;
        practiceSession = new PracticeSessionInfo(focus, isLightWorkout, false);
        return true;
    }

    private string BuildPracticeDevelopmentReport(DateTime practiceDate, PracticeSessionInfo practiceSession, IReadOnlyList<PracticeDevelopmentResult> results)
    {
        var dayLabel = BuildPracticeReportTitle(practiceDate, practiceSession);
        var coachNotes = GetPracticeReportCoachNotes(results);
        return $"{dayLabel}. {string.Join(" ", coachNotes)}";
    }

    private string BuildPracticeReportTitle(DateTime practiceDate, PracticeSessionInfo practiceSession)
    {
        return practiceSession.IsSpringTraining
            ? $"Spring work on {practiceDate:ddd, MMM d}"
            : practiceSession.Focus == TeamPracticeFocus.Recovery
                ? $"Recovery / film day on {practiceDate:ddd, MMM d}"
                : $"{(practiceSession.IsLightWorkout ? "Light " : string.Empty)}{GetPracticeFocusLabel(practiceSession.Focus)} work on {practiceDate:ddd, MMM d}";
    }

    private List<string> GetPracticeReportCoachNotes(IReadOnlyList<PracticeDevelopmentResult> results)
    {
        if (results.Count == 0)
        {
            return ["The staff liked the tempo, but nobody made a noticeable jump today."];
        }

        return results
            .GroupBy(result => result.CoachRole)
            .OrderBy(group => GetCoachRoleSortOrder(group.Key))
            .Select(group => BuildCoachPracticeSummary(group.Key, group.ToList()))
            .ToList();
    }

    private void StoreTrainingReport(DateTime practiceDate, PracticeSessionInfo practiceSession, IReadOnlyList<PracticeDevelopmentResult> results)
    {
        if (SelectedTeam == null)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        var seasonYear = GetCurrentTrainingReportSeason();
        var coachNotes = GetPracticeReportCoachNotes(results);

        teamState.TrainingReports.RemoveAll(report => report.SeasonYear != seasonYear || report.ReportDate.Date == practiceDate.Date);
        teamState.TrainingReports.Add(new TrainingReportState
        {
            ReportDate = practiceDate.Date,
            SeasonYear = seasonYear,
            Title = BuildPracticeReportTitle(practiceDate, practiceSession),
            FocusLabel = GetPracticeFocusLabel(practiceSession.Focus),
            Summary = results.Count == 0
                ? coachNotes[0]
                : $"{coachNotes.Count} staff update(s) were filed after the workout.",
            CoachNotes = coachNotes
        });
        teamState.TrainingReports = teamState.TrainingReports
            .OrderByDescending(report => report.ReportDate)
            .ToList();
    }

    private string BuildCoachPracticeSummary(string coachRole, IReadOnlyList<PracticeDevelopmentResult> results)
    {
        var coachName = GetCoachNameByRole(coachRole);
        var playerNotes = results
            .Select(DescribePracticeImprovement)
            .ToList();

        return $"{coachName} ({coachRole}): {string.Join("; ", playerNotes)}.";
    }

    private static string DescribePracticeImprovement(PracticeDevelopmentResult result)
    {
        var playerName = GetPracticeReportName(result.PlayerName);
        return result.Attribute switch
        {
            PracticeDevelopmentAttribute.Contact => result.Amount >= 2d
                ? $"{playerName} was squaring everything up"
                : result.Amount >= 1d
                    ? $"{playerName} showed sharper barrel control"
                    : $"{playerName} found a cleaner swing path",
            PracticeDevelopmentAttribute.Power => result.Amount >= 2d
                ? $"{playerName} was driving the ball with real thunder"
                : result.Amount >= 1d
                    ? $"{playerName} got more carry off the bat"
                    : $"{playerName} showed a little extra jump in the swing",
            PracticeDevelopmentAttribute.Discipline => result.Amount >= 2d
                ? $"{playerName} was seeing the zone exceptionally well"
                : result.Amount >= 1d
                    ? $"{playerName} tracked pitches better today"
                    : $"{playerName} looked more under control in counts",
            PracticeDevelopmentAttribute.Speed => result.Amount >= 2d
                ? $"{playerName} had a real burst in the first step"
                : result.Amount >= 1d
                    ? $"{playerName} looked quicker out of the box"
                    : $"{playerName} showed a little more burst",
            PracticeDevelopmentAttribute.Fielding => result.Amount >= 2d
                ? $"{playerName} turned in noticeably cleaner glove work"
                : result.Amount >= 1d
                    ? $"{playerName} handled the ball with more confidence"
                    : $"{playerName}'s footwork tightened up",
            PracticeDevelopmentAttribute.Arm => result.Amount >= 2d
                ? $"{playerName}'s throws had real carry"
                : result.Amount >= 1d
                    ? $"{playerName}'s arm looked livelier"
                    : $"{playerName} got a bit more zip on the ball",
            PracticeDevelopmentAttribute.Pitching => result.Amount >= 2d
                ? $"{playerName}'s stuff had real late life"
                : result.Amount >= 1d
                    ? $"{playerName} finished pitches with more conviction"
                    : $"{playerName}'s delivery looked a touch crisper",
            PracticeDevelopmentAttribute.Stamina => result.Amount >= 2d
                ? $"{playerName} held the workload deep into the session"
                : result.Amount >= 1d
                    ? $"{playerName} carried the work better today"
                    : $"{playerName} looked a little stronger late",
            _ => result.Amount >= 2d
                ? $"{playerName}'s body handled the grind with ease"
                : result.Amount >= 1d
                    ? $"{playerName} bounced back well between reps"
                    : $"{playerName} came through the work a bit cleaner"
        };
    }

    private static string GetPracticeReportName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? fullName : parts[^1];
    }

    private static bool IsPlayerEligibleForPracticeDevelopment(string primaryPosition, TeamPracticeFocus focus)
    {
        var isPitcher = primaryPosition is "SP" or "RP";
        return focus switch
        {
            TeamPracticeFocus.Hitting => !isPitcher,
            TeamPracticeFocus.Pitching => isPitcher,
            TeamPracticeFocus.Defense => !isPitcher,
            TeamPracticeFocus.Baserunning => !isPitcher,
            _ => true
        };
    }

    private static double RollPracticeDevelopmentGain(Random random)
    {
        var roll = random.NextDouble();
        if (roll < 0.0625d)
        {
            return 1d;
        }

        if (roll < 0.1875d)
        {
            return 0.5d;
        }

        return roll < 0.4375d ? 0.5d : 0d;
    }

    private static PracticeDevelopmentAttribute PickPracticeDevelopmentAttribute(Random random, TeamPracticeFocus focus, string primaryPosition)
    {
        var options = GetPracticeDevelopmentAttributes(focus, primaryPosition);
        return options[random.Next(options.Length)];
    }

    private static string GetPracticeCoachRole(TeamPracticeFocus focus, string primaryPosition, PracticeDevelopmentAttribute attribute)
    {
        return focus switch
        {
            TeamPracticeFocus.Hitting => "Hitting Coach",
            TeamPracticeFocus.Pitching => "Pitching Coach",
            TeamPracticeFocus.Defense or TeamPracticeFocus.Baserunning => "Bench Coach",
            TeamPracticeFocus.Recovery => "Physiologist",
            _ => attribute switch
            {
                PracticeDevelopmentAttribute.Contact or PracticeDevelopmentAttribute.Power or PracticeDevelopmentAttribute.Discipline => "Hitting Coach",
                PracticeDevelopmentAttribute.Pitching or PracticeDevelopmentAttribute.Stamina => "Pitching Coach",
                PracticeDevelopmentAttribute.Fielding or PracticeDevelopmentAttribute.Arm or PracticeDevelopmentAttribute.Speed => "Bench Coach",
                _ => primaryPosition is "SP" or "RP" ? "Pitching Coach" : "Physiologist"
            }
        };
    }

    private static PracticeDevelopmentAttribute[] GetPracticeDevelopmentAttributes(TeamPracticeFocus focus, string primaryPosition)
    {
        var isPitcher = primaryPosition is "SP" or "RP";

        return focus switch
        {
            TeamPracticeFocus.Hitting => [PracticeDevelopmentAttribute.Contact, PracticeDevelopmentAttribute.Power, PracticeDevelopmentAttribute.Discipline],
            TeamPracticeFocus.Pitching when isPitcher => [PracticeDevelopmentAttribute.Pitching, PracticeDevelopmentAttribute.Stamina, PracticeDevelopmentAttribute.Durability],
            TeamPracticeFocus.Pitching => [PracticeDevelopmentAttribute.Arm, PracticeDevelopmentAttribute.Fielding, PracticeDevelopmentAttribute.Discipline],
            TeamPracticeFocus.Defense when string.Equals(primaryPosition, "C", StringComparison.OrdinalIgnoreCase) => [PracticeDevelopmentAttribute.Fielding, PracticeDevelopmentAttribute.Arm, PracticeDevelopmentAttribute.Durability],
            TeamPracticeFocus.Defense => [PracticeDevelopmentAttribute.Fielding, PracticeDevelopmentAttribute.Arm, PracticeDevelopmentAttribute.Speed],
            TeamPracticeFocus.Baserunning => [PracticeDevelopmentAttribute.Speed, PracticeDevelopmentAttribute.Discipline, PracticeDevelopmentAttribute.Durability],
            TeamPracticeFocus.Recovery when isPitcher => [PracticeDevelopmentAttribute.Durability, PracticeDevelopmentAttribute.Stamina],
            TeamPracticeFocus.Recovery => [PracticeDevelopmentAttribute.Durability, PracticeDevelopmentAttribute.Speed],
            _ when isPitcher => [PracticeDevelopmentAttribute.Pitching, PracticeDevelopmentAttribute.Stamina, PracticeDevelopmentAttribute.Durability, PracticeDevelopmentAttribute.Arm, PracticeDevelopmentAttribute.Fielding],
            _ => [PracticeDevelopmentAttribute.Contact, PracticeDevelopmentAttribute.Power, PracticeDevelopmentAttribute.Discipline, PracticeDevelopmentAttribute.Speed, PracticeDevelopmentAttribute.Fielding, PracticeDevelopmentAttribute.Arm, PracticeDevelopmentAttribute.Durability]
        };
    }

    private static void ApplyPracticeDevelopmentGain(PlayerHiddenRatingsState ratings, PracticeDevelopmentAttribute attribute, double amount)
    {
        switch (attribute)
        {
            case PracticeDevelopmentAttribute.Contact:
                (ratings.ContactRating, ratings.ContactProgress) = AddFractionalRating(ratings.ContactRating, ratings.ContactProgress, amount);
                break;
            case PracticeDevelopmentAttribute.Power:
                (ratings.PowerRating, ratings.PowerProgress) = AddFractionalRating(ratings.PowerRating, ratings.PowerProgress, amount);
                break;
            case PracticeDevelopmentAttribute.Discipline:
                (ratings.DisciplineRating, ratings.DisciplineProgress) = AddFractionalRating(ratings.DisciplineRating, ratings.DisciplineProgress, amount);
                break;
            case PracticeDevelopmentAttribute.Speed:
                (ratings.SpeedRating, ratings.SpeedProgress) = AddFractionalRating(ratings.SpeedRating, ratings.SpeedProgress, amount);
                break;
            case PracticeDevelopmentAttribute.Fielding:
                (ratings.FieldingRating, ratings.FieldingProgress) = AddFractionalRating(ratings.FieldingRating, ratings.FieldingProgress, amount);
                break;
            case PracticeDevelopmentAttribute.Arm:
                (ratings.ArmRating, ratings.ArmProgress) = AddFractionalRating(ratings.ArmRating, ratings.ArmProgress, amount);
                break;
            case PracticeDevelopmentAttribute.Pitching:
                (ratings.PitchingRating, ratings.PitchingProgress) = AddFractionalRating(ratings.PitchingRating, ratings.PitchingProgress, amount);
                break;
            case PracticeDevelopmentAttribute.Stamina:
                (ratings.StaminaRating, ratings.StaminaProgress) = AddFractionalRating(ratings.StaminaRating, ratings.StaminaProgress, amount);
                break;
            case PracticeDevelopmentAttribute.Durability:
                (ratings.DurabilityRating, ratings.DurabilityProgress) = AddFractionalRating(ratings.DurabilityRating, ratings.DurabilityProgress, amount);
                break;
        }
    }

    private static (int Rating, double Progress) AddFractionalRating(int rating, double progress, double amount)
    {
        var exactRating = Math.Clamp(rating + progress + amount, 1d, 99d);
        var wholeRating = Math.Clamp((int)Math.Floor(exactRating), 1, 99);
        var newProgress = wholeRating >= 99
            ? 0d
            : Math.Round((exactRating - wholeRating) * 2d, MidpointRounding.AwayFromZero) / 2d;

        return (wholeRating, newProgress);
    }

    private void ApplyRecoveryBetweenDates(DateTime currentDate, DateTime targetDate)
    {
        var daysElapsed = Math.Max(0, (targetDate.Date - currentDate.Date).Days);
        if (daysElapsed <= 0)
        {
            return;
        }

        ApplyPracticeDevelopmentBetweenDates(currentDate, targetDate);

        foreach (var player in _leagueData.Players)
        {
            var health = GetPlayerHealth(player.PlayerId);
            var isPitcher = player.PrimaryPosition is "SP" or "RP";
            var assignedTeamName = GetAssignedTeamName(player.PlayerId, player.TeamName);
            var teamEconomy = string.IsNullOrWhiteSpace(assignedTeamName) ? null : GetOrCreateTeamState(assignedTeamName).Economy;
            var recoveryMultiplier = teamEconomy == null
                ? 1d
                : FranchiseEconomyEffects.GetMedicalRecoveryMultiplier(teamEconomy.BudgetAllocation.MedicalBudget, teamEconomy.FacilitiesLevel);
            var recoveryPerDay = Math.Max(1, (int)Math.Round((isPitcher ? 18 : 4) * recoveryMultiplier));

            health.Fatigue = Math.Max(0, health.Fatigue - (daysElapsed * recoveryPerDay));
            health.DaysUntilAvailable = Math.Max(0, health.DaysUntilAvailable - daysElapsed);
            health.InjuryDaysRemaining = Math.Max(0, health.InjuryDaysRemaining - daysElapsed);
            if (health.InjuryDaysRemaining == 0)
            {
                health.InjuryDescription = string.Empty;
            }

            if (daysElapsed > 0)
            {
                health.PitchCountToday = 0;
            }
        }
    }

    private FranchiseRosterEntry? SelectStartingPitcher(IEnumerable<FranchiseRosterEntry> rosterEntries, string teamName)
    {
        var pitchers = rosterEntries
            .Where(player => player.PrimaryPosition is "SP" or "RP")
            .ToList();

        if (pitchers.Count == 0)
        {
            return null;
        }

        var orderedRotation = pitchers
            .Where(player => player.RotationSlot.HasValue)
            .OrderBy(player => player.RotationSlot)
            .ToList();

        if (orderedRotation.Count > 0)
        {
            var scheduledIndex = GetScheduledRotationIndex(teamName, orderedRotation.Count);
            for (var offset = 0; offset < orderedRotation.Count; offset++)
            {
                var candidate = orderedRotation[(scheduledIndex + offset) % orderedRotation.Count];
                if (!ShouldRestPlayer(candidate.PlayerId, candidate.PrimaryPosition))
                {
                    return candidate;
                }
            }

            return orderedRotation[scheduledIndex];
        }

        return pitchers
            .OrderBy(player => ShouldRestPlayer(player.PlayerId, player.PrimaryPosition) ? 1 : 0)
            .ThenBy(player => GetAvailabilityPriority(player.PlayerId, player.PrimaryPosition))
            .ThenBy(player => player.PrimaryPosition is "SP" ? 0 : 1)
            .ThenBy(player => player.PlayerName)
            .FirstOrDefault();
    }

    private int GetScheduledRotationIndex(string teamName, int starterCount)
    {
        if (starterCount <= 0)
        {
            return 0;
        }

        var completedGames = _leagueData.Schedule.Count(game =>
            IsScheduledGameCompleted(game) &&
            (string.Equals(game.HomeTeamName, teamName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(game.AwayTeamName, teamName, StringComparison.OrdinalIgnoreCase)));

        return completedGames % starterCount;
    }

    private bool ShouldRestPlayer(Guid playerId, string primaryPosition)
    {
        var health = GetPlayerHealth(playerId);
        if (health.InjuryDaysRemaining > 0)
        {
            return true;
        }

        var isPitcher = primaryPosition is "SP" or "RP";
        return isPitcher
            ? health.DaysUntilAvailable > 0 || health.Fatigue >= 80
            : health.DaysUntilAvailable > 1 || health.Fatigue >= 92;
    }

    private int GetAvailabilityPriority(Guid playerId, string primaryPosition)
    {
        var health = GetPlayerHealth(playerId);
        var injuryPenalty = health.InjuryDaysRemaining * 1000;
        var recoveryPenalty = health.DaysUntilAvailable * 100;
        var fatiguePenalty = health.Fatigue;
        if (primaryPosition is not ("SP" or "RP") && health.Fatigue >= 90)
        {
            fatiguePenalty += 120;
        }

        return injuryPenalty + recoveryPenalty + fatiguePenalty;
    }

    private void MaybeApplyFatigueInjury(MatchPlayerSnapshot player, PlayerHealthState health)
    {
        if (health.InjuryDaysRemaining > 0 || health.Fatigue < 78)
        {
            return;
        }

        var isPitcher = player.PrimaryPosition is "SP" or "RP";
        var playerImport = FindPlayerImport(player.Id);
        var assignedTeamName = playerImport == null ? string.Empty : GetAssignedTeamName(player.Id, playerImport.TeamName);
        var teamEconomy = string.IsNullOrWhiteSpace(assignedTeamName) ? null : GetOrCreateTeamState(assignedTeamName).Economy;
        var medicalProtection = teamEconomy == null
            ? 0d
            : (FranchiseEconomyEffects.GetMedicalRecoveryMultiplier(teamEconomy.BudgetAllocation.MedicalBudget, teamEconomy.FacilitiesLevel) - 1d) * 20d;
        var riskScore = health.Fatigue + (health.DaysUntilAvailable * 8) - (player.DurabilityRating / 4);
        var riskThreshold = (isPitcher ? 92 : 97) + (int)Math.Round(medicalProtection);
        if (riskScore < riskThreshold)
        {
            return;
        }

        var random = CreateStableRandom(player.Id.ToString(), GetCurrentFranchiseDate().ToString("yyyyMMdd"), health.Fatigue.ToString(), health.LastPitchCount.ToString());
        var triggerChance = Math.Clamp(riskScore - riskThreshold + 8, 5, 28);
        if (random.Next(100) >= triggerChance)
        {
            return;
        }

        var injuryOptions = isPitcher
            ? new[] { "shoulder inflammation", "forearm tightness", "elbow soreness" }
            : new[] { "hamstring tightness", "quad soreness", "wrist soreness" };
        health.InjuryDescription = injuryOptions[random.Next(injuryOptions.Length)];
        health.InjuryDaysRemaining = isPitcher ? random.Next(3, 8) : random.Next(1, 5);
        health.DaysUntilAvailable = Math.Max(health.DaysUntilAvailable, health.InjuryDaysRemaining);
    }

    private MedicalPlayerStatus BuildMedicalPlayerStatus(Guid playerId, string playerName, string primaryPosition, string secondaryPosition, int age)
    {
        var health = GetPlayerHealth(playerId);
        var ratings = GetPlayerRatings(playerId, playerName, primaryPosition, secondaryPosition, age);
        var isPitcher = primaryPosition is "SP" or "RP";

        string status;
        string report;

        if (health.InjuryDaysRemaining > 0)
        {
            status = $"Out {health.InjuryDaysRemaining}d";
            report = $"{GetMedicalStaffName("Team Doctor")}: {playerName} is dealing with {health.InjuryDescription}. I would plan on about {health.InjuryDaysRemaining} more day(s) down.";
        }
        else if (isPitcher && health.DaysUntilAvailable > 0)
        {
            var recentPitchCount = Math.Max(health.LastPitchCount, health.PitchCountToday);
            status = $"Rest {health.DaysUntilAvailable}d";
            report = $"{GetMedicalStaffName("Physiologist")}: after {recentPitchCount} pitches, the arm still needs {health.DaysUntilAvailable} more day(s). Stamina looks {DescribeStamina(ratings.EffectiveStaminaRating)}.";
        }
        else if (health.Fatigue >= 75)
        {
            status = "High risk";
            report = isPitcher
                ? $"{GetMedicalStaffName("Team Doctor")}: the arm is running hot right now. I would avoid another heavy outing until he settles."
                : $"{GetMedicalStaffName("Physiologist")}: the legs look heavy. He can play, but a rest day would help.";
        }
        else if (health.Fatigue >= 45)
        {
            status = "Watch";
            report = isPitcher
                ? "He is usable, but the stuff could back up once the pitch count climbs."
                : "The workload is starting to show. I would rotate in a lighter day soon.";
        }
        else
        {
            status = "Ready";
            report = isPitcher
                ? "No major red flag today. He looks ready for a normal pitching load."
                : "No major concern today. He should handle a regular game workload.";
        }

        return new MedicalPlayerStatus(playerId, playerName, primaryPosition, status, report, health.Fatigue, health.DaysUntilAvailable, health.InjuryDaysRemaining > 0);
    }

    private void ClearTrainingReportsIfSeasonComplete()
    {
        if (SelectedTeam == null)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        if (teamState.TrainingReports.Count == 0)
        {
            return;
        }

        var teamSchedule = _leagueData.Schedule
            .Where(game =>
                string.Equals(game.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.AwayTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (teamSchedule.Count == 0)
        {
            teamState.TrainingReports.Clear();
            return;
        }

        var seasonIsOver = !teamSchedule.Any(game => !IsScheduledGameCompleted(game)) &&
                           GetCurrentFranchiseDate().Date > teamSchedule.Max(game => game.Date.Date);
        if (seasonIsOver)
        {
            teamState.TrainingReports.Clear();
        }
    }

    private string GetCoachNameByRole(string role)
    {
        return GetCoachingStaff().FirstOrDefault(coach => string.Equals(coach.Role, role, StringComparison.OrdinalIgnoreCase))?.Name ?? role;
    }

    private string GetMedicalStaffName(string role)
    {
        return GetCoachNameByRole(role);
    }

    private static string DescribeStamina(int staminaRating)
    {
        return staminaRating switch
        {
            >= 75 => "strong",
            >= 60 => "solid",
            >= 45 => "average",
            _ => "light"
        };
    }

    private static int CalculatePitcherFatigueGain(int pitchCount, int staminaRating, int durabilityRating)
    {
        if (pitchCount <= 0)
        {
            return 0;
        }

        var comfortLimit = 50 + (staminaRating / 2);
        var overage = Math.Max(0, pitchCount - comfortLimit);
        var durabilityOffset = Math.Max(0, durabilityRating - 50) / 6;
        return Math.Clamp(8 + (pitchCount / 4) + (overage / 2) - durabilityOffset, 8, 75);
    }

    private static int CalculatePitcherRecoveryDays(int pitchCount, int staminaRating, int durabilityRating)
    {
        if (pitchCount <= 25)
        {
            return 1;
        }

        var comfortLimit = 50 + (staminaRating / 2);
        var overage = Math.Max(0, pitchCount - comfortLimit);
        var baseDays = pitchCount switch
        {
            >= 115 => 5,
            >= 100 => 4,
            >= 85 => 3,
            >= 65 => 2,
            _ => 1
        };

        if (overage >= 20)
        {
            baseDays++;
        }

        if (durabilityRating >= 72)
        {
            baseDays--;
        }
        else if (durabilityRating <= 40)
        {
            baseDays++;
        }

        return Math.Clamp(baseDays, 1, 7);
    }

    private void MovePlayerContract(Guid playerId, TeamEconomy fromEconomy, TeamEconomy toEconomy, string playerName)
    {
        var contract = fromEconomy.PlayerContracts.FirstOrDefault(existing => existing.SubjectId == playerId);
        if (contract == null)
        {
            var player = FindPlayerImport(playerId);
            if (player != null)
            {
                _signPlayerContractUseCase.Execute(toEconomy, playerId, playerName, EstimatePlayerSalary(player), player.Age <= 29 ? 3 : 2, GetCurrentFranchiseDate());
            }
        }
        else
        {
            fromEconomy.PlayerContracts.Remove(contract);
            toEconomy.PlayerContracts.RemoveAll(existing => existing.SubjectId == playerId);
            toEconomy.PlayerContracts.Add(contract);
        }

        fromEconomy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(fromEconomy);
        toEconomy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(toEconomy);
    }

    private void ApplyTradeFanInterestShift(TeamEconomy economy, PlayerImportDto incomingPlayer, PlayerImportDto outgoingPlayer)
    {
        var incomingOverall = GetPlayerRatings(incomingPlayer.PlayerId, incomingPlayer.FullName, incomingPlayer.PrimaryPosition, incomingPlayer.SecondaryPosition, incomingPlayer.Age).OverallRating;
        var outgoingOverall = GetPlayerRatings(outgoingPlayer.PlayerId, outgoingPlayer.FullName, outgoingPlayer.PrimaryPosition, outgoingPlayer.SecondaryPosition, outgoingPlayer.Age).OverallRating;
        var difference = incomingOverall - outgoingOverall;
        var fanInterestDelta = difference switch
        {
            >= 8 => 3,
            >= 3 => 1,
            <= -8 => -3,
            <= -3 => -1,
            _ => 0
        };

        economy.FanInterest = Math.Clamp(economy.FanInterest + fanInterestDelta, 10, 100);
        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
    }

    private static decimal GetBudgetValue(BudgetAllocation allocation, string budgetKey)
    {
        return budgetKey.ToLowerInvariant() switch
        {
            "scouting" => allocation.ScoutingBudget,
            "development" => allocation.PlayerDevelopmentBudget,
            "medical" => allocation.MedicalBudget,
            "facilities" => allocation.FacilitiesBudget,
            _ => 0m
        };
    }

    private static string GetBudgetDisplay(decimal value)
    {
        return value >= 1_000_000m
            ? $"${value / 1_000_000m:0.00}M"
            : $"${value / 1_000m:0}K";
    }

    private static double RoundToHalfPoint(double value)
    {
        return Math.Round(value * 2d, MidpointRounding.AwayFromZero) / 2d;
    }

    private static double MapRating(double rating, double lowValue, double highValue)
    {
        var normalized = Math.Clamp((rating - 1d) / 98d, 0d, 1d);
        return lowValue + ((highValue - lowValue) * normalized);
    }

    private static string BuildPracticeDateKey(DateTime date)
    {
        return date.Date.ToString("yyyy-MM-dd");
    }

    private void Save()
    {
        _stateStore.Save(_saveState);
    }

    private readonly record struct PracticeSessionInfo(TeamPracticeFocus Focus, bool IsLightWorkout, bool IsSpringTraining);

    private readonly record struct PracticeDevelopmentResult(string CoachRole, string PlayerName, string PrimaryPosition, PracticeDevelopmentAttribute Attribute, double Amount);

    private readonly record struct TeamGameResult(DateTime Date, int GameNumber, bool WonGame);

    private readonly record struct TeamRecordSummary(int Wins, int Losses, string Streak);

    private sealed record AttributeInsight(string Key, int Rating);
}
