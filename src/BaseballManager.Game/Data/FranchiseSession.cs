using BaseballManager.Application.Drafts;
using BaseballManager.Application.Transactions;
using BaseballManager.Contracts.ImportDtos;
using BaseballManager.Core.Drafts;
using BaseballManager.Core.Economy;
using BaseballManager.Core.Players;
using BaseballManager.Core.Teams;
using BaseballManager.Sim.AI;
using BaseballManager.Sim.Economy;
using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Results;

namespace BaseballManager.Game.Data;

public sealed class FranchiseSession
{
    private const int MaxRosterSize = 40;
    private const int FirstTeamRosterSize = 26;
    private const int AffiliateRosterSize = 26;
    private const string FreeAgentTeamName = "Free Agents";
    private const int DefaultDraftRounds = 5;
    private const string PendingRosterAssignment = "Pending";
    private const string MajorLeagueRosterAssignment = "40-Man Roster";
    private const string ReserveRosterAssignment = "Organization Roster";
    private const string LegacyAffiliateRosterAssignment = "Affiliate";
    private const string TripleARosterAssignment = "AAA";
    private const string DoubleARosterAssignment = "AA";
    private const string SingleARosterAssignment = "A";

    private enum RosterCompositionBucket
    {
        Pitchers,
        Catchers,
        Infielders,
        Outfielders,
        Utility
    }

    private delegate bool BatchRosterMoveOperation(Guid playerId, out string statusMessage);

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

    private readonly record struct LineupSlotAssignments(
        int VsLeftHandedPitcherIndex,
        int VsRightHandedPitcherIndex,
        bool IsVsLeftHandedPitcherDesignatedHitter,
        bool IsVsRightHandedPitcherDesignatedHitter);

    private readonly record struct LineupPresetConfiguration(List<Guid?> Slots, Guid? DesignatedHitterPlayerId);

    private sealed record DefaultLineupCandidate(
        Guid PlayerId,
        string PlayerName,
        string PrimaryPosition,
        string SecondaryPosition,
        int Age,
        int OverallRating,
        int ContactRating,
        int PowerRating,
        int DisciplineRating,
        int SpeedRating);

    private static readonly string[] CoachRoleOrder = ["Manager", "Hitting Coach", "Pitching Coach", "Bench Coach", "Scouting Director", "Team Doctor", "Physiologist"];
    private static readonly string[] CoachFirstNames = ["Alex", "Jordan", "Sam", "Casey", "Drew", "Taylor", "Riley", "Morgan", "Cameron", "Jamie", "Hayden", "Avery"];
    private static readonly string[] CoachLastNames = ["Maddox", "Sullivan", "Torres", "Bennett", "Foster", "Callahan", "Diaz", "Reed", "Hughes", "Alvarez", "Parker", "Watts"];
    private static readonly string[] ScoutingCountryOptions = ["U.S. High School", "Dominican Republic", "Venezuela", "Japan", "South Korea", "Mexico", "Canada", "Cuba", "Puerto Rico"];
    private static readonly string[] ScoutingPositionOptions = ["Any", "C", "1B", "2B", "3B", "SS", "OF", "SP", "RP"];
    private static readonly string[] ScoutingTraitOptions = ["Best Athlete", "Power Hitter", "Contact Hitter", "Disciplined Hitter", "Speed / Defense", "Power Pitcher", "Location Pitcher", "Workhorse Starter", "Late-Inning Arm"];
    private static readonly string[] ScoutAssignmentModeOptions = ["Unassigned", "Region Search", "Player Follow", "Auto Need Search"];
    private static readonly string[] ProspectFirstNames = ["Mason", "Jace", "Noah", "Liam", "Elijah", "Carter", "Diego", "Luis", "Mateo", "Yuki", "Hiro", "Seong", "Min", "Adrian", "Roman", "Trey"];
    private static readonly string[] ProspectLastNames = ["Johnson", "Miller", "Clark", "Ramirez", "De La Cruz", "Santos", "Kim", "Park", "Tanaka", "Sato", "Rivera", "Torres", "Gonzalez", "Lee", "Martinez", "Flores"];
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseStateStore _stateStore;
    private readonly FranchiseSaveState _saveState;
    private readonly ProcessGameRevenueUseCase _processGameRevenueUseCase = new();
    private readonly ProcessMonthlyFinanceUseCase _processMonthlyFinanceUseCase = new();
    private readonly SetBudgetAllocationUseCase _setBudgetAllocationUseCase = new();
    private readonly SignPlayerContractUseCase _signPlayerContractUseCase = new();
    private readonly ReleasePlayerUseCase _releasePlayerUseCase = new();
    private readonly OffseasonContractEvaluator _offseasonContractEvaluator = new();
    private readonly DraftProspectFactory _draftProspectFactory = new();
    private readonly StartDraftUseCase _startDraftUseCase = new();
    private readonly GetDraftStateUseCase _getDraftStateUseCase = new();
    private readonly MakeUserDraftPickUseCase _makeUserDraftPickUseCase = new();
    private readonly AdvanceDraftUseCase _advanceDraftUseCase = new();
    private readonly FinishDraftUseCase _finishDraftUseCase = new();
    private readonly DraftCpuPicker _draftCpuPicker = new();
    private readonly ManagerAi _managerAi = new();
    private readonly HireCoachUseCase _hireCoachUseCase = new();
    private readonly Dictionary<Guid, PlayerSeasonStatsState> _lastSeasonStatsCache = new();
    private Guid? _pendingTransfersFocusPlayerId;
    private Guid? _pendingScoutingFocusPlayerId;
    private bool _preferLiveBoxScore;

    public FranchiseSession(ImportedLeagueData leagueData, FranchiseStateStore stateStore)
    {
        _leagueData = leagueData;
        _stateStore = stateStore;
        _saveState = _stateStore.Load();
        _saveState.PlayerRatings ??= new Dictionary<Guid, PlayerHiddenRatingsState>();
        _saveState.PlayerSeasonStats ??= new Dictionary<Guid, PlayerSeasonStatsState>();
        _saveState.PreviousSeasonStats ??= new Dictionary<Guid, PlayerSeasonStatsState>();
        _saveState.PlayerRecentGameStats ??= new Dictionary<Guid, List<PlayerRecentGameStatState>>();
        _saveState.PlayerRecentTrackingTotals ??= new Dictionary<Guid, PlayerRecentTotalsState>();
        _saveState.PlayerHealth ??= new Dictionary<Guid, PlayerHealthState>();
        _saveState.PlayerAges ??= new Dictionary<Guid, int>();
        _saveState.PlayerAssignments ??= new Dictionary<Guid, string>();
        _saveState.CreatedPlayers ??= new Dictionary<Guid, FranchiseCreatedPlayerState>();
        _saveState.CompletedScheduleGameKeys ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _saveState.CompletedScheduleGameResults ??= new Dictionary<string, CompletedScheduleGameResult>(StringComparer.OrdinalIgnoreCase);
        _saveState.CompletedGameBoxScores ??= new Dictionary<string, CompletedGameBoxScoreState>(StringComparer.OrdinalIgnoreCase);
        SyncPlayerAgesFromSave();

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
        EnsureSeasonYearInitialized();

        var hasInitializationChanges = false;
        hasInitializationChanges |= EnsurePlayerRatingsGenerated();
        hasInitializationChanges |= EnsurePlayerSeasonStatsGenerated();
        hasInitializationChanges |= EnsureRecentGameTrackingGenerated();
        hasInitializationChanges |= EnsurePlayerHealthGenerated();
        hasInitializationChanges |= EnsurePlayerAssignmentsGenerated();
        hasInitializationChanges |= EnsurePlayerRosterAssignmentsGenerated();
        hasInitializationChanges |= EnsureFortyManRosterGenerated();
        hasInitializationChanges |= EnsurePlayerAgesGenerated();
        hasInitializationChanges |= EnsureCreatedPlayerRosterAssignmentsGenerated();
        hasInitializationChanges |= EnsureDraftCompletionStateConsistency();
        hasInitializationChanges |= EnsureCoachingStaffGenerated();
        hasInitializationChanges |= EnsureScoutingDepartmentGenerated();
        hasInitializationChanges |= EnsureTeamEconomyGenerated();

        if (hasInitializationChanges)
        {
            Save();
        }
    }

    public TeamImportDto? SelectedTeam { get; private set; }

    public string SelectedTeamName => SelectedTeam?.Name ?? "No Team Selected";

    public LiveMatchMode PendingLiveMatchMode { get; private set; }

    public bool IsSelectedTeamMinorLeagueAutomationEnabled()
    {
        return SelectedTeam != null && GetOrCreateTeamState(SelectedTeam.Name).AutoManageMinorLeaguePromotions;
    }

    public string ToggleSelectedTeamMinorLeagueAutomation()
    {
        if (SelectedTeam == null)
        {
            return "Select a team before changing minor-league automation.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        teamState.AutoManageMinorLeaguePromotions = !teamState.AutoManageMinorLeaguePromotions;
        if (teamState.AutoManageMinorLeaguePromotions)
        {
            ApplyAutomaticMinorLeaguePromotions(SelectedTeam.Name);
            RefreshRosterSlots(SelectedTeam.Name);
        }

        Save();
        return teamState.AutoManageMinorLeaguePromotions
            ? "Minor-league AI promotions are on. Unlocked AAA, AA, and A players can move automatically."
            : "Minor-league AI promotions are off. Affiliate tiers will stay fixed until you move players manually or turn AI back on.";
    }

    public bool HasFranchiseSaveData => SelectedTeam != null;

    public bool HasQuickMatchSaveData => _saveState.QuickMatchLiveMatch != null;

    public bool HasAnySaveData =>
        !string.IsNullOrWhiteSpace(_saveState.SelectedTeamName) ||
        _saveState.Teams.Count > 0 ||
        _saveState.CreatedPlayers.Count > 0 ||
        _saveState.ActiveDraft != null ||
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

    public void UpdateScheduleCompactMode(ScheduleCompactMode scheduleCompactMode)
    {
        var displaySettings = GetDisplaySettings();
        if (displaySettings.ScheduleCompactMode == scheduleCompactMode)
        {
            return;
        }

        displaySettings.ScheduleCompactMode = scheduleCompactMode;
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

    public string SetPracticeFocusForDate(DateTime targetDate, TeamPracticeFocus focus)
    {
        if (SelectedTeam == null)
        {
            return "Select a franchise team before changing the practice plan.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        var practiceDateKey = BuildPracticeDateKey(targetDate);
        if (focus == teamState.PracticeFocus)
        {
            teamState.PracticeFocusOverrides.Remove(practiceDateKey);
        }
        else
        {
            teamState.PracticeFocusOverrides[practiceDateKey] = focus;
        }

        Save();
        return $"Practice plan for {targetDate:ddd, MMM d} set to {GetPracticeFocusLabel(focus)}.";
    }

    public string SetPracticeFocusForDates(IEnumerable<DateTime> targetDates, TeamPracticeFocus focus)
    {
        if (SelectedTeam == null)
        {
            return "Select a franchise team before changing the practice plan.";
        }

        var dates = targetDates
            .Select(date => date.Date)
            .Distinct()
            .OrderBy(date => date)
            .ToList();
        if (dates.Count == 0)
        {
            return "Select one or more days first.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        foreach (var date in dates)
        {
            var practiceDateKey = BuildPracticeDateKey(date);
            if (focus == teamState.PracticeFocus)
            {
                teamState.PracticeFocusOverrides.Remove(practiceDateKey);
            }
            else
            {
                teamState.PracticeFocusOverrides[practiceDateKey] = focus;
            }
        }

        Save();
        return $"Applied {GetPracticeFocusLabel(focus)} to {dates.Count} selected day(s).";
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

    public int GetCurrentSeasonYear()
    {
        EnsureSeasonYearInitialized();
        return _saveState.CurrentSeasonYear;
    }

    public bool IsRegularSeasonComplete()
    {
        if (SelectedTeam == null)
        {
            return false;
        }

        var activeSchedule = GetActiveSchedule();
        if (activeSchedule.Count == 0)
        {
            return true;
        }

        var seasonEndDate = activeSchedule.Max(game => game.Date.Date);
        return !activeSchedule.Any(game => !IsScheduledGameCompleted(game)) && GetCurrentFranchiseDate().Date >= seasonEndDate;
    }

    public bool CanAdvanceToNextSeason()
    {
        return IsRegularSeasonComplete() && !HasRequiredDraftWorkRemaining();
    }

    public string GetNextSeasonBlockerMessage()
    {
        if (!IsRegularSeasonComplete())
        {
            return $"Finish the {GetCurrentSeasonYear()} regular season before moving into the offseason.";
        }

        if (_saveState.ActiveDraft != null)
        {
            return "Finish the draft before starting next season.";
        }

        if (!HasCompletedDraftForCurrentSeason())
        {
            return "Run the draft before starting next season.";
        }

        if (HasPendingDraftRosterDecisions())
        {
            return "Resolve your drafted players' 40-man or affiliate assignments before starting next season.";
        }

        return string.Empty;
    }

    public OffseasonSummaryState? GetLastOffseasonSummary()
    {
        return _saveState.LastOffseasonSummary;
    }

    public bool AdvanceToNextSeason(out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before advancing to the next season.";
            return false;
        }

        if (!CanAdvanceToNextSeason())
        {
            statusMessage = GetNextSeasonBlockerMessage();
            return false;
        }

        RunOffseasonSimulation(out var offseasonSummary);
        _saveState.LastOffseasonSummary = offseasonSummary;
        StoreOffseasonReports(offseasonSummary);
        Save();
        statusMessage = offseasonSummary.Overview;
        return true;
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

    public IReadOnlyList<ScheduleImportDto> GetSelectedTeamSeasonSchedule()
    {
        return GetSeasonSchedule();
    }

    public bool CanStartDraft()
    {
        return SelectedTeam != null
            && _saveState.ActiveDraft == null
            && IsRegularSeasonComplete()
            && !HasCompletedDraftForCurrentSeason();
    }

    public bool HasActiveDraft()
    {
        return _saveState.ActiveDraft != null;
    }

    public DraftBoardView GetDraftBoard()
    {
        var draftState = LoadDraftState();
        if (draftState == null)
        {
            return new DraftBoardView(false, false, 0, 0, 0, string.Empty, [], [], []);
        }

        var currentRoundOrder = draftState.IsComplete
            ? Array.Empty<string>()
            : draftState.GetDraftOrderForRound(draftState.CurrentRound).ToArray();

        return new DraftBoardView(
            true,
            draftState.IsComplete,
            draftState.TotalRounds,
            draftState.CurrentRound,
            draftState.CurrentPickNumber,
            draftState.CurrentTeamName,
            currentRoundOrder,
            draftState.AvailableProspects
                .Select(prospect => new DraftProspectView(
                    prospect.PlayerId,
                    prospect.PlayerName,
                    prospect.PrimaryPosition,
                    prospect.SecondaryPosition,
                    prospect.Age,
                    prospect.Source,
                    prospect.Summary,
                    prospect.ScoutSummary,
                    prospect.PotentialSummary,
                    prospect.SourceTeamName,
                    prospect.SourceStatsSummary,
                    prospect.OverallRating,
                    prospect.PotentialRating))
                .ToList(),
            draftState.DraftedPicks
                .OrderByDescending(pick => pick.OverallPickNumber)
                .Take(12)
                .Select(pick => new DraftPickView(
                    pick.RoundNumber,
                    pick.PickNumberInRound,
                    pick.OverallPickNumber,
                    pick.TeamName,
                    pick.PlayerName,
                    pick.PrimaryPosition,
                    pick.IsUserPick))
                .ToList());
    }

    public IReadOnlyList<DraftOrganizationPlayerView> GetDraftOrganizationPlayers()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamName = SelectedTeam.Name;
        var currentSeasonYear = GetCurrentSeasonYear();
        return _saveState.CreatedPlayers.Values
            .Where(player => string.Equals(player.TeamName, teamName, StringComparison.OrdinalIgnoreCase))
            .Where(player => player.DraftSeasonYear == currentSeasonYear)
            .OrderByDescending(player => player.RequiresRosterDecision)
            .ThenByDescending(player => player.DraftSeasonYear)
            .ThenByDescending(player => player.DraftOverallRating)
            .ThenBy(player => player.FullName)
            .Select(player => new DraftOrganizationPlayerView(
                player.PlayerId,
                player.FullName,
                player.PrimaryPosition,
                player.SecondaryPosition,
                player.Age,
                player.ScoutSummary,
                player.PotentialSummary,
                player.Source,
                player.SourceTeamName,
                player.SourceStatsSummary,
                GetRosterAssignmentLabel(player),
                player.RequiresRosterDecision,
                player.MinorLeagueOptionsRemaining,
                player.DraftOverallRating,
                player.PotentialRating))
            .ToList();
    }

    public IReadOnlyList<DraftClassHistoryView> GetRecentDraftClasses(int maxClasses = 5)
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamName = SelectedTeam.Name;
        return _saveState.CreatedPlayers.Values
            .Where(player => string.Equals(player.TeamName, teamName, StringComparison.OrdinalIgnoreCase))
            .GroupBy(player => player.DraftSeasonYear)
            .OrderByDescending(group => group.Key)
            .Take(Math.Max(1, maxClasses))
            .Select(group =>
            {
                var players = group
                    .OrderByDescending(player => player.DraftOverallRating)
                    .ThenByDescending(player => player.PotentialRating)
                    .ThenBy(player => player.FullName)
                    .Select(player =>
                    {
                        var assignmentLabel = GetRosterAssignmentLabel(player);
                        var isOnFortyMan = string.Equals(player.RosterAssignment, MajorLeagueRosterAssignment, StringComparison.OrdinalIgnoreCase);
                        return new DraftClassHistoryPlayerView(
                            player.PlayerId,
                            player.FullName,
                            player.PrimaryPosition,
                            player.SecondaryPosition,
                            player.Age,
                            player.DraftOverallRating,
                            player.PotentialRating,
                            player.Source,
                            assignmentLabel,
                            isOnFortyMan);
                    })
                    .ToList();

                var fortyManCount = players.Count(player => player.IsOnFortyMan);
                var affiliateLevels = group.Select(player => GetAffiliateLevel(player.RosterAssignment)).ToList();
                var affiliateCount = affiliateLevels.Count(level => level.HasValue);
                var organizationCount = players.Count(player => string.Equals(player.AssignmentLabel, ReserveRosterAssignment, StringComparison.OrdinalIgnoreCase));
                var summary = $"{players.Count} signed player(s): {fortyManCount} on 40-man, {organizationCount} in organization depth, {BuildAffiliateSummary(affiliateCount, affiliateLevels)}.";

                return new DraftClassHistoryView(
                    group.Key,
                    $"{group.Key} Draft Class",
                    summary,
                    players.Count,
                    fortyManCount,
                    affiliateCount,
                    organizationCount,
                    players);
            })
            .ToList();
    }

    public IReadOnlyList<DraftFortyManPlayerView> GetDraftFortyManRoster()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        return GetSelectedTeamOrganizationRoster()
            .Where(player => player.IsOnFortyMan)
            .OrderBy(player => player.PlayerName)
            .Select(player =>
            {
                var isDraftedPlayer = _saveState.CreatedPlayers.TryGetValue(player.PlayerId, out var createdPlayer);
                return new DraftFortyManPlayerView(
                    player.PlayerId,
                    player.PlayerName,
                    player.PrimaryPosition,
                    player.SecondaryPosition,
                    player.Age,
                    isDraftedPlayer ? "Draft pick on 40-man" : "Current 40-man player",
                    isDraftedPlayer,
                    isDraftedPlayer ? createdPlayer!.MinorLeagueOptionsRemaining : 0);
            })
            .ToList();
    }

    public int GetSelectedTeam40ManCount()
    {
        return SelectedTeam == null ? 0 : GetTeamFortyManCount(SelectedTeam.Name);
    }

    public OrganizationRosterCompositionView GetSelectedTeamRosterComposition(OrganizationRosterCompositionMode mode)
    {
        var title = mode switch
        {
            OrganizationRosterCompositionMode.Depth => "Depth",
            OrganizationRosterCompositionMode.Affiliate => "Affiliates",
            _ => "First Team"
        };

        if (SelectedTeam == null)
        {
            return CreateEmptyRosterComposition(
                title,
                mode == OrganizationRosterCompositionMode.FirstTeam
                    ? "Structured around the MLB-style 26-man mix."
                    : "No team is selected.",
                mode == OrganizationRosterCompositionMode.FirstTeam ? FirstTeamRosterSize : null);
        }

        var organizationPlayers = GetSelectedTeamOrganizationRoster();
        var firstTeamPlayers = SelectFirstTeamPlayers(organizationPlayers);
        var firstTeamPlayerIds = firstTeamPlayers.Select(player => player.PlayerId).ToHashSet();

        return mode switch
        {
            OrganizationRosterCompositionMode.Depth => BuildRosterComposition(
                "Depth",
                "40-man extras and organization depth outside the current 26-man mix.",
                organizationPlayers.Where(player => !firstTeamPlayerIds.Contains(player.PlayerId) && !player.AffiliateLevel.HasValue).ToList(),
                null),
            OrganizationRosterCompositionMode.Affiliate => BuildRosterComposition(
                "Affiliates",
                $"Players currently assigned across the {SelectedTeam.Name} AAA, AA, and A affiliates. {BuildAffiliateBreakdownLabel(organizationPlayers)}",
                organizationPlayers.Where(player => player.AffiliateLevel.HasValue).ToList(),
                null),
            _ => BuildRosterComposition(
                "First Team",
                "Structured like the MLB 26-man split: 13 pitchers, 2 catchers, 6 infielders, 5 outfielders.",
                firstTeamPlayers,
                FirstTeamRosterSize)
        };
    }

    public IReadOnlyList<OrganizationRosterPlayerView> GetSelectedTeamOrganizationRoster()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamName = SelectedTeam.Name;
        var rostersByPlayerId = _leagueData.Rosters
            .GroupBy(roster => roster.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());
        var lineupMap = BuildSlotMap(BuildLineupSlots(teamName));
        var rotationMap = BuildSlotMap(BuildRotationSlots(teamName));

        var organizationPlayers = GetAllPlayers()
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .Select(player =>
            {
                rostersByPlayerId.TryGetValue(player.PlayerId, out var roster);
                var playerName = roster?.PlayerName ?? player.FullName;
                var primaryPosition = string.IsNullOrWhiteSpace(roster?.PrimaryPosition) ? player.PrimaryPosition : roster.PrimaryPosition;
                var secondaryPosition = string.IsNullOrWhiteSpace(roster?.SecondaryPosition) ? player.SecondaryPosition : roster.SecondaryPosition;
                var isDraftedPlayer = _saveState.CreatedPlayers.TryGetValue(player.PlayerId, out var createdPlayer);
                var rosterAssignment = GetPlayerRosterAssignment(player.PlayerId);
                var assignmentLabel = GetRosterAssignmentLabel(teamName, rosterAssignment);
                var isOnFortyMan = IsPlayerOnFortyMan(player.PlayerId);
                var isOnActiveRoster = IsPlayerOnActiveTeamRoster(player.PlayerId);
                var affiliateLevel = GetAffiliateLevel(rosterAssignment);
                var isMinorLeagueAssignmentLocked = affiliateLevel.HasValue && IsMinorLeagueAssignmentLocked(teamName, player.PlayerId);
                var canAssignToFortyMan = !isOnFortyMan;

                return new OrganizationRosterPlayerView(
                    player.PlayerId,
                    playerName,
                    primaryPosition,
                    secondaryPosition,
                    player.Age,
                    assignmentLabel,
                    string.Empty,
                    affiliateLevel,
                        isMinorLeagueAssignmentLocked,
                    isOnFortyMan,
                    false,
                    isDraftedPlayer,
                    isDraftedPlayer ? createdPlayer!.MinorLeagueOptionsRemaining : 0,
                    canAssignToFortyMan,
                        affiliateLevel.HasValue && isMinorLeagueAssignmentLocked,
                    true,
                    isOnActiveRoster && lineupMap.TryGetValue(player.PlayerId, out var lineupSlot) ? lineupSlot : null,
                    isOnActiveRoster && rotationMap.TryGetValue(player.PlayerId, out var rotationSlot) ? rotationSlot : null);
            })
                    .ToList();

        var firstTeamPlayerIds = SelectFirstTeamPlayers(organizationPlayers)
            .Select(player => player.PlayerId)
            .ToHashSet();

        return organizationPlayers
            .Select(player => player with
            {
                TeamStatusLabel = firstTeamPlayerIds.Contains(player.PlayerId)
                    ? "First Team"
                    : player.AffiliateLevel.HasValue
                        ? $"{GetAffiliateLevelLabel(player.AffiliateLevel.Value)} Affiliate"
                        : "Organization Depth",
                IsOnFirstTeam = firstTeamPlayerIds.Contains(player.PlayerId)
            })
            .OrderByDescending(player => player.AssignmentLabel == ReserveRosterAssignment)
                    .ThenByDescending(player => player.AssignmentLabel == "Decision Needed")
            .ThenByDescending(player => player.IsOnFortyMan)
            .ThenBy(player => player.PrimaryPosition)
            .ThenBy(player => player.PlayerName)
            .ToList();
    }

    public bool AssignSelectedTeamPlayersToFortyMan(IReadOnlyCollection<Guid> playerIds, out string statusMessage)
    {
        return ProcessBatchRosterMove(playerIds, AssignSelectedTeamPlayerToFortyMan, "added to the 40-man roster", out statusMessage);
    }

    public bool AssignSelectedTeamPlayersToAffiliate(IReadOnlyCollection<Guid> playerIds, MinorLeagueAffiliateLevel affiliateLevel, out string statusMessage)
    {
        var affiliateLabel = GetAffiliateLevelLabel(affiliateLevel);
        return ProcessBatchRosterMove(playerIds, (Guid playerId, out string message) => AssignSelectedTeamPlayerToAffiliate(playerId, affiliateLevel, out message), $"assigned to the {affiliateLabel} affiliate", out statusMessage);
    }

    public bool ReturnSelectedTeamPlayersToAutomaticAffiliate(IReadOnlyCollection<Guid> playerIds, out string statusMessage)
    {
        return ProcessBatchRosterMove(playerIds, ReturnSelectedTeamPlayerToAutomaticAffiliate, "returned to automatic minor-league management", out statusMessage);
    }

    public bool RemoveSelectedTeamPlayersFromFortyMan(IReadOnlyCollection<Guid> playerIds, out string statusMessage)
    {
        return ProcessBatchRosterMove(playerIds, RemoveSelectedTeamPlayerFromFortyMan, "removed from the 40-man roster", out statusMessage);
    }

    private OrganizationRosterCompositionView CreateEmptyRosterComposition(string title, string summary, int? targetCount)
    {
        return new OrganizationRosterCompositionView(
            title,
            summary,
            0,
            targetCount,
            BuildRosterCompositionBuckets([], targetCount));
    }

    private OrganizationRosterCompositionView BuildRosterComposition(string title, string summary, IReadOnlyList<OrganizationRosterPlayerView> players, int? targetCount)
    {
        return new OrganizationRosterCompositionView(
            title,
            summary,
            players.Count,
            targetCount,
            BuildRosterCompositionBuckets(players, targetCount));
    }

    private IReadOnlyList<OrganizationRosterCompositionBucketView> BuildRosterCompositionBuckets(IReadOnlyList<OrganizationRosterPlayerView> players, int? targetCount)
    {
        var playersByBucket = players
            .GroupBy(GetRosterCompositionBucket)
            .ToDictionary(group => group.Key, group => group.Count());

        var playersByPrimaryPosition = players
            .GroupBy(player => player.PrimaryPosition, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var buckets = new List<OrganizationRosterCompositionBucketView>
        {
            CreateRosterCompositionBucket(RosterCompositionBucket.Pitchers, "Pitchers", playersByBucket, targetCount == FirstTeamRosterSize ? 13 : null),
            CreateRosterCompositionBucket(RosterCompositionBucket.Catchers, "Catchers", playersByBucket, targetCount == FirstTeamRosterSize ? 2 : null),
            CreateRosterCompositionBucket(
                RosterCompositionBucket.Infielders,
                "Infielders",
                playersByBucket,
                targetCount == FirstTeamRosterSize ? 6 : null,
                BuildPrimaryPositionDetails(playersByPrimaryPosition, ["1B", "2B", "3B", "SS"])),
            CreateRosterCompositionBucket(
                RosterCompositionBucket.Outfielders,
                "Outfielders",
                playersByBucket,
                targetCount == FirstTeamRosterSize ? 5 : null,
                BuildPrimaryPositionDetails(playersByPrimaryPosition, ["LF", "CF", "RF"]))
        };

        var utilityCount = playersByBucket.GetValueOrDefault(RosterCompositionBucket.Utility);
        if (utilityCount > 0 || players.Count == 0)
        {
            buckets.Add(new OrganizationRosterCompositionBucketView("Utility", utilityCount, null));
        }

        return buckets;
    }

    private static OrganizationRosterCompositionBucketView CreateRosterCompositionBucket(
        RosterCompositionBucket bucket,
        string label,
        IReadOnlyDictionary<RosterCompositionBucket, int> playersByBucket,
        int? targetCount,
        IReadOnlyList<OrganizationRosterCompositionBucketView>? details = null)
    {
        return new OrganizationRosterCompositionBucketView(label, playersByBucket.GetValueOrDefault(bucket), targetCount, details);
    }

    private static IReadOnlyList<OrganizationRosterCompositionBucketView> BuildPrimaryPositionDetails(
        IReadOnlyDictionary<string, int> playersByPrimaryPosition,
        IReadOnlyList<string> positions)
    {
        return positions
            .Select(position => new OrganizationRosterCompositionBucketView(position, playersByPrimaryPosition.GetValueOrDefault(position), null))
            .ToList();
    }

    private List<OrganizationRosterPlayerView> SelectFirstTeamPlayers(IReadOnlyList<OrganizationRosterPlayerView> organizationPlayers)
    {
        var availablePlayers = organizationPlayers
            .Where(player => player.IsOnFortyMan && string.Equals(GetPlayerRosterAssignment(player.PlayerId), MajorLeagueRosterAssignment, StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetFirstTeamPriority)
            .ThenBy(player => player.PrimaryPosition)
            .ThenBy(player => player.PlayerName)
            .ToList();

        var selectedPlayers = new List<OrganizationRosterPlayerView>();
        var selectedPlayerIds = new HashSet<Guid>();

        AddRosterCompositionPlayers(selectedPlayers, selectedPlayerIds, availablePlayers.Where(player => IsPitcherPosition(player.PrimaryPosition)), 13);
        AddRosterCompositionPlayers(selectedPlayers, selectedPlayerIds, availablePlayers.Where(IsCatcher), 2);
        AddRosterCompositionPlayers(selectedPlayers, selectedPlayerIds, availablePlayers.Where(IsInfielder), 6);
        AddRosterCompositionPlayers(selectedPlayers, selectedPlayerIds, availablePlayers.Where(IsOutfielder), 5);
        AddRosterCompositionPlayers(selectedPlayers, selectedPlayerIds, availablePlayers, FirstTeamRosterSize - selectedPlayers.Count);

        return selectedPlayers;
    }

    private static void AddRosterCompositionPlayers(
        List<OrganizationRosterPlayerView> selectedPlayers,
        HashSet<Guid> selectedPlayerIds,
        IEnumerable<OrganizationRosterPlayerView> candidates,
        int playerCount)
    {
        if (playerCount <= 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            if (selectedPlayers.Count >= FirstTeamRosterSize || playerCount <= 0 || !selectedPlayerIds.Add(candidate.PlayerId))
            {
                continue;
            }

            selectedPlayers.Add(candidate);
            playerCount--;
        }
    }

    private static int GetFirstTeamPriority(OrganizationRosterPlayerView player)
    {
        if (player.RotationSlot.HasValue)
        {
            return 0;
        }

        if (player.LineupSlot.HasValue)
        {
            return 1;
        }

        return player.IsOnFortyMan ? 2 : 3;
    }

    private static RosterCompositionBucket GetRosterCompositionBucket(OrganizationRosterPlayerView player)
    {
        if (IsPitcherPosition(player.PrimaryPosition))
        {
            return RosterCompositionBucket.Pitchers;
        }

        if (IsCatcher(player))
        {
            return RosterCompositionBucket.Catchers;
        }

        if (IsInfielder(player))
        {
            return RosterCompositionBucket.Infielders;
        }

        if (IsOutfielder(player))
        {
            return RosterCompositionBucket.Outfielders;
        }

        return RosterCompositionBucket.Utility;
    }

    private static bool IsCatcher(OrganizationRosterPlayerView player)
    {
        return HasPosition(player, "C");
    }

    private static bool IsInfielder(OrganizationRosterPlayerView player)
    {
        return HasAnyPosition(player, ["1B", "2B", "3B", "SS", "IF"]);
    }

    private static bool IsOutfielder(OrganizationRosterPlayerView player)
    {
        return HasAnyPosition(player, ["LF", "CF", "RF", "OF"]);
    }

    private static bool HasAnyPosition(OrganizationRosterPlayerView player, IReadOnlyList<string> positions)
    {
        return positions.Any(position => HasPosition(player, position));
    }

    private static bool HasPosition(OrganizationRosterPlayerView player, string position)
    {
        return string.Equals(player.PrimaryPosition, position, StringComparison.OrdinalIgnoreCase)
            || string.Equals(player.SecondaryPosition, position, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPitcherPosition(string position)
    {
        return position is "SP" or "RP";
    }

    public bool IsSelectedTeam40ManFull()
    {
        return SelectedTeam != null && GetTeamFortyManCount(SelectedTeam.Name) >= MaxRosterSize;
    }

    public bool AssignSelectedTeamPlayerToFortyMan(Guid playerId, out string statusMessage)
    {
        if (!TryGetSelectedTeamPlayer(playerId, out var player, out statusMessage))
        {
            return false;
        }

        var currentAssignment = GetPlayerRosterAssignment(playerId);
        if (string.Equals(currentAssignment, MajorLeagueRosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = $"{player.FullName} is already on the 40-man roster.";
            return false;
        }

        if (GetTeamFortyManCount(SelectedTeam!.Name) >= MaxRosterSize)
        {
            statusMessage = $"The {SelectedTeam.Name} already have {MaxRosterSize} players on the 40-man roster. Move or release someone to make space.";
            return false;
        }

        AddPlayerToFortyManRoster(playerId);
        SetMinorLeagueAssignmentLock(SelectedTeam.Name, playerId, false);
        if (_saveState.CreatedPlayers.TryGetValue(playerId, out var createdPlayer))
        {
            createdPlayer.RequiresRosterDecision = false;
        }

        RefreshRosterSlots(SelectedTeam.Name);
        Save();
        AddTransferRecord(SelectedTeam.Name, $"Added {player.FullName} to the 40-man roster.");
        statusMessage = $"{player.FullName} is now on the 40-man roster.";
        return true;
    }

    public bool AssignSelectedTeamPlayerToAffiliate(Guid playerId, MinorLeagueAffiliateLevel affiliateLevel, out string statusMessage)
    {
        if (!TryGetSelectedTeamPlayer(playerId, out var player, out statusMessage))
        {
            return false;
        }

        var currentAssignment = GetPlayerRosterAssignment(playerId);
        var targetAssignment = GetRosterAssignmentForAffiliateLevel(affiliateLevel);
        var affiliateLabel = GetAffiliateLevelLabel(affiliateLevel);
        if (string.Equals(currentAssignment, targetAssignment, StringComparison.OrdinalIgnoreCase))
        {
            if (IsMinorLeagueAssignmentLocked(SelectedTeam!.Name, playerId))
            {
                statusMessage = $"{player.FullName} is already locked to the {affiliateLabel} affiliate.";
                return false;
            }

            SetMinorLeagueAssignmentLock(SelectedTeam.Name, playerId, true);
            Save();
            statusMessage = $"{player.FullName} is now locked to the {affiliateLabel} affiliate.";
            return true;
        }

        if (_saveState.CreatedPlayers.TryGetValue(playerId, out var createdPlayer)
            && IsPlayerOnFortyMan(playerId)
            && string.Equals(currentAssignment, MajorLeagueRosterAssignment, StringComparison.OrdinalIgnoreCase)
            && createdPlayer.LastOptionedSeasonYear != GetCurrentSeasonYear())
        {
            if (createdPlayer.MinorLeagueOptionsRemaining <= 0)
            {
                statusMessage = $"{createdPlayer.FullName} has no minor-league option years remaining.";
                return false;
            }

            createdPlayer.MinorLeagueOptionsRemaining--;
            createdPlayer.LastOptionedSeasonYear = GetCurrentSeasonYear();
        }

        SetPlayerRosterAssignment(playerId, targetAssignment);
        SetMinorLeagueAssignmentLock(SelectedTeam!.Name, playerId, true);
        if (_saveState.CreatedPlayers.TryGetValue(playerId, out createdPlayer))
        {
            createdPlayer.RequiresRosterDecision = false;
        }

        RefreshRosterSlots(SelectedTeam!.Name);
        Save();
        AddTransferRecord(SelectedTeam.Name, $"Assigned {player.FullName} to the {affiliateLabel} affiliate.");
        statusMessage = IsPlayerOnFortyMan(playerId)
            ? $"{player.FullName} was assigned to the {SelectedTeam.Name} {affiliateLabel} affiliate and remains on the 40-man roster."
            : $"{player.FullName} was assigned to the {SelectedTeam.Name} {affiliateLabel} affiliate.";
        return true;
    }

    public bool AssignDraftPlayerTo40Man(Guid playerId, out string statusMessage)
    {
        return AssignSelectedTeamPlayerToFortyMan(playerId, out statusMessage);
    }

    public bool AssignDraftPlayerToAffiliate(Guid playerId, MinorLeagueAffiliateLevel affiliateLevel, out string statusMessage)
    {
        return AssignSelectedTeamPlayerToAffiliate(playerId, affiliateLevel, out statusMessage);
    }

    public bool ReturnSelectedTeamPlayerToAutomaticAffiliate(Guid playerId, out string statusMessage)
    {
        if (!TryGetSelectedTeamPlayer(playerId, out var player, out statusMessage))
        {
            return false;
        }

        var currentLevel = GetAffiliateLevel(GetPlayerRosterAssignment(playerId));
        if (!currentLevel.HasValue)
        {
            statusMessage = $"{player.FullName} is not currently on a minor-league affiliate.";
            return false;
        }

        if (!IsMinorLeagueAssignmentLocked(SelectedTeam!.Name, playerId))
        {
            statusMessage = $"{player.FullName} is already under automatic minor-league management.";
            return false;
        }

        SetMinorLeagueAssignmentLock(SelectedTeam.Name, playerId, false);
        var movedByAi = ApplyAutomaticMinorLeaguePromotions(SelectedTeam.Name);
        RefreshRosterSlots(SelectedTeam.Name);
        Save();

        var updatedLevel = GetAffiliateLevel(GetPlayerRosterAssignment(playerId)) ?? currentLevel.Value;
        statusMessage = movedByAi && updatedLevel != currentLevel.Value
            ? $"{player.FullName} is back under AI control and moved to {GetAffiliateLevelLabel(updatedLevel)}."
            : $"{player.FullName} is back under AI control on {GetAffiliateLevelLabel(updatedLevel)}.";
        return true;
    }

    public bool ReleaseSelectedTeam40ManPlayer(Guid playerId, out string statusMessage)
    {
        return ReleaseSelectedTeamPlayer(playerId, out statusMessage);
    }

    public bool RemoveSelectedTeamPlayerFromFortyMan(Guid playerId, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before managing the 40-man roster.";
            return false;
        }

        if (!TryGetSelectedTeamPlayer(playerId, out var player, out statusMessage))
        {
            return false;
        }

        if (!IsPlayerOnFortyMan(playerId))
        {
            statusMessage = $"{player.FullName} is not currently on the 40-man roster.";
            return false;
        }

        RemovePlayerFromFortyManRoster(playerId);
        if (string.Equals(GetPlayerRosterAssignment(playerId), MajorLeagueRosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            SetPlayerRosterAssignment(playerId, ReserveRosterAssignment);
        }

        SetMinorLeagueAssignmentLock(SelectedTeam.Name, playerId, false);
        if (_saveState.CreatedPlayers.TryGetValue(playerId, out var createdPlayer))
        {
            createdPlayer.RequiresRosterDecision = false;
        }

        RefreshRosterSlots(SelectedTeam.Name);
        Save();
        AddTransferRecord(SelectedTeam.Name, $"Removed {player.FullName} from the 40-man roster.");
        statusMessage = $"{player.FullName} was removed from the 40-man roster and remains with the organization.";
        return true;
    }

    public bool ReleaseSelectedTeamPlayer(Guid playerId, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before managing the roster.";
            return false;
        }

        var player = FindPlayerImport(playerId);
        if (player == null || !string.Equals(GetAssignedTeamName(playerId, player.TeamName), SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = "That player is not on your roster.";
            return false;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        var releaseCost = _releasePlayerUseCase.Execute(teamState.Economy, playerId);
        _saveState.PlayerAssignments[playerId] = FreeAgentTeamName;
        _saveState.PlayerRosterAssignments.Remove(playerId);
        SetMinorLeagueAssignmentLock(SelectedTeam.Name, playerId, false);

        if (_saveState.CreatedPlayers.TryGetValue(playerId, out var createdPlayer))
        {
            createdPlayer.TeamName = FreeAgentTeamName;
            createdPlayer.RequiresRosterDecision = false;
            createdPlayer.RosterAssignment = ReserveRosterAssignment;
        }

        RemovePlayerFromFortyManRoster(playerId);

        RefreshRosterSlots(SelectedTeam.Name);
        AddTransferRecord(SelectedTeam.Name, $"Released {player.FullName} from the roster.");
        Save();

        statusMessage = releaseCost > 0m
            ? $"Released {player.FullName}. Buyout cost: {GetBudgetDisplay(releaseCost)}."
            : $"Released {player.FullName}.";
        return true;
    }

    private bool ProcessBatchRosterMove(
        IReadOnlyCollection<Guid> playerIds,
        BatchRosterMoveOperation operation,
        string successLabel,
        out string statusMessage)
    {
        if (playerIds.Count == 0)
        {
            statusMessage = "Select at least one player before making a roster move.";
            return false;
        }

        var successCount = 0;
        var failureMessages = new List<string>();

        foreach (var playerId in playerIds.Distinct())
        {
            if (operation(playerId, out var message))
            {
                successCount++;
            }
            else if (!string.IsNullOrWhiteSpace(message))
            {
                failureMessages.Add(message);
            }
        }

        if (successCount == 0)
        {
            statusMessage = failureMessages.Count == 0 ? "No players were updated." : failureMessages[0];
            return false;
        }

        statusMessage = successCount == 1
            ? $"1 player was {successLabel}."
            : $"{successCount} players were {successLabel}.";

        if (failureMessages.Count > 0)
        {
            statusMessage = $"{statusMessage} {failureMessages.Count} move(s) were skipped. {failureMessages[0]}";
        }

        return true;
    }

    public string GetDraftProspectDebugSummary(Guid playerId)
    {
        var draftState = LoadDraftState();
        var prospect = draftState?.FindProspect(playerId);
        if (prospect != null)
        {
            return $"Debug - OVR {prospect.OverallRating}, POT {prospect.PotentialRating}, outcome {prospect.TalentOutcome}.";
        }

        if (_saveState.CreatedPlayers.TryGetValue(playerId, out var createdPlayer))
        {
            return $"Debug - OVR {createdPlayer.DraftOverallRating}, POT {createdPlayer.PotentialRating}, outcome {createdPlayer.TalentOutcome}.";
        }

        return "Debug data unavailable.";
    }

    public bool StartDraft(out string statusMessage, int totalRounds = DefaultDraftRounds, bool isSnakeDraft = false)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before starting the draft.";
            return false;
        }

        if (_saveState.ActiveDraft != null)
        {
            statusMessage = "A draft is already in progress.";
            return false;
        }

        if (!IsRegularSeasonComplete())
        {
            statusMessage = "Finish the regular season before opening the draft.";
            return false;
        }

        if (HasCompletedDraftForCurrentSeason())
        {
            statusMessage = "The current season draft has already been completed.";
            return false;
        }

        var draftOrder = BuildDraftOrder();
        var draftProspects = _draftProspectFactory.CreateProspects(GetCurrentSeasonYear(), Math.Max(draftOrder.Count * Math.Max(1, totalRounds) + 24, 60));
        foreach (var prospect in draftProspects)
        {
            EnsureDraftCreatedPlayer(prospect);
        }

        var draftState = _startDraftUseCase.Execute(draftOrder, draftProspects, totalRounds, isSnakeDraft);
        SaveDraftState(draftState);
        Save();
        statusMessage = $"Draft started: {draftOrder.Count} teams, {totalRounds} rounds, {draftProspects.Count} available prospects.";
        return true;
    }

    public bool MakeDraftPick(Guid playerId, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before making a draft pick.";
            return false;
        }

        var draftState = LoadDraftState();
        if (draftState == null)
        {
            statusMessage = "Start a draft before making a pick.";
            return false;
        }

        try
        {
            var pick = _makeUserDraftPickUseCase.Execute(draftState, SelectedTeam.Name, playerId);
            ApplyDraftPick(pick);
            return FinalizeDraftProgress(draftState, $"{pick.TeamName} selected {pick.PlayerName} ({pick.PrimaryPosition}).", out statusMessage);
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
            return false;
        }
    }

    public bool SimulateCpuDraftPick(out string statusMessage)
    {
        return AdvanceDraft(out statusMessage);
    }

    public bool AdvanceDraftToNextUserPick(out string statusMessage)
    {
        var draftState = LoadDraftState();
        if (draftState == null)
        {
            statusMessage = "There is no active draft to advance.";
            return false;
        }

        var userTeamName = SelectedTeam?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userTeamName))
        {
            statusMessage = "Select a team before advancing the draft.";
            return false;
        }

        if (draftState.IsTeamOnClock(userTeamName))
        {
            statusMessage = $"The {userTeamName} are already on the clock.";
            return false;
        }

        var lastMessage = string.Empty;
        while (!draftState.IsComplete && !draftState.IsTeamOnClock(userTeamName))
        {
            var result = _advanceDraftUseCase.Execute(draftState, userTeamName, BuildDraftCpuTeamContext);
            if (result.PickMade == null)
            {
                statusMessage = string.IsNullOrWhiteSpace(result.Message) ? "Draft advance stopped unexpectedly." : result.Message;
                return false;
            }

            ApplyDraftPick(result.PickMade);
            lastMessage = result.Message;
        }

        return FinalizeDraftProgress(draftState, string.IsNullOrWhiteSpace(lastMessage) ? "Draft advanced." : lastMessage, out statusMessage);
    }

    public bool AdvanceDraft(out string statusMessage)
    {
        var draftState = LoadDraftState();
        if (draftState == null)
        {
            statusMessage = "There is no active draft to advance.";
            return false;
        }

        var userTeamName = SelectedTeam?.Name ?? string.Empty;

        try
        {
            var result = _advanceDraftUseCase.Execute(draftState, userTeamName, BuildDraftCpuTeamContext);
            if (result.PickMade == null)
            {
                statusMessage = result.Message;
                return false;
            }

            ApplyDraftPick(result.PickMade);
            return FinalizeDraftProgress(draftState, result.Message, out statusMessage);
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
            return false;
        }
    }

    public bool AutoDraftPick(out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before using auto pick.";
            return false;
        }

        var draftState = LoadDraftState();
        if (draftState == null)
        {
            statusMessage = "There is no active draft to auto pick.";
            return false;
        }

        var userTeamName = SelectedTeam.Name;
        if (!draftState.IsTeamOnClock(userTeamName))
        {
            statusMessage = $"The {userTeamName} are not on the clock.";
            return false;
        }

        try
        {
            var pick = MakeAutoUserDraftPick(draftState, userTeamName);
            ApplyDraftPick(pick);
            return FinalizeDraftProgress(draftState, $"{pick.TeamName} auto-picked {pick.PlayerName} ({pick.PrimaryPosition}).", out statusMessage);
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
            return false;
        }
    }

    public bool SimDraftRound(out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before simming a round.";
            return false;
        }

        var draftState = LoadDraftState();
        if (draftState == null)
        {
            statusMessage = "There is no active draft to sim.";
            return false;
        }

        var userTeamName = SelectedTeam.Name;
        var startingRound = draftState.CurrentRound;
        var userSelections = new List<string>();
        var picksSimmed = 0;

        try
        {
            while (!draftState.IsComplete && draftState.CurrentRound == startingRound)
            {
                if (draftState.IsTeamOnClock(userTeamName))
                {
                    var pick = MakeAutoUserDraftPick(draftState, userTeamName);
                    ApplyDraftPick(pick);
                    userSelections.Add($"{pick.PlayerName} ({pick.PrimaryPosition})");
                }
                else
                {
                    var result = _advanceDraftUseCase.Execute(draftState, userTeamName, BuildDraftCpuTeamContext);
                    if (result.PickMade == null)
                    {
                        statusMessage = string.IsNullOrWhiteSpace(result.Message) ? "Round sim stopped unexpectedly." : result.Message;
                        return false;
                    }

                    ApplyDraftPick(result.PickMade);
                }

                picksSimmed++;
            }

            var summary = userSelections.Count == 0
                ? $"Simmed Round {startingRound}: {picksSimmed} pick(s)."
                : $"Simmed Round {startingRound}: {picksSimmed} pick(s). {userTeamName} selected {string.Join(", ", userSelections)}.";
            return FinalizeDraftProgress(draftState, summary, out statusMessage);
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
            return false;
        }
    }

    public bool AutoDraft(out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before using auto finish draft.";
            return false;
        }

        var draftState = LoadDraftState();
        if (draftState == null)
        {
            statusMessage = "There is no active draft to auto finish.";
            return false;
        }

        var userTeamName = SelectedTeam.Name;
        var userSelections = new List<string>();
        var lastMessage = string.Empty;

        try
        {
            while (!draftState.IsComplete)
            {
                if (draftState.IsTeamOnClock(userTeamName))
                {
                    var pick = MakeAutoUserDraftPick(draftState, userTeamName);
                    ApplyDraftPick(pick);
                    userSelections.Add($"{pick.PlayerName} ({pick.PrimaryPosition})");
                    lastMessage = $"{pick.TeamName} auto-drafted {pick.PlayerName} ({pick.PrimaryPosition}).";
                    continue;
                }

                var result = _advanceDraftUseCase.Execute(draftState, userTeamName, BuildDraftCpuTeamContext);
                if (result.PickMade == null)
                {
                    statusMessage = string.IsNullOrWhiteSpace(result.Message) ? "Auto finish draft stopped unexpectedly." : result.Message;
                    return false;
                }

                ApplyDraftPick(result.PickMade);
                lastMessage = result.Message;
            }

            var summary = userSelections.Count == 0
                ? "Auto finish draft completed."
                : $"Auto finish draft completed for {userTeamName}. Picks: {string.Join(", ", userSelections.Take(5))}{(userSelections.Count > 5 ? ", ..." : string.Empty)}.";
            return FinalizeDraftProgress(draftState, string.IsNullOrWhiteSpace(lastMessage) ? summary : summary, out statusMessage);
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
            return false;
        }
    }

    private DraftPick MakeAutoUserDraftPick(DraftState draftState, string userTeamName)
    {
        var teamContext = BuildDraftCpuTeamContext(userTeamName);
        var prospect = _draftCpuPicker.SelectProspect(draftState, teamContext);
        return draftState.MakePick(userTeamName, prospect.PlayerId, isUserPick: true);
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

    public bool HasPendingScheduledGame()
    {
        return !IsSeasonAdvanceBlockedByPendingDraftWork() && GetNextScheduledGameForSelectedTeam() != null;
    }

    public void SetPreferLiveBoxScore(bool preferLiveBoxScore)
    {
        _preferLiveBoxScore = preferLiveBoxScore;
    }

    public bool ShouldUseLiveBoxScore()
    {
        return _preferLiveBoxScore && GetLiveMatchState() is { IsGameOver: false };
    }

    public void SelectTeam(TeamImportDto team, Action<AsyncOperationProgressView>? reportProgress = null)
    {
        ReportAsyncOperationProgress(reportProgress, "Loading Franchise", $"Loading {team.Name} and restoring franchise state.", 0.05d);
        SelectedTeam = team;
        PendingLiveMatchMode = LiveMatchMode.Franchise;
        _saveState.SelectedTeamName = team.Name;
        _saveState.CurrentSeasonYear = 0;
        _saveState.CurrentFranchiseDate = default;
        _saveState.LastOffseasonSummary = null;
        ReportAsyncOperationProgress(reportProgress, "Loading Franchise", "Restoring any legacy live match data for the selected club.", 0.2d);
        MigrateLegacyFranchiseMatchIfNeeded(team.Name);
        ReportAsyncOperationProgress(reportProgress, "Loading Franchise", "Initializing the franchise calendar and season markers.", 0.45d);
        EnsureFranchiseDateInitialized();
        EnsureSeasonYearInitialized();
        ReportAsyncOperationProgress(reportProgress, "Loading Franchise", "Synchronizing imported players, ratings, and roster mappings.", 0.75d);
        EnsureImportedPlayerMappingsAreSynchronized();
        ReportAsyncOperationProgress(reportProgress, "Loading Franchise", "Saving the franchise snapshot.", 0.92d);
        _stateStore.Save(_saveState);
        ReportAsyncOperationProgress(reportProgress, "Loading Franchise", $"{team.Name} is ready.", 1d);
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

    public PlayerProfileView? GetPlayerProfile(Guid playerId)
    {
        var player = FindPlayerImport(playerId);
        if (player == null)
        {
            return null;
        }

        var assignedTeamName = GetAssignedTeamName(playerId, player.TeamName);
        var ratings = GetPlayerRatings(playerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var currentStats = GetPlayerSeasonStats(playerId);
        var lastSeasonStats = GetLastSeasonStats(playerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var recentStats = GetRecentPlayerStats(playerId, 10);
        var health = GetPlayerHealth(playerId);
        var contractsByPlayerId = GetContractsByPlayerId();
        var contract = contractsByPlayerId.TryGetValue(playerId, out var resolvedContract) ? resolvedContract : null;
        var isPitcher = player.PrimaryPosition is "SP" or "RP";

        var detailLines = new List<string>
        {
            $"{player.PrimaryPosition}/{player.SecondaryPosition}".TrimEnd('/').TrimEnd(),
            $"Team: {assignedTeamName} | Age {player.Age}",
            isPitcher
                ? $"OVR {ratings.OverallRating} | Pitch {ratings.EffectivePitchingRating} | Stamina {ratings.EffectiveStaminaRating} | Durability {ratings.EffectiveDurabilityRating}"
                : $"OVR {ratings.OverallRating} | Contact {ratings.EffectiveContactRating} | Power {ratings.EffectivePowerRating} | Discipline {ratings.EffectiveDisciplineRating}",
            isPitcher
                ? $"Arm {ratings.EffectiveArmRating} | Field {ratings.EffectiveFieldingRating} | Speed {ratings.EffectiveSpeedRating}"
                : $"Speed {ratings.EffectiveSpeedRating} | Field {ratings.EffectiveFieldingRating} | Arm {ratings.EffectiveArmRating}",
            health.InjuryDaysRemaining > 0
                ? $"Medical: {health.InjuryDescription} | {health.InjuryDaysRemaining} day(s) remaining"
                : health.DaysUntilAvailable > 0
                    ? $"Medical: recovery day | {health.DaysUntilAvailable} day(s) until available"
                    : $"Medical: available | Fatigue {health.Fatigue}",
            contract == null
                ? "Contract: no active deal on file"
                : $"Contract: {GetBudgetDisplay(contract.AnnualSalary)} | {contract.YearsRemaining} year(s) remaining"
        };

        var summaryLines = new List<string>
        {
            $"Current season: {FormatPlayerSeasonLine(player.PrimaryPosition, currentStats)}",
            $"Last season: {FormatPlayerSeasonLine(player.PrimaryPosition, lastSeasonStats)}",
            $"Last 10 games: {FormatRecentStatsLine(player.PrimaryPosition, recentStats)}"
        };

        return new PlayerProfileView(
            player.FullName,
            $"{player.PrimaryPosition}/{player.SecondaryPosition} | {assignedTeamName}",
            detailLines,
            summaryLines);
    }

    public void QueueTransfersFocus(Guid playerId)
    {
        _pendingTransfersFocusPlayerId = playerId;
    }

    public bool TryConsumeTransfersFocus(out Guid playerId)
    {
        if (_pendingTransfersFocusPlayerId.HasValue)
        {
            playerId = _pendingTransfersFocusPlayerId.Value;
            _pendingTransfersFocusPlayerId = null;
            return true;
        }

        playerId = Guid.Empty;
        return false;
    }

    public void QueueScoutingFocus(Guid playerId)
    {
        _pendingScoutingFocusPlayerId = playerId;
    }

    public bool TryConsumeScoutingFocus(out Guid playerId)
    {
        if (_pendingScoutingFocusPlayerId.HasValue)
        {
            playerId = _pendingScoutingFocusPlayerId.Value;
            _pendingScoutingFocusPlayerId = null;
            return true;
        }

        playerId = Guid.Empty;
        return false;
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
        if (_saveState.PreviousSeasonStats.TryGetValue(playerId, out var previousSeasonStats))
        {
            return previousSeasonStats;
        }

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
        var contractsByPlayerId = GetContractsByPlayerId();

        return _leagueData.Players
            .Select(player =>
            {
                var assignedTeamName = GetAssignedTeamName(player.PlayerId, player.TeamName);
                teamsByName.TryGetValue(assignedTeamName, out var team);
                var isOnSelectedTeam = selectedTeamName != null && string.Equals(assignedTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase);
                var fee = isOnSelectedTeam ? 0m : CalculateAcquisitionCost(player, assignedTeamName, contractsByPlayerId);

                return new ScoutingPlayerCard(
                    player.PlayerId,
                    player.FullName,
                    assignedTeamName,
                    team?.Abbreviation ?? "FA",
                    player.PrimaryPosition,
                    player.SecondaryPosition,
                    player.Age,
                    isOnSelectedTeam,
                    fee);
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

        var contractsByPlayerId = GetContractsByPlayerId();

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
                true,
                CalculateAcquisitionCost(
                    FindPlayerImport(player.PlayerId)!,
                    SelectedTeam.Name,
                    contractsByPlayerId)))
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

    public IReadOnlyList<ScoutAssignmentView> GetScoutDepartment()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureCoachingStaffInitialized(teamState, SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);

        var department = new List<ScoutAssignmentView>();
        var headScout = teamState.CoachingStaff.FirstOrDefault(coach => string.Equals(coach.Role, "Scouting Director", StringComparison.OrdinalIgnoreCase));
        if (headScout != null)
        {
            department.Add(new ScoutAssignmentView(
                -1,
                "Head Scout",
                headScout.Name,
                headScout.Specialty,
                headScout.Voice,
                "Department oversight",
                "All positions",
                "Balanced coverage",
                "Department Lead",
                "Organization-wide coverage",
                0,
                true,
                false));
        }

        department.AddRange(teamState.AssistantScouts
            .OrderBy(scout => scout.SlotIndex)
            .Take(3)
            .Select(scout => new ScoutAssignmentView(
                scout.SlotIndex,
                $"Scout {scout.SlotIndex + 1}",
                string.IsNullOrWhiteSpace(scout.Name) ? "Open Slot" : scout.Name,
                string.IsNullOrWhiteSpace(scout.Specialty) ? "regional coverage" : scout.Specialty,
                string.IsNullOrWhiteSpace(scout.Voice) ? "balanced" : scout.Voice,
                scout.Country,
                scout.PositionFocus,
                scout.TraitFocus,
                scout.AssignmentMode,
                GetScoutAssignmentTargetLabel(teamState, scout),
                scout.DaysUntilNextDiscovery,
                false,
                string.IsNullOrWhiteSpace(scout.Name))));

        return department;
    }

    public IReadOnlyList<CoachProfileView> GetAvailableAssistantScoutCandidates(int slotIndex)
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        return BuildScoutCandidatePool(SelectedTeam.Name, slotIndex)
            .Select(candidate => new CoachProfileView($"Scout {slotIndex + 1}", candidate.Name, candidate.Specialty, candidate.Voice))
            .ToList();
    }

    public bool HireAssistantScout(int slotIndex, int candidateIndex, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before changing your scouts.";
            return false;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null)
        {
            statusMessage = "That scout slot is not available right now.";
            return false;
        }

        var candidatePool = BuildScoutCandidatePool(SelectedTeam.Name, slotIndex);
        if (candidatePool.Count == 0)
        {
            statusMessage = "No scout candidates are available right now.";
            return false;
        }

        if (candidateIndex < 0 || candidateIndex >= candidatePool.Count)
        {
            statusMessage = "That scout candidate is no longer available.";
            return false;
        }

        var replacement = candidatePool[candidateIndex];
        ClearScoutPlayerAssignment(teamState, slotIndex);
        scout.Name = replacement.Name;
        scout.Specialty = replacement.Specialty;
        scout.Voice = replacement.Voice;
        scout.AssignmentMode = "Unassigned";
        scout.AssignmentTarget = string.Empty;
        scout.DaysUntilNextDiscovery = 0;
        Save();

        statusMessage = $"{scout.Name} has been hired into Scout {scout.SlotIndex + 1}. Assign him to a region or player when you're ready.";
        return true;
    }

    public bool ChangeAssistantScout(int slotIndex, int direction, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before changing your scouts.";
            return false;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null)
        {
            statusMessage = "That scout slot is not available right now.";
            return false;
        }

        var candidatePool = BuildScoutCandidatePool(SelectedTeam.Name, slotIndex);
        if (candidatePool.Count == 0)
        {
            statusMessage = "No scout candidates are available right now.";
            return false;
        }

        var currentIndex = candidatePool.FindIndex(candidate =>
            string.Equals(candidate.Name, scout.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Specialty, scout.Specialty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Voice, scout.Voice, StringComparison.OrdinalIgnoreCase));

        var step = direction >= 0 ? 1 : -1;
        var nextIndex = currentIndex < 0
            ? (step > 0 ? 0 : candidatePool.Count - 1)
            : (currentIndex + step + candidatePool.Count) % candidatePool.Count;

        return HireAssistantScout(slotIndex, nextIndex, out statusMessage);
    }

    public bool ReleaseAssistantScout(int slotIndex, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before changing your scouts.";
            return false;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null)
        {
            statusMessage = "That scout slot is not available right now.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(scout.Name))
        {
            statusMessage = $"Scout {slotIndex + 1} is already open.";
            return false;
        }

        ClearScoutPlayerAssignment(teamState, slotIndex);
        scout.Name = string.Empty;
        scout.Specialty = string.Empty;
        scout.Voice = string.Empty;
        scout.AssignmentMode = "Unassigned";
        scout.AssignmentTarget = string.Empty;
        scout.DaysUntilNextDiscovery = 0;
        Save();

        statusMessage = $"Scout {slotIndex + 1} is now open. Any players already found stay on your board at their current scouting percentage.";
        return true;
    }

    public string CycleAssistantScoutCountry(int slotIndex, int direction)
    {
        return CycleAssistantScoutSetting(slotIndex, direction, ScoutingCountryOptions, scout => scout.Country, (scout, value) => scout.Country = value, "country");
    }

    public string CycleAssistantScoutPositionFocus(int slotIndex, int direction)
    {
        return CycleAssistantScoutSetting(slotIndex, direction, ScoutingPositionOptions, scout => scout.PositionFocus, (scout, value) => scout.PositionFocus = value, "position focus");
    }

    public string CycleAssistantScoutTraitFocus(int slotIndex, int direction)
    {
        return CycleAssistantScoutSetting(slotIndex, direction, ScoutingTraitOptions, scout => scout.TraitFocus, (scout, value) => scout.TraitFocus = value, "trait focus");
    }

    public IReadOnlyList<string> GetScoutCountryOptions() => ScoutingCountryOptions;

    public IReadOnlyList<string> GetScoutPositionOptions() => ScoutingPositionOptions;

    public IReadOnlyList<string> GetScoutTraitOptions() => ScoutingTraitOptions;

    public IReadOnlyList<string> GetScoutAssignmentModes() => ScoutAssignmentModeOptions;

    public string SetAssistantScoutCountry(int slotIndex, string country)
    {
        return SetAssistantScoutSetting(slotIndex, country, ScoutingCountryOptions, scout => scout.Country, (scout, value) => scout.Country = value, "country");
    }

    public string SetAssistantScoutPositionFocus(int slotIndex, string positionFocus)
    {
        return SetAssistantScoutSetting(slotIndex, positionFocus, ScoutingPositionOptions, scout => scout.PositionFocus, (scout, value) => scout.PositionFocus = value, "position focus");
    }

    public string SetAssistantScoutTraitFocus(int slotIndex, string traitFocus)
    {
        return SetAssistantScoutSetting(slotIndex, traitFocus, ScoutingTraitOptions, scout => scout.TraitFocus, (scout, value) => scout.TraitFocus = value, "trait focus");
    }

    public string AssignScoutToRegionSearch(int slotIndex)
    {
        if (SelectedTeam == null)
        {
            return "Select a team before assigning your scouts.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null || string.IsNullOrWhiteSpace(scout.Name))
        {
            return $"Hire Scout {slotIndex + 1} before assigning a region search.";
        }

        ClearScoutPlayerAssignment(teamState, slotIndex);
        scout.AssignmentMode = "Region Search";
        scout.AssignmentTarget = $"{scout.Country}|{scout.PositionFocus}|{scout.TraitFocus}";
        scout.DaysUntilNextDiscovery = Math.Max(2, GetScoutDiscoveryDays(SelectedTeam.Name, scout));
        Save();
        return $"{scout.Name} is now scouting {scout.Country} for {GetScoutFocusText(scout.PositionFocus, scout.TraitFocus)}. First report in {scout.DaysUntilNextDiscovery} day(s).";
    }

    public string AssignScoutToAutoNeedSearch(int slotIndex)
    {
        if (SelectedTeam == null)
        {
            return "Select a team before assigning your scouts.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null || string.IsNullOrWhiteSpace(scout.Name))
        {
            return $"Hire Scout {slotIndex + 1} before enabling auto scouting.";
        }

        ClearScoutPlayerAssignment(teamState, slotIndex);
        scout.AssignmentMode = "Auto Need Search";
        ApplyAutoScoutNeedPlan(SelectedTeam.Name, scout);
        scout.DaysUntilNextDiscovery = Math.Max(2, GetScoutDiscoveryDays(SelectedTeam.Name, scout));
        Save();

        return $"{scout.Name} is now in auto mode, scouting {scout.Country} for {GetScoutFocusText(scout.PositionFocus, scout.TraitFocus)} based on the club's current needs. First report in {scout.DaysUntilNextDiscovery} day(s).";
    }

    public string AssignScoutToScoutedPlayer(int slotIndex, string prospectKey)
    {
        if (SelectedTeam == null)
        {
            return "Select a team before assigning your scouts.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null || string.IsNullOrWhiteSpace(scout.Name))
        {
            return $"Hire Scout {slotIndex + 1} before assigning a player follow.";
        }

        var prospect = teamState.ScoutedPlayers.FirstOrDefault(player => string.Equals(player.ProspectKey, prospectKey, StringComparison.OrdinalIgnoreCase));
        if (prospect == null)
        {
            return "That player is not on your scouted board yet.";
        }

        ClearScoutPlayerAssignment(teamState, slotIndex);
        scout.AssignmentMode = "Player Follow";
        scout.AssignmentTarget = prospect.ProspectKey;
        scout.DaysUntilNextDiscovery = 0;
        prospect.AssignedScoutSlotIndex = slotIndex;
        prospect.AssignedScoutName = scout.Name;
        Save();
        return $"{scout.Name} is now following {prospect.PlayerName}. The file is {prospect.ScoutingProgress}% complete.";
    }

    public string ClearScoutAssignment(int slotIndex)
    {
        if (SelectedTeam == null)
        {
            return "Select a team before assigning your scouts.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null)
        {
            return "That scout slot is not available right now.";
        }

        ClearScoutPlayerAssignment(teamState, slotIndex);
        scout.AssignmentMode = "Unassigned";
        scout.AssignmentTarget = string.Empty;
        scout.DaysUntilNextDiscovery = 0;
        Save();

        var scoutLabel = string.IsNullOrWhiteSpace(scout.Name) ? $"Scout {slotIndex + 1}" : scout.Name;
        return $"{scoutLabel} is now unassigned.";
    }

    public IReadOnlyList<AmateurProspectView> GetScoutedPlayers(bool targetListOnly = false)
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);

        return teamState.ScoutedPlayers
            .Where(player => targetListOnly ? player.IsOnTargetList : !player.IsOnTargetList)
            .OrderByDescending(player => player.ScoutingProgress)
            .ThenByDescending(player => player.FoundDate)
            .ThenBy(player => player.PlayerName)
            .Select(BuildAmateurProspectView)
            .ToList();
    }

    public IReadOnlyList<AmateurProspectView> GetScoutFoundPlayers(int slotIndex)
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);

        return teamState.ScoutedPlayers
            .Where(player => player.FoundByScoutSlotIndex == slotIndex)
            .OrderByDescending(player => player.FoundDate)
            .ThenByDescending(player => player.ScoutingProgress)
            .ThenBy(player => player.PlayerName)
            .Select(BuildAmateurProspectView)
            .ToList();
    }

    public string AddScoutedPlayerToTargetList(string prospectKey)
    {
        return SetScoutedPlayerTargetStatus(prospectKey, true);
    }

    public string RemoveScoutedPlayerFromTargetList(string prospectKey)
    {
        return SetScoutedPlayerTargetStatus(prospectKey, false);
    }

    public IReadOnlyList<AmateurProspectView> GetAmateurScoutingProspects()
    {
        if (SelectedTeam == null)
        {
            return [];
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);

        return teamState.ScoutedPlayers
            .OrderByDescending(player => player.ScoutingProgress)
            .ThenByDescending(player => player.FoundDate)
            .ThenBy(player => player.PlayerName)
            .Select(BuildAmateurProspectView)
            .ToList();
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

        var offeredLineupAssignments = CaptureLineupSlotAssignments(selectedTeamState, selectedTeamName, offeredPlayerId);
        var offeredRotationIndex = FindPlayerSlotIndex(selectedTeamState.RotationSlots, offeredPlayerId);
        var targetLineupAssignments = CaptureLineupSlotAssignments(targetTeamState, targetTeamName, targetPlayerId);
        var targetRotationIndex = FindPlayerSlotIndex(targetTeamState.RotationSlots, targetPlayerId);

        _saveState.PlayerAssignments[targetPlayerId] = selectedTeamName;
        _saveState.PlayerAssignments[offeredPlayerId] = targetTeamName;

        ClearPlayerFromAllLineupSlots(selectedTeamState, selectedTeamName, offeredPlayerId);
        ClearPlayerFromSlots(selectedTeamState.RotationSlots, offeredPlayerId);
        ClearPlayerFromAllLineupSlots(selectedTeamState, selectedTeamName, targetPlayerId);
        ClearPlayerFromSlots(selectedTeamState.RotationSlots, targetPlayerId);
        ClearPlayerFromAllLineupSlots(targetTeamState, targetTeamName, targetPlayerId);
        ClearPlayerFromSlots(targetTeamState.RotationSlots, targetPlayerId);
        ClearPlayerFromAllLineupSlots(targetTeamState, targetTeamName, offeredPlayerId);
        ClearPlayerFromSlots(targetTeamState.RotationSlots, offeredPlayerId);

        TryAssignPlayerToPreviousSlots(selectedTeamState, selectedTeamName, targetPlayerId, targetPlayer.PrimaryPosition, offeredLineupAssignments, offeredRotationIndex);
        TryAssignPlayerToPreviousSlots(targetTeamState, targetTeamName, offeredPlayerId, offeredPlayer.PrimaryPosition, targetLineupAssignments, targetRotationIndex);

        AddTransferRecord(selectedTeamName, $"Acquired {targetPlayer.FullName} from {targetTeamName} for {offeredPlayer.FullName}.");
        AddTransferRecord(targetTeamName, $"Sent {targetPlayer.FullName} to {selectedTeamName} for {offeredPlayer.FullName}.");
        MovePlayerContract(targetPlayerId, targetTeamState.Economy, selectedTeamState.Economy, targetPlayer.FullName);
        MovePlayerContract(offeredPlayerId, selectedTeamState.Economy, targetTeamState.Economy, offeredPlayer.FullName);
        ApplyTradeFanInterestShift(selectedTeamState.Economy, targetPlayer, offeredPlayer);
        Save();

        statusMessage = $"Deal complete: {targetPlayer.FullName} joins {selectedTeamName} and {offeredPlayer.FullName} heads to {targetTeamName}.";
        return true;
    }

    public bool TryBuyPlayer(Guid targetPlayerId, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a franchise team before making a move.";
            return false;
        }

        var targetPlayer = FindPlayerImport(targetPlayerId);
        if (targetPlayer == null)
        {
            statusMessage = "Player not found in the league database.";
            return false;
        }

        var selectedTeamName = SelectedTeam.Name;
        var targetTeamName = GetAssignedTeamName(targetPlayerId, targetPlayer.TeamName);

        if (string.Equals(targetTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = $"{targetPlayer.FullName} is already on your roster.";
            return false;
        }

        var selectedTeamState = GetOrCreateTeamState(selectedTeamName);
        var targetTeamState = GetOrCreateTeamState(targetTeamName);
        var contractsByPlayerId = GetContractsByPlayerId();
        var transferFee = CalculateAcquisitionCost(targetPlayer, targetTeamName, contractsByPlayerId);
        var transferBudget = GetTransferBudget();

        if (!HasRosterRoom(selectedTeamName, incomingPlayers: 1, outgoingPlayers: 0))
        {
            statusMessage = $"Roster limit reached. You already have {GetTeamRosterCount(selectedTeamName)} players and cannot exceed {MaxRosterSize}.";
            return false;
        }

        if (transferFee > transferBudget)
        {
            statusMessage = $"Not enough budget. {targetPlayer.FullName} costs {GetBudgetDisplay(transferFee)} but you only have {GetBudgetDisplay(transferBudget)} available after payroll reserve.";
            return false;
        }

        selectedTeamState.Economy.CashOnHand = decimal.Round(selectedTeamState.Economy.CashOnHand - transferFee, 2);
        targetTeamState.Economy.CashOnHand = decimal.Round(targetTeamState.Economy.CashOnHand + (transferFee * 0.5m), 2);
        selectedTeamState.Economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(selectedTeamState.Economy);
        targetTeamState.Economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(targetTeamState.Economy);

        _saveState.PlayerAssignments[targetPlayerId] = selectedTeamName;
        InitializeLineupSlots(selectedTeamState, selectedTeamName);
        InitializeRotationSlots(selectedTeamState, selectedTeamName);
        InitializeLineupSlots(targetTeamState, targetTeamName);
        InitializeRotationSlots(targetTeamState, targetTeamName);
        ClearPlayerFromAllLineupSlots(targetTeamState, targetTeamName, targetPlayerId);
        ClearPlayerFromSlots(targetTeamState.RotationSlots, targetPlayerId);
        ClearPlayerFromAllLineupSlots(selectedTeamState, selectedTeamName, targetPlayerId);
        ClearPlayerFromSlots(selectedTeamState.RotationSlots, targetPlayerId);

        MovePlayerContract(targetPlayerId, targetTeamState.Economy, selectedTeamState.Economy, targetPlayer.FullName);

        var ratings = GetPlayerRatings(targetPlayer.PlayerId, targetPlayer.FullName, targetPlayer.PrimaryPosition, targetPlayer.SecondaryPosition, targetPlayer.Age);
        var interestGain = ratings.OverallRating switch { >= 80 => 3, >= 70 => 1, _ => 0 };
        selectedTeamState.Economy.FanInterest = Math.Clamp(selectedTeamState.Economy.FanInterest + interestGain, 10, 100);
        selectedTeamState.Economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(selectedTeamState.Economy);

        FinanceMath.AddSnapshot(selectedTeamState.Economy, new FinancialSnapshot
        {
            EffectiveDate = GetCurrentFranchiseDate(),
            Category = "Transfer Fee",
            Revenue = 0m,
            Expenses = transferFee,
            Attendance = 0,
            FanInterest = selectedTeamState.Economy.FanInterest,
            CashAfter = selectedTeamState.Economy.CashOnHand,
            Notes = $"Bought {targetPlayer.FullName} ({targetPlayer.PrimaryPosition}) from {targetTeamName} for {GetBudgetDisplay(transferFee)}."
        });

        AddTransferRecord(selectedTeamName, $"Signed {targetPlayer.FullName} from {targetTeamName} for {GetBudgetDisplay(transferFee)}.");
        AddTransferRecord(targetTeamName, $"Sold {targetPlayer.FullName} to {selectedTeamName} for {GetBudgetDisplay(transferFee)}.");
        Save();

        statusMessage = $"{targetPlayer.FullName} joins {selectedTeamName} for {GetBudgetDisplay(transferFee)}.";
        return true;
    }

    public bool TryBuyPlayerWithTradeChips(Guid targetPlayerId, IReadOnlyList<Guid> offeredPlayerIds, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a franchise team before making a move.";
            return false;
        }

        // If no trade chips offered, fall back to regular buy
        if (offeredPlayerIds.Count == 0)
        {
            return TryBuyPlayer(targetPlayerId, out statusMessage);
        }

        var targetPlayer = FindPlayerImport(targetPlayerId);
        if (targetPlayer == null)
        {
            statusMessage = "Player not found in the league database.";
            return false;
        }

        var selectedTeamName = SelectedTeam.Name;
        var targetTeamName = GetAssignedTeamName(targetPlayerId, targetPlayer.TeamName);

        if (string.Equals(targetTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = $"{targetPlayer.FullName} is already on your roster.";
            return false;
        }

        var uniqueOfferedIds = offeredPlayerIds
            .Distinct()
            .Where(playerId => playerId != targetPlayerId)
            .ToList();

        if (uniqueOfferedIds.Count == 0)
        {
            return TryBuyPlayer(targetPlayerId, out statusMessage);
        }

        // Validate all offered players are on the selected team
        var offeredPlayers = new List<PlayerImportDto>();
        foreach (var offeredPlayerId in uniqueOfferedIds)
        {
            var offeredPlayer = FindPlayerImport(offeredPlayerId);
            if (offeredPlayer == null)
            {
                statusMessage = "One of the offered players could not be found.";
                return false;
            }

            var offeredTeamName = GetAssignedTeamName(offeredPlayerId, offeredPlayer.TeamName);
            if (!string.Equals(offeredTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
            {
                statusMessage = $"{offeredPlayer.FullName} is not on your roster.";
                return false;
            }

            offeredPlayers.Add(offeredPlayer);
        }

        var contractsByPlayerId = GetContractsByPlayerId();
        var askingPrice = CalculateAcquisitionCost(targetPlayer, targetTeamName, contractsByPlayerId);
        var packageValue = offeredPlayers.Sum(player => GetTradeChipValueForTeam(player.PlayerId, targetTeamName));
        var requiredValue = decimal.Round(askingPrice * 0.93m, 0);

        // Check if we have enough roster space for a trade (incoming 1, outgoing N)
        if (!HasRosterRoom(selectedTeamName, incomingPlayers: 1, outgoingPlayers: uniqueOfferedIds.Count))
        {
            statusMessage = $"Trade would exceed the {MaxRosterSize}-player roster cap.";
            return false;
        }

        decimal cashNeeded = 0m;
        if (packageValue < requiredValue)
        {
            // Need to make up the difference with cash
            cashNeeded = requiredValue - packageValue;
            var transferBudget = GetTransferBudget();
            if (cashNeeded > transferBudget)
            {
                statusMessage = $"{targetTeamName} wants more value. Package is {GetBudgetDisplay(packageValue)}, they want {GetBudgetDisplay(requiredValue)}, and you only have {GetBudgetDisplay(transferBudget)} budget available. Try offering more players.";
                return false;
            }
        }

        // All validations passed - execute the hybrid transaction (trade with potential cash top-up)
        var selectedTeamState = GetOrCreateTeamState(selectedTeamName);
        var targetTeamState = GetOrCreateTeamState(targetTeamName);

        // Deduct cash if needed
        if (cashNeeded > 0)
        {
            selectedTeamState.Economy.CashOnHand = decimal.Round(selectedTeamState.Economy.CashOnHand - cashNeeded, 2);
        }

        // Give receiving team half of the cash value (or proportional split for package)
        var cashToReceivingTeam = decimal.Round((cashNeeded + packageValue) * 0.5m, 2);
        targetTeamState.Economy.CashOnHand = decimal.Round(targetTeamState.Economy.CashOnHand + cashToReceivingTeam, 2);

        selectedTeamState.Economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(selectedTeamState.Economy);
        targetTeamState.Economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(targetTeamState.Economy);

        // Assign incoming player
        _saveState.PlayerAssignments[targetPlayerId] = selectedTeamName;

        // Remove outgoing players from their current team
        foreach (var outgoingPlayerId in uniqueOfferedIds)
        {
            _saveState.PlayerAssignments[outgoingPlayerId] = targetTeamName;
        }

        InitializeLineupSlots(selectedTeamState, selectedTeamName);
        InitializeRotationSlots(selectedTeamState, selectedTeamName);
        InitializeLineupSlots(targetTeamState, targetTeamName);
        InitializeRotationSlots(targetTeamState, targetTeamName);

        // Clear players from lineup/rotation slots
        foreach (var outgoingPlayerId in uniqueOfferedIds)
        {
            ClearPlayerFromAllLineupSlots(selectedTeamState, selectedTeamName, outgoingPlayerId);
            ClearPlayerFromSlots(selectedTeamState.RotationSlots, outgoingPlayerId);
        }

        ClearPlayerFromAllLineupSlots(targetTeamState, targetTeamName, targetPlayerId);
        ClearPlayerFromSlots(targetTeamState.RotationSlots, targetPlayerId);
        ClearPlayerFromAllLineupSlots(selectedTeamState, selectedTeamName, targetPlayerId);
        ClearPlayerFromSlots(selectedTeamState.RotationSlots, targetPlayerId);

        // Move contracts
        MovePlayerContract(targetPlayerId, targetTeamState.Economy, selectedTeamState.Economy, targetPlayer.FullName);
        foreach (var outgoingPlayer in offeredPlayers)
        {
            MovePlayerContract(outgoingPlayer.PlayerId, selectedTeamState.Economy, targetTeamState.Economy, outgoingPlayer.FullName);
        }

        // Update fan interest based on incoming player quality
        var ratings = GetPlayerRatings(targetPlayer.PlayerId, targetPlayer.FullName, targetPlayer.PrimaryPosition, targetPlayer.SecondaryPosition, targetPlayer.Age);
        var interestGain = ratings.OverallRating switch { >= 80 => 2, >= 70 => 1, _ => 0 };

        // Apply interest penalty for each outgoing player
        var interestLoss = offeredPlayers.Count;
        selectedTeamState.Economy.FanInterest = Math.Clamp(selectedTeamState.Economy.FanInterest + interestGain - interestLoss, 10, 100);
        selectedTeamState.Economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(selectedTeamState.Economy);

        // Log financial snapshot
        var totalValue = cashNeeded + packageValue;
        FinanceMath.AddSnapshot(selectedTeamState.Economy, new FinancialSnapshot
        {
            EffectiveDate = GetCurrentFranchiseDate(),
            Category = "Trade/Transfer",
            Revenue = 0m,
            Expenses = cashNeeded,
            Attendance = 0,
            FanInterest = selectedTeamState.Economy.FanInterest,
            CashAfter = selectedTeamState.Economy.CashOnHand,
            Notes = $"Traded {string.Join(", ", offeredPlayers.Select(p => p.FullName))} to {targetTeamName} for {targetPlayer.FullName}."
        });

        // Add transfer records
        var outgoingNames = string.Join(", ", offeredPlayers.Select(p => p.FullName));
        var transactionDesc = cashNeeded > 0
            ? $"Traded {outgoingNames} to {targetTeamName} for {targetPlayer.FullName} (package value: {GetBudgetDisplay(packageValue)}, cash: {GetBudgetDisplay(cashNeeded)})."
            : $"Traded {outgoingNames} to {targetTeamName} for {targetPlayer.FullName}.";

        AddTransferRecord(selectedTeamName, transactionDesc);
        AddTransferRecord(targetTeamName, $"Received {targetPlayer.FullName} from {selectedTeamName}, gave {outgoingNames}.");
        Save();

        statusMessage = $"Trade complete: {outgoingNames} → {targetTeamName}, {targetPlayer.FullName} ← {selectedTeamName}";
        if (cashNeeded > 0)
        {
            statusMessage += $". (Package: {GetBudgetDisplay(packageValue)} + {GetBudgetDisplay(cashNeeded)} cash)";
        }
        return true;
    }

    public bool TryTradeForPlayerPackage(Guid targetPlayerId, IReadOnlyList<Guid> offeredPlayerIds, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before trying to make a move.";
            return false;
        }

        if (offeredPlayerIds.Count == 0)
        {
            statusMessage = "Choose at least one outgoing player for a trade package, or use the Buy option.";
            return false;
        }

        var selectedTeamName = SelectedTeam.Name;
        var targetPlayer = FindPlayerImport(targetPlayerId);
        if (targetPlayer == null)
        {
            statusMessage = "Player not found in the league database.";
            return false;
        }

        var targetTeamName = GetAssignedTeamName(targetPlayerId, targetPlayer.TeamName);
        if (string.Equals(targetTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = $"{targetPlayer.FullName} is already on your roster.";
            return false;
        }

        var uniqueOfferedIds = offeredPlayerIds
            .Distinct()
            .Where(playerId => playerId != targetPlayerId)
            .ToList();
        if (uniqueOfferedIds.Count == 0)
        {
            statusMessage = "Select at least one valid outgoing player for the trade package.";
            return false;
        }

        var offeredPlayers = new List<PlayerImportDto>();
        foreach (var offeredPlayerId in uniqueOfferedIds)
        {
            var offeredPlayer = FindPlayerImport(offeredPlayerId);
            if (offeredPlayer == null)
            {
                statusMessage = "One of the offered players could not be found.";
                return false;
            }

            var offeredTeamName = GetAssignedTeamName(offeredPlayerId, offeredPlayer.TeamName);
            if (!string.Equals(offeredTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
            {
                statusMessage = $"{offeredPlayer.FullName} is not on your roster.";
                return false;
            }

            offeredPlayers.Add(offeredPlayer);
        }

        if (!HasRosterRoom(selectedTeamName, incomingPlayers: 1, outgoingPlayers: uniqueOfferedIds.Count))
        {
            statusMessage = $"Trade would exceed the {MaxRosterSize}-player roster cap.";
            return false;
        }

        var contractsByPlayerId = GetContractsByPlayerId();
        var askingPrice = CalculateAcquisitionCost(targetPlayer, targetTeamName, contractsByPlayerId);
        var packageValue = offeredPlayers.Sum(player => GetTradeChipValueForTeam(player.PlayerId, targetTeamName));
        var requiredValue = decimal.Round(askingPrice * 0.93m, 0);
        if (packageValue < requiredValue)
        {
            statusMessage = $"{targetTeamName} declines. Package value is {GetBudgetDisplay(packageValue)} but they want about {GetBudgetDisplay(requiredValue)} in return.";
            return false;
        }

        var selectedTeamState = GetOrCreateTeamState(selectedTeamName);
        var targetTeamState = GetOrCreateTeamState(targetTeamName);
        InitializeLineupSlots(selectedTeamState, selectedTeamName);
        InitializeRotationSlots(selectedTeamState, selectedTeamName);
        InitializeLineupSlots(targetTeamState, targetTeamName);
        InitializeRotationSlots(targetTeamState, targetTeamName);

        var firstOfferedPlayerId = uniqueOfferedIds[0];
        var incomingLineupAssignments = CaptureLineupSlotAssignments(selectedTeamState, selectedTeamName, firstOfferedPlayerId);
        var incomingRotationIndex = FindPlayerSlotIndex(selectedTeamState.RotationSlots, firstOfferedPlayerId);
        var targetLineupAssignments = CaptureLineupSlotAssignments(targetTeamState, targetTeamName, targetPlayerId);
        var targetRotationIndex = FindPlayerSlotIndex(targetTeamState.RotationSlots, targetPlayerId);

        _saveState.PlayerAssignments[targetPlayerId] = selectedTeamName;
        foreach (var offeredPlayerId in uniqueOfferedIds)
        {
            _saveState.PlayerAssignments[offeredPlayerId] = targetTeamName;
        }

        ClearPlayerFromAllLineupSlots(selectedTeamState, selectedTeamName, targetPlayerId);
        ClearPlayerFromSlots(selectedTeamState.RotationSlots, targetPlayerId);
        ClearPlayerFromAllLineupSlots(targetTeamState, targetTeamName, targetPlayerId);
        ClearPlayerFromSlots(targetTeamState.RotationSlots, targetPlayerId);

        foreach (var offeredPlayerId in uniqueOfferedIds)
        {
            ClearPlayerFromAllLineupSlots(selectedTeamState, selectedTeamName, offeredPlayerId);
            ClearPlayerFromSlots(selectedTeamState.RotationSlots, offeredPlayerId);
            ClearPlayerFromAllLineupSlots(targetTeamState, targetTeamName, offeredPlayerId);
            ClearPlayerFromSlots(targetTeamState.RotationSlots, offeredPlayerId);
        }

        TryAssignPlayerToPreviousSlots(selectedTeamState, selectedTeamName, targetPlayerId, targetPlayer.PrimaryPosition, incomingLineupAssignments, incomingRotationIndex);

        for (var i = 0; i < offeredPlayers.Count; i++)
        {
            var offeredPlayer = offeredPlayers[i];
            var lineupAssignments = i == 0 ? targetLineupAssignments : default;
            var rotationIndex = i == 0 ? targetRotationIndex : -1;
            TryAssignPlayerToPreviousSlots(targetTeamState, targetTeamName, offeredPlayer.PlayerId, offeredPlayer.PrimaryPosition, lineupAssignments, rotationIndex);
        }

        MovePlayerContract(targetPlayerId, targetTeamState.Economy, selectedTeamState.Economy, targetPlayer.FullName);
        foreach (var offeredPlayer in offeredPlayers)
        {
            MovePlayerContract(offeredPlayer.PlayerId, selectedTeamState.Economy, targetTeamState.Economy, offeredPlayer.FullName);
        }

        var outgoingLabel = string.Join(", ", offeredPlayers.Select(player => player.FullName));
        AddTransferRecord(selectedTeamName, $"Acquired {targetPlayer.FullName} from {targetTeamName} for {outgoingLabel}.");
        AddTransferRecord(targetTeamName, $"Sent {targetPlayer.FullName} to {selectedTeamName} for {outgoingLabel}.");
        ApplyTradeFanInterestShift(selectedTeamState.Economy, targetPlayer, offeredPlayers[0]);
        Save();

        statusMessage = $"Deal complete: {targetPlayer.FullName} joins {selectedTeamName} for {outgoingLabel}.";
        return true;
    }

    public int GetSelectedTeamRosterCount()
    {
        return SelectedTeam == null ? 0 : GetTeamRosterCount(SelectedTeam.Name);
    }

    public decimal GetPlayerAskingPrice(Guid playerId)
    {
        var player = FindPlayerImport(playerId);
        if (player == null)
        {
            return 0m;
        }

        var owningTeamName = GetAssignedTeamName(playerId, player.TeamName);
        var contractsByPlayerId = GetContractsByPlayerId();
        return CalculateAcquisitionCost(player, owningTeamName, contractsByPlayerId);
    }

    public decimal GetTradeChipValueForTeam(Guid playerId, string receivingTeamName)
    {
        var player = FindPlayerImport(playerId);
        if (player == null)
        {
            return 0m;
        }

        var contractsByPlayerId = GetContractsByPlayerId();
        var currentTeamName = GetAssignedTeamName(playerId, player.TeamName);
        var baseValue = CalculateAcquisitionCost(player, currentTeamName, contractsByPlayerId);
        var multiplier = GetPositionNeedMultiplier(receivingTeamName, player.PrimaryPosition);
        return decimal.Round(baseValue * multiplier, 0);
    }

    public string GetTeamPositionNeedLabel(string teamName, string position)
    {
        var multiplier = GetPositionNeedMultiplier(teamName, position);
        if (multiplier >= 1.25m)
        {
            return "High Need";
        }

        if (multiplier >= 1.10m)
        {
            return "Need";
        }

        if (multiplier <= 0.85m)
        {
            return "Low Need";
        }

        return "Balanced";
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

        var teamSchedule = GetActiveSchedule()
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

    public decimal GetTransferBudget()
    {
        if (SelectedTeam == null)
        {
            return 0m;
        }

        var economy = GetOrCreateTeamState(SelectedTeam.Name).Economy;
        var annualPayrollCommitment = economy.PlayerPayroll + economy.CoachPayroll;
        return Math.Max(0m, decimal.Round(economy.CashOnHand - annualPayrollCommitment, 2));
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

    public string GetTeamRecordLabel(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return "0-0";
        }

        return BuildRecordSummaryLabel(GetTeamRecordSummary(teamName));
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

    private string FormatPlayerSeasonLine(string primaryPosition, PlayerSeasonStatsState stats)
    {
        return primaryPosition is "SP" or "RP"
            ? $"{stats.EarnedRunAverageDisplay} ERA | {stats.WinLossDisplay} | {stats.StrikeoutsPitched} K in {stats.GamesPitched} G"
            : $"{stats.BattingAverageDisplay} AVG | {stats.HomeRuns} HR | {stats.RunsBattedIn} RBI | {stats.OpsDisplay} OPS in {stats.GamesPlayed} G";
    }

    private static string FormatRecentStatsLine(string primaryPosition, RecentPlayerStatsView stats)
    {
        return primaryPosition is "SP" or "RP"
            ? $"{stats.EraDisplay} ERA | {stats.PitcherStrikeouts} K | {stats.Wins}-{stats.Losses} in {stats.GamesPlayed} G"
            : $"{stats.BattingAverageDisplay} AVG | {stats.HomeRuns} HR | {stats.OpsDisplay} OPS in {stats.GamesPlayed} G";
    }

    private List<ScheduleImportDto> GetRemainingScheduledGamesForDate(DateTime date)
    {
        return GetActiveSchedule()
            .Where(game =>
                !IsScheduledGameCompleted(game) &&
                game.Date.Date == date.Date)
            .OrderBy(game => game.GameNumber ?? 1)
            .ThenBy(game => game.HomeTeamName)
            .ToList();
    }

    private List<ScheduleImportDto> GetActiveSchedule()
    {
        EnsureSeasonYearInitialized();
        var yearOffset = _saveState.CurrentSeasonYear - GetBaseScheduleYear();
        return _leagueData.Schedule
            .Select(game => new ScheduleImportDto
            {
                Date = game.Date.AddYears(yearOffset),
                HomeTeamName = game.HomeTeamName,
                AwayTeamName = game.AwayTeamName,
                GameNumber = game.GameNumber,
                Venue = game.Venue
            })
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameNumber ?? 1)
            .ThenBy(game => game.HomeTeamName)
            .ToList();
    }

    public bool SimulateCurrentDay(out string statusMessage)
    {
        return SimulateCurrentDayInternal(saveAfterAdvance: true, out statusMessage);
    }

    public bool CanSimulateCurrentDay()
    {
        return SelectedTeam != null && !IsRegularSeasonComplete() && !IsSeasonAdvanceBlockedByPendingDraftWork();
    }

    public bool SimulateToEndOfSeason(out string statusMessage, Action<AsyncOperationProgressView>? reportProgress = null)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before simulating the season.";
            return false;
        }

        if (IsSeasonAdvanceBlockedByPendingDraftWork())
        {
            statusMessage = GetNextSeasonBlockerMessage();
            return false;
        }

        var selectedTeamName = SelectedTeam.Name;
        var startDate = GetCurrentFranchiseDate().Date;
        var seasonEnd = GetSeasonCalendarEndDate().Date;
        if (startDate > seasonEnd)
        {
            statusMessage = "The regular season has already been completed.";
            return false;
        }

        var daysSimulated = 0;
        var selectedTeamGames = 0;
        var totalLeagueGames = 0;
        var practiceDays = 0;
        var totalDaysToSimulate = Math.Max(1, (seasonEnd - startDate).Days + 1);

        ReportAsyncOperationProgress(reportProgress, "Simming Season", $"Preparing to sim from {startDate:MMM d} through {seasonEnd:MMM d}.", 0d);

        while (GetCurrentFranchiseDate().Date <= seasonEnd)
        {
            var currentDate = GetCurrentFranchiseDate().Date;
            var todaysGames = GetRemainingScheduledGamesForDate(currentDate);
            totalLeagueGames += todaysGames.Count;
            selectedTeamGames += todaysGames.Count(game =>
                string.Equals(game.HomeTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.AwayTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase));

            if (TryGetPracticeSessionInfo(currentDate, out _))
            {
                practiceDays++;
            }

            var progressValue = Math.Clamp(daysSimulated / (double)totalDaysToSimulate, 0d, 0.98d);
            ReportAsyncOperationProgress(
                reportProgress,
                "Simming Season",
                $"{currentDate:MMM d}: {todaysGames.Count} league game(s), {selectedTeamGames} {selectedTeamName} game(s) counted so far, {practiceDays} practice / recovery day(s).",
                progressValue);

            if (!SimulateCurrentDayInternal(saveAfterAdvance: false, out statusMessage))
            {
                return false;
            }

            daysSimulated++;

            if (GetCurrentFranchiseDate().Date == currentDate && IsSeasonAdvanceBlockedByPendingDraftWork())
            {
                break;
            }
        }

        ReportAsyncOperationProgress(reportProgress, "Simming Season", "Saving the completed season simulation.", 0.99d);
        ClearLiveMatchState(LiveMatchMode.Franchise);
        Save();
        statusMessage = IsSeasonAdvanceBlockedByPendingDraftWork()
            ? $"Simmed from {startDate:MMM d} through season end: {daysSimulated} day(s), {selectedTeamGames} {selectedTeamName} game(s), {totalLeagueGames} league game(s), and {practiceDays} practice / recovery day(s). {GetNextSeasonBlockerMessage()}"
            : $"Simmed from {startDate:MMM d} through season end: {daysSimulated} day(s), {selectedTeamGames} {selectedTeamName} game(s), {totalLeagueGames} league game(s), and {practiceDays} practice / recovery day(s).";
        ReportAsyncOperationProgress(reportProgress, "Simming Season", statusMessage, 1d);
        return true;
    }

    public bool SimulateNextScheduledGame(out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before simulating games.";
            return false;
        }

        if (IsSeasonAdvanceBlockedByPendingDraftWork())
        {
            statusMessage = GetNextSeasonBlockerMessage();
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

    private void RunOffseasonSimulation(out OffseasonSummaryState offseasonSummary)
    {
        var completedSeasonYear = GetCurrentSeasonYear();
        var selectedTeamName = SelectedTeam?.Name ?? string.Empty;
        offseasonSummary = new OffseasonSummaryState
        {
            CompletedSeasonYear = completedSeasonYear,
            NewSeasonYear = completedSeasonYear + 1
        };

        _saveState.PreviousSeasonStats = CloneSeasonStatsMap(_saveState.PlayerSeasonStats);

        var expiringContracts = ExpirePlayerContracts();
        offseasonSummary.ExpiringContracts = expiringContracts.Count;
        ExpireCoachContracts();

        offseasonSummary.ExtensionsCompleted = RunExtensionNegotiations(
            selectedTeamName,
            offseasonSummary.SelectedTeamContractDecisions,
            out var extensionOffers);
        offseasonSummary.ExtensionOffers = extensionOffers;

        var unsignedFreeAgents = new HashSet<Guid>();

        foreach (var expiringContract in expiringContracts
                     .OrderBy(contract => contract.TeamName)
                     .ThenBy(contract => contract.Player.FullName))
        {
            var evaluation = EvaluateContractForTeam(expiringContract.Player, expiringContract.TeamName, expiringContract.AnnualSalary);
            offseasonSummary.LeagueOfferCount++;

            if (evaluation.Accepted)
            {
                SignPlayerToTeam(expiringContract.Player, expiringContract.TeamName, evaluation.AgreedAnnualSalary, evaluation.AgreedYears);
                AddTransferRecord(expiringContract.TeamName, $"Re-signed {expiringContract.Player.FullName} for {GetBudgetDisplay(evaluation.AgreedAnnualSalary)} over {evaluation.AgreedYears} year(s).");
                offseasonSummary.FreeAgentsSigned++;

                if (string.Equals(expiringContract.TeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
                {
                    offseasonSummary.SelectedTeamContractDecisions.Add(BuildContractDecision(expiringContract.Player.FullName, expiringContract.TeamName, "Re-signed", evaluation));
                }

                continue;
            }

            _saveState.PlayerAssignments[expiringContract.Player.PlayerId] = FreeAgentTeamName;
            unsignedFreeAgents.Add(expiringContract.Player.PlayerId);

            if (string.Equals(expiringContract.TeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
            {
                offseasonSummary.SelectedTeamContractDecisions.Add(BuildContractDecision(expiringContract.Player.FullName, expiringContract.TeamName, "Walked to free agency", evaluation));
            }
        }

        var leagueOfferCount = offseasonSummary.LeagueOfferCount;
        offseasonSummary.FreeAgentsSigned += RunFreeAgency(unsignedFreeAgents, selectedTeamName, offseasonSummary.SelectedTeamContractDecisions, ref leagueOfferCount);
        offseasonSummary.LeagueOfferCount = leagueOfferCount;
        offseasonSummary.TradesCompleted = RunOffseasonTrades(selectedTeamName, offseasonSummary.SelectedTeamTradeDecisions, offseasonSummary.LeagueNotes);

        AdvanceSeasonBoundary(completedSeasonYear + 1);
        var selectedTeamSummary = offseasonSummary.SelectedTeamContractDecisions.Count == 0 && offseasonSummary.SelectedTeamTradeDecisions.Count == 0
            ? "Your club stood pat through the offseason."
            : $"Your club logged {offseasonSummary.SelectedTeamContractDecisions.Count} contract decision(s) and {offseasonSummary.SelectedTeamTradeDecisions.Count} trade move(s).";
        offseasonSummary.Overview = $"Advanced from {completedSeasonYear} to {offseasonSummary.NewSeasonYear}. {offseasonSummary.ExpiringContracts} contract(s) expired, {offseasonSummary.ExtensionOffers} extension offer(s) went out with {offseasonSummary.ExtensionsCompleted} completed, {offseasonSummary.FreeAgentsSigned} free-agent signing(s) were completed, and {offseasonSummary.TradesCompleted} trade(s) landed across the league. {selectedTeamSummary}";
    }

    private List<ExpiringPlayerContract> ExpirePlayerContracts()
    {
        var expiringContracts = new List<ExpiringPlayerContract>();

        foreach (var team in _leagueData.Teams)
        {
            var economy = GetOrCreateTeamState(team.Name).Economy;
            var activeContracts = new List<BaseballManager.Core.Economy.Contract>();
            foreach (var contract in economy.PlayerContracts)
            {
                contract.YearsRemaining = Math.Max(0, contract.YearsRemaining - 1);
                if (contract.YearsRemaining > 0)
                {
                    activeContracts.Add(contract);
                    continue;
                }

                var player = FindPlayerImport(contract.SubjectId);
                if (player != null)
                {
                    expiringContracts.Add(new ExpiringPlayerContract(team.Name, player, contract.AnnualSalary));
                }
            }

            economy.PlayerContracts = activeContracts;
            economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
        }

        return expiringContracts;
    }

    private void ExpireCoachContracts()
    {
        foreach (var team in _leagueData.Teams)
        {
            var economy = GetOrCreateTeamState(team.Name).Economy;
            foreach (var contract in economy.CoachContracts)
            {
                contract.YearsRemaining = Math.Max(0, contract.YearsRemaining - 1);
            }

            economy.CoachContracts.RemoveAll(contract => contract.YearsRemaining <= 0);
            economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
        }
    }

    private int RunFreeAgency(HashSet<Guid> freeAgentIds, string selectedTeamName, List<OffseasonContractDecisionState> selectedTeamDecisions, ref int leagueOfferCount)
    {
        var signings = 0;
        var freeAgents = freeAgentIds
            .Select(FindPlayerImport)
            .Where(player => player != null)
            .Select(player => player!)
            .OrderByDescending(GetOffseasonMarketScore)
            .ThenBy(player => player.FullName)
            .ToList();

        foreach (var player in freeAgents)
        {
            FreeAgentOfferChoice? bestOffer = null;

            foreach (var team in _leagueData.Teams)
            {
                if (GetTeamRosterCount(team.Name) >= MaxRosterSize)
                {
                    continue;
                }

                var evaluation = EvaluateContractForTeam(player, team.Name, EstimatePlayerSalary(player));
                leagueOfferCount++;
                if (!evaluation.Accepted)
                {
                    continue;
                }

                var score = evaluation.AgreedAnnualSalary + (evaluation.AgreedYears * 250_000m) + (GetTeamNeedScore(team.Name, player.PrimaryPosition) * 100_000m);
                if (bestOffer == null || score > bestOffer.Value.Score)
                {
                    bestOffer = new FreeAgentOfferChoice(team.Name, evaluation, score);
                }
            }

            if (bestOffer == null)
            {
                _saveState.PlayerAssignments[player.PlayerId] = FreeAgentTeamName;
                continue;
            }

            var acceptedOffer = bestOffer.Value;
            SignPlayerToTeam(player, acceptedOffer.TeamName, acceptedOffer.Evaluation.AgreedAnnualSalary, acceptedOffer.Evaluation.AgreedYears);
            AddTransferRecord(acceptedOffer.TeamName, $"Signed free agent {player.FullName} for {GetBudgetDisplay(acceptedOffer.Evaluation.AgreedAnnualSalary)} over {acceptedOffer.Evaluation.AgreedYears} year(s).");
            signings++;

            if (string.Equals(acceptedOffer.TeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
            {
                selectedTeamDecisions.Add(BuildContractDecision(player.FullName, acceptedOffer.TeamName, "Signed free agent", acceptedOffer.Evaluation));
            }
        }

        return signings;
    }

    private int RunExtensionNegotiations(string selectedTeamName, List<OffseasonContractDecisionState> selectedTeamDecisions, out int extensionOffers)
    {
        extensionOffers = 0;
        var completedExtensions = 0;

        foreach (var team in _leagueData.Teams)
        {
            var economy = GetOrCreateTeamState(team.Name).Economy;
            foreach (var contract in economy.PlayerContracts
                         .Where(contract => contract.YearsRemaining == 1)
                         .OrderByDescending(contract => contract.AnnualSalary)
                         .ToList())
            {
                var player = FindPlayerImport(contract.SubjectId);
                if (player == null)
                {
                    continue;
                }

                extensionOffers++;
                var evaluation = EvaluateContractForTeam(player, team.Name, contract.AnnualSalary);
                var wantsExtension = evaluation.PlayerExpectation.Years >= 2 || evaluation.PlayerExpectation.AnnualSalary >= contract.AnnualSalary * 1.05m;
                if (!wantsExtension)
                {
                    continue;
                }

                var agreedYears = Math.Max(contract.YearsRemaining + 1, evaluation.AgreedYears + 1);
                var canAffordExtension = GetTransferBudget(team.Name) >= Math.Max(0m, evaluation.TeamExpectation.AnnualSalary - contract.AnnualSalary);
                if (evaluation.Accepted && canAffordExtension)
                {
                    _signPlayerContractUseCase.Execute(economy, player.PlayerId, player.FullName, evaluation.AgreedAnnualSalary, agreedYears, GetCurrentFranchiseDate());
                    AddTransferRecord(team.Name, $"Extended {player.FullName} for {GetBudgetDisplay(evaluation.AgreedAnnualSalary)} with {agreedYears} year(s) remaining.");
                    completedExtensions++;

                    if (string.Equals(team.Name, selectedTeamName, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedTeamDecisions.Add(BuildContractDecision(player.FullName, team.Name, "Extended", evaluation));
                    }
                }
                else if (string.Equals(team.Name, selectedTeamName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedTeamDecisions.Add(BuildContractDecision(player.FullName, team.Name, "Extension talks stalled", evaluation));
                }
            }
        }

        return completedExtensions;
    }

    private int RunOffseasonTrades(string selectedTeamName, List<OffseasonTradeDecisionState> selectedTeamTradeDecisions, List<string> leagueNotes)
    {
        var tradeCount = 0;
        var lockedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var buyingTeam in _leagueData.Teams.OrderBy(team => team.Name))
        {
            if (lockedTeams.Contains(buyingTeam.Name))
            {
                continue;
            }

            if (!TryExecuteOffseasonTrade(buyingTeam.Name, lockedTeams, out var tradeOutcome))
            {
                continue;
            }

            tradeCount++;
            leagueNotes.Add(tradeOutcome.Description);
            if (string.Equals(tradeOutcome.FromTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tradeOutcome.ToTeamName, selectedTeamName, StringComparison.OrdinalIgnoreCase))
            {
                selectedTeamTradeDecisions.Add(new OffseasonTradeDecisionState
                {
                    TeamName = selectedTeamName,
                    Description = tradeOutcome.Description
                });
            }
        }

        return tradeCount;
    }

    private void AdvanceSeasonBoundary(int newSeasonYear)
    {
        ApplyOffseasonAging();
        ResetHealthForNewSeason();
        _saveState.CurrentSeasonYear = newSeasonYear;
        _saveState.CurrentFranchiseDate = GetFranchiseStartDate().Date;
        _saveState.CompletedScheduleGameKeys.Clear();
        _saveState.CompletedScheduleGameResults.Clear();
        _saveState.CompletedGameBoxScores.Clear();
        _saveState.CurrentViewedBoxScoreGameKey = null;
        _saveState.LastCompletedLiveMatch = null;
        _saveState.CurrentLiveMatch = null;
        _saveState.QuickMatchLiveMatch = null;
        _saveState.PlayerSeasonStats = new Dictionary<Guid, PlayerSeasonStatsState>();
        _saveState.PlayerRecentGameStats = new Dictionary<Guid, List<PlayerRecentGameStatState>>();
        _saveState.PlayerRecentTrackingTotals = new Dictionary<Guid, PlayerRecentTotalsState>();
        _lastSeasonStatsCache.Clear();

        foreach (var team in _leagueData.Teams)
        {
            var teamState = GetOrCreateTeamState(team.Name);
            teamState.CurrentLiveMatch = null;
            teamState.TrainingReports.Clear();
            InitializeLineupSlots(teamState, team.Name);
            InitializeRotationSlots(teamState, team.Name);
            EnsureTeamEconomyInitialized(teamState, team.Name);
        }

        EnsurePlayerSeasonStatsGenerated();
        EnsureRecentGameTrackingGenerated();
    }

    private bool SimulateCurrentDayInternal(bool saveAfterAdvance, out string statusMessage)
    {
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before simulating the day.";
            return false;
        }

        if (IsSeasonAdvanceBlockedByPendingDraftWork())
        {
            statusMessage = GetNextSeasonBlockerMessage();
            return false;
        }

        if (IsRegularSeasonComplete())
        {
            statusMessage = $"The {GetCurrentSeasonYear()} regular season is complete. Use Next Season to move into the offseason.";
            return false;
        }

        var currentDate = GetCurrentFranchiseDate().Date;
        var todaysGames = GetRemainingScheduledGamesForDate(currentDate);
        var selectedTeamGames = todaysGames
            .Where(game =>
                string.Equals(game.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.AwayTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var otherGames = todaysGames
            .Where(game => !selectedTeamGames.Contains(game))
            .ToList();
        var gameSummaries = new List<string>();

        foreach (var game in selectedTeamGames)
        {
            if (!TrySimulateScheduledGame(game, advanceDateAfterGame: false, out var gameSummary))
            {
                statusMessage = gameSummary;
                return false;
            }

            gameSummaries.Add(gameSummary);
        }

        SimulateGamesInParallel(otherGames);

        var hasPractice = TryGetPracticeSessionInfo(currentDate, out var practiceSession);
        var developmentResults = ApplyPracticeDevelopmentAcrossLeague(currentDate, SelectedTeam.Name);

        var holdAtSeasonBoundary = ShouldHoldAtSeasonBoundaryForDraft(currentDate);
        if (!holdAtSeasonBoundary)
        {
            AdvanceFranchiseDateTo(currentDate.AddDays(1));
        }

        ClearLiveMatchState(LiveMatchMode.Franchise);
        if (saveAfterAdvance)
        {
            Save();
        }

        if (gameSummaries.Count > 0)
        {
            statusMessage = otherGames.Count > 0
                ? $"{currentDate:ddd, MMM d}: {string.Join(" ", gameSummaries)} Around the league, {otherGames.Count} other game(s) were played."
                : $"{currentDate:ddd, MMM d}: {string.Join(" ", gameSummaries)}";
            if (holdAtSeasonBoundary)
            {
                statusMessage = $"{statusMessage} {GetNextSeasonBlockerMessage()}";
            }

            return true;
        }

        if (hasPractice)
        {
            var practiceSummary = BuildPracticeDevelopmentReport(currentDate, practiceSession, developmentResults);
            statusMessage = todaysGames.Count > 0
                ? $"{practiceSummary} Around the league, {todaysGames.Count} game(s) were played."
                : practiceSummary;
            if (holdAtSeasonBoundary)
            {
                statusMessage = $"{statusMessage} {GetNextSeasonBlockerMessage()}";
            }

            return true;
        }

        statusMessage = todaysGames.Count > 0
            ? $"Advanced through {currentDate:ddd, MMM d}. {todaysGames.Count} league game(s) were played."
            : $"Advanced through {currentDate:ddd, MMM d}. No game or full-team workout was on the calendar.";
        if (holdAtSeasonBoundary)
        {
            statusMessage = $"{statusMessage} {GetNextSeasonBlockerMessage()}";
        }

        return true;
    }

    private bool TrySimulateScheduledGame(ScheduleImportDto scheduledGame, bool advanceDateAfterGame, out string summary)
    {
        if (!TryPrepareGameSimulation(scheduledGame, out var preparedGame, out summary) ||
            !TrySimulatePreparedGame(preparedGame, applyPerformanceDevelopmentDuringSim: true, out var finalState, out summary))
        {
            return false;
        }

        RecordCompletedGame(finalState);
        var completedScheduledGame = FinalizeFranchiseScheduledGame(finalState, scheduledGame, advanceDateAfterGame) ?? scheduledGame;
        var completedSummary = BuildCompletedLiveMatchSummary(finalState, LiveMatchMode.Franchise, completedScheduledGame);
        StoreCompletedGameBoxScore(finalState, completedScheduledGame, completedSummary, setCurrentView: false);
        return true;
    }

    private bool TryPrepareGameSimulation(ScheduleImportDto scheduledGame, out PreparedGameSimulation preparedGame, out string summary)
    {
        preparedGame = default;

        var awayTeam = FindTeamByName(scheduledGame.AwayTeamName);
        var homeTeam = FindTeamByName(scheduledGame.HomeTeamName);
        if (awayTeam == null || homeTeam == null)
        {
            summary = "Could not resolve teams for the scheduled game.";
            return false;
        }

        var useFranchiseSelectionsForAway = SelectedTeam != null && string.Equals(SelectedTeam.Name, awayTeam.Name, StringComparison.OrdinalIgnoreCase);
        var useFranchiseSelectionsForHome = SelectedTeam != null && string.Equals(SelectedTeam.Name, homeTeam.Name, StringComparison.OrdinalIgnoreCase);
        var awaySnapshot = BuildTeamSnapshot(awayTeam, homeTeam.Name, useFranchiseSelectionsForAway);
        var homeSnapshot = BuildTeamSnapshot(homeTeam, awayTeam.Name, useFranchiseSelectionsForHome);

        preparedGame = new PreparedGameSimulation(scheduledGame, awaySnapshot, homeSnapshot, awayTeam.Abbreviation, homeTeam.Abbreviation);
        summary = string.Empty;
        return true;
    }

    private bool TrySimulatePreparedGame(PreparedGameSimulation preparedGame, bool applyPerformanceDevelopmentDuringSim, out MatchState finalState, out string summary)
    {
        var engine = new MatchEngine(preparedGame.AwaySnapshot, preparedGame.HomeSnapshot);

        while (!engine.CurrentState.IsGameOver)
        {
            if (_managerAi.TryApplyDefensiveManagerDecision(engine.CurrentState))
            {
                continue;
            }

            var batter = engine.CurrentState.CurrentBatter;
            var pitcher = engine.CurrentState.CurrentPitcher;
            var defensiveTeam = engine.CurrentState.DefensiveTeam;
            var result = engine.Tick();
            if (applyPerformanceDevelopmentDuringSim)
            {
                ApplyPerformanceDevelopment(batter, pitcher, defensiveTeam, result);
            }
        }

        finalState = engine.CurrentState;
        summary = $"{preparedGame.AwayAbbreviation} {finalState.AwayTeam.Runs} - {finalState.HomeTeam.Runs} {preparedGame.HomeAbbreviation}.";
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
                ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Discipline, 0.12d);
                if (result.RunsScored > 0)
                {
                    ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Contact, 0.08d);
                }
                break;

            case "Single":
                ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Contact, 0.10d);
                if (result.RunsScored > 0)
                {
                    ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Speed, 0.08d);
                }
                break;

            case "Double":
                ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Contact, 0.10d);
                ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Power, 0.10d);
                break;

            case "Triple":
                ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Contact, 0.10d);
                ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Speed, 0.14d);
                break;

            case "HomeRun":
                ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Power, 0.16d);
                ApplyAgeAdjustedPerformanceGain(batter, PracticeDevelopmentAttribute.Contact, 0.08d);
                break;
        }

        if (result.OutsRecorded > 0)
        {
            ApplyAgeAdjustedPerformanceGain(pitcher, PracticeDevelopmentAttribute.Pitching, result.Code == "Strikeout" ? 0.12d : 0.08d);
            ApplyAgeAdjustedPerformanceGain(pitcher, PracticeDevelopmentAttribute.Durability, 0.04d);

            var fielder = defensiveTeam.FindFielder(result.Fielder);
            if (fielder != null && fielder.Id != pitcher.Id)
            {
                ApplyAgeAdjustedPerformanceGain(fielder, PracticeDevelopmentAttribute.Fielding, 0.05d);
                if (result.Code is "Groundout" or "Flyout")
                {
                    ApplyAgeAdjustedPerformanceGain(fielder, PracticeDevelopmentAttribute.Arm, 0.05d);
                }
            }
        }
    }

    private void ApplyAgeAdjustedPerformanceGain(MatchPlayerSnapshot player, PracticeDevelopmentAttribute attribute, double baseAmount)
    {
        if (baseAmount <= 0d)
        {
            return;
        }

        var multiplier = GetPerformanceDevelopmentMultiplier(player.Age, player.PrimaryPosition is "SP" or "RP");
        var adjustedAmount = baseAmount * multiplier;
        if (Math.Abs(adjustedAmount) < 0.01d)
        {
            return;
        }

        AdjustPlayerRatings(player.Id, ratings => ApplyPracticeDevelopmentGain(ratings, attribute, adjustedAmount));
    }

    private static double GetPerformanceDevelopmentMultiplier(int age, bool isPitcher)
    {
        var peakAge = isPitcher ? 30 : 29;
        if (age <= peakAge - 4)
        {
            return 1.10d;
        }

        if (age <= peakAge)
        {
            return 1.00d;
        }

        if (age <= peakAge + 3)
        {
            return 0.70d;
        }

        if (age <= peakAge + 6)
        {
            return 0.35d;
        }

        if (age <= peakAge + 8)
        {
            return 0.15d;
        }

        if (age <= peakAge + 10)
        {
            return 0.00d;
        }

        if (age <= peakAge + 12)
        {
            return -0.25d;
        }

        if (age <= 39)
        {
            return -0.60d;
        }

        return -0.90d;
    }

    public void RecordCompletedGame(MatchState finalState)
    {
        var awayWon = finalState.AwayTeam.Runs > finalState.HomeTeam.Runs;
        ApplyCompletedGame(finalState.AwayTeam, awayWon);
        ApplyCompletedGame(finalState.HomeTeam, !awayWon);
        RecordRecentGameStats(finalState);
    }

    public IReadOnlyList<FranchiseRosterEntry> GetSelectedTeamRoster(LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        return SelectedTeam == null ? [] : GetTeamRoster(SelectedTeam.Name, lineupPresetType);
    }

    public IReadOnlyList<FranchiseRosterEntry> GetLineupPlayers(LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        return GetSelectedTeamRoster(lineupPresetType)
            .Where(entry => entry.LineupSlot.HasValue)
            .OrderBy(entry => entry.LineupSlot)
            .ThenBy(entry => entry.PlayerName)
            .ToList();
    }

    public LineupValidationView GetSelectedTeamLineupValidation(LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        if (SelectedTeam == null)
        {
            return new LineupValidationView(
                false,
                "No team is selected.",
                ["C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH"],
                BuildEmptyLineupPositionAssignments(["C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH"]));
        }

        return BuildLineupValidation(GetLineupPlayers(lineupPresetType));
    }

    public LineupPresetType GetSelectedTeamPregameLineupPreset()
    {
        var opposingStarter = GetSelectedTeamPregameOpposingStarter();
        return ResolveLineupPresetType(opposingStarter?.Throws ?? Handedness.Right);
    }

    public LineupValidationView GetSelectedTeamPregameLineupValidation()
    {
        return GetSelectedTeamLineupValidation(GetSelectedTeamPregameLineupPreset());
    }

    public IReadOnlyList<FranchiseRosterEntry> GetBenchPlayers(LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        return GetSelectedTeamRoster(lineupPresetType)
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

    private FranchiseRosterEntry? GetSelectedTeamPregameOpposingStarter()
    {
        if (SelectedTeam == null)
        {
            return null;
        }

        var nextGame = GetNextScheduledGame();
        if (nextGame == null)
        {
            return null;
        }

        var opponentTeamName = string.Equals(nextGame.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase)
            ? nextGame.AwayTeamName
            : nextGame.HomeTeamName;
        return GetScheduledStartingPitcher(opponentTeamName);
    }

    private LineupPresetType GetLineupPresetForOpponent(string opposingTeamName)
    {
        return ResolveLineupPresetType(GetScheduledStartingPitcher(opposingTeamName)?.Throws ?? Handedness.Right);
    }

    private static LineupPresetType ResolveLineupPresetType(Handedness pitcherThrows)
    {
        return pitcherThrows == Handedness.Left
            ? LineupPresetType.VsLeftHandedPitcher
            : LineupPresetType.VsRightHandedPitcher;
    }

    public void AssignLineupSlot(Guid playerId, int slotNumber, LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        if (SelectedTeam == null || slotNumber < 1 || slotNumber > 9)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        InitializeLineupSlots(teamState, SelectedTeam.Name);
        AssignPlayerToSlotWithSwap(GetLineupSlotsForPreset(teamState, SelectedTeam.Name, lineupPresetType), playerId, slotNumber - 1);
        SanitizeDesignatedHitterForPreset(teamState, SelectedTeam.Name, lineupPresetType);
        Save();
    }

    public void ClearLineupSlot(int slotNumber, LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        if (SelectedTeam == null || slotNumber < 1 || slotNumber > 9)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        InitializeLineupSlots(teamState, SelectedTeam.Name);
        var lineupSlots = GetLineupSlotsForPreset(teamState, SelectedTeam.Name, lineupPresetType);
        lineupSlots[slotNumber - 1] = null;
        SanitizeDesignatedHitterForPreset(teamState, SelectedTeam.Name, lineupPresetType);
        Save();
    }

    public void SetSelectedTeamDesignatedHitter(Guid playerId, LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        if (SelectedTeam == null)
        {
            return;
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        InitializeLineupSlots(teamState, SelectedTeam.Name);
        var lineupSlots = GetLineupSlotsForPreset(teamState, SelectedTeam.Name, lineupPresetType);
        if (!lineupSlots.Contains(playerId))
        {
            return;
        }

        SetDesignatedHitterPlayerIdForPreset(teamState, lineupPresetType, playerId);
        Save();
    }

    public FranchiseRosterEntry? GetSelectedTeamDesignatedHitter(LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        if (SelectedTeam == null)
        {
            return null;
        }

        var designatedHitterPlayerId = GetDesignatedHitterPlayerId(SelectedTeam.Name, lineupPresetType);
        if (!designatedHitterPlayerId.HasValue)
        {
            return null;
        }

        return GetSelectedTeamRoster(lineupPresetType).FirstOrDefault(player => player.PlayerId == designatedHitterPlayerId.Value);
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

    private LineupValidationView BuildLineupValidation(IReadOnlyList<FranchiseRosterEntry> lineupPlayers)
    {
        string[] requiredPositions = ["C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH"];

        if (lineupPlayers.Count != 9)
        {
            var missingPlayerCount = Math.Max(0, 9 - lineupPlayers.Count);
            var countSummary = missingPlayerCount > 0
                ? $"Lineup is incomplete: add {missingPlayerCount} more player{(missingPlayerCount == 1 ? string.Empty : "s")} to reach nine starters."
                : "Lineup has too many assigned players.";
            return new LineupValidationView(false, countSummary, requiredPositions, BuildEmptyLineupPositionAssignments(requiredPositions));
        }

        if (lineupPlayers.Select(player => player.PlayerId).Distinct().Count() != lineupPlayers.Count)
        {
            return new LineupValidationView(false, "Lineup is invalid: each batting slot must contain a different player.", requiredPositions, BuildEmptyLineupPositionAssignments(requiredPositions));
        }

        var matchedPlayersByPosition = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var player in lineupPlayers)
        {
            var visitedPositions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TryAssignLineupValidationPosition(player, lineupPlayers, requiredPositions, matchedPlayersByPosition, visitedPositions);
        }

        var missingPositions = requiredPositions
            .Where(position => !matchedPlayersByPosition.ContainsKey(position))
            .ToList();
        var positionAssignments = BuildLineupPositionAssignments(requiredPositions, matchedPlayersByPosition, lineupPlayers);

        if (missingPositions.Count == 0)
        {
            return new LineupValidationView(true, "Lineup is valid: all field positions and DH are covered.", [], positionAssignments);
        }

        return new LineupValidationView(
            false,
            $"Lineup is invalid: missing coverage for {string.Join(", ", missingPositions)}.",
            missingPositions,
            positionAssignments);
    }

    private static IReadOnlyList<LineupPositionAssignmentView> BuildEmptyLineupPositionAssignments(IReadOnlyList<string> requiredPositions)
    {
        return requiredPositions
            .Select(position => new LineupPositionAssignmentView(position, null, "Missing", null))
            .ToList();
    }

    private static IReadOnlyList<LineupPositionAssignmentView> BuildLineupPositionAssignments(
        IReadOnlyList<string> requiredPositions,
        IReadOnlyDictionary<string, Guid> matchedPlayersByPosition,
        IReadOnlyList<FranchiseRosterEntry> lineupPlayers)
    {
        return requiredPositions
            .Select(position =>
            {
                if (!matchedPlayersByPosition.TryGetValue(position, out var playerId))
                {
                    return new LineupPositionAssignmentView(position, null, "Missing", null);
                }

                var player = lineupPlayers.FirstOrDefault(entry => entry.PlayerId == playerId);
                return player == null
                    ? new LineupPositionAssignmentView(position, null, "Missing", null)
                    : new LineupPositionAssignmentView(position, player.PlayerId, player.PlayerName, player.LineupSlot);
            })
            .ToList();
    }

    private static bool TryAssignLineupValidationPosition(
        FranchiseRosterEntry player,
        IReadOnlyList<FranchiseRosterEntry> lineupPlayers,
        IReadOnlyList<string> requiredPositions,
        Dictionary<string, Guid> matchedPlayersByPosition,
        HashSet<string> visitedPositions)
    {
        foreach (var position in requiredPositions)
        {
            if (visitedPositions.Contains(position) || !CanCoverLineupPosition(player, position))
            {
                continue;
            }

            visitedPositions.Add(position);

            if (!matchedPlayersByPosition.TryGetValue(position, out var existingPlayerId))
            {
                matchedPlayersByPosition[position] = player.PlayerId;
                return true;
            }

            var existingPlayer = lineupPlayers.FirstOrDefault(entry => entry.PlayerId == existingPlayerId);
            if (existingPlayer != null && TryAssignLineupValidationPosition(existingPlayer, lineupPlayers, requiredPositions, matchedPlayersByPosition, visitedPositions))
            {
                matchedPlayersByPosition[position] = player.PlayerId;
                return true;
            }
        }

        return false;
    }

    private static bool CanCoverLineupPosition(FranchiseRosterEntry player, string position)
    {
        if (string.Equals(position, "DH", StringComparison.OrdinalIgnoreCase))
        {
            return player.IsDesignatedHitter;
        }

        if (player.IsDesignatedHitter)
        {
            return false;
        }

        return PositionMatchesLineupRole(player.PrimaryPosition, position)
            || PositionMatchesLineupRole(player.SecondaryPosition, position);
    }

    private static bool PositionMatchesLineupRole(string playerPosition, string targetPosition)
    {
        if (string.IsNullOrWhiteSpace(playerPosition))
        {
            return false;
        }

        if (string.Equals(playerPosition, targetPosition, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return targetPosition switch
        {
            "LF" or "CF" or "RF" => string.Equals(playerPosition, "OF", StringComparison.OrdinalIgnoreCase),
            "1B" or "2B" or "3B" or "SS" => string.Equals(playerPosition, "IF", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public CompletedLiveMatchSummaryState? GetLastCompletedLiveMatchSummary()
    {
        return _saveState.LastCompletedLiveMatch;
    }

    public CompletedGameBoxScoreState? GetCurrentCompletedGameBoxScore()
    {
        if (!string.IsNullOrWhiteSpace(_saveState.CurrentViewedBoxScoreGameKey) &&
            _saveState.CompletedGameBoxScores.TryGetValue(_saveState.CurrentViewedBoxScoreGameKey, out var selectedBoxScore))
        {
            return selectedBoxScore;
        }

        var summary = _saveState.LastCompletedLiveMatch;
        if (summary == null)
        {
            return null;
        }

        var summaryKey = BuildCompletedGameBoxScoreKey(completedScheduledGame: null, summary);
        return _saveState.CompletedGameBoxScores.TryGetValue(summaryKey, out var boxScore)
            ? boxScore
            : null;
    }

    public bool HasCompletedGameBoxScore(ScheduleImportDto game)
    {
        return _saveState.CompletedGameBoxScores.ContainsKey(BuildScheduleGameKey(game));
    }

    public bool OpenCompletedGameBoxScore(ScheduleImportDto game)
    {
        var key = BuildScheduleGameKey(game);
        if (!_saveState.CompletedGameBoxScores.TryGetValue(key, out var boxScore))
        {
            return false;
        }

        _preferLiveBoxScore = false;
        _saveState.CurrentViewedBoxScoreGameKey = key;
        _saveState.LastCompletedLiveMatch = boxScore.Summary;
        Save();
        return true;
    }

    public void CaptureCompletedLiveMatch(MatchState finalState, LiveMatchMode mode, ScheduleImportDto? completedScheduledGame = null)
    {
        _preferLiveBoxScore = false;
        completedScheduledGame ??= ResolveCompletedScheduledGame(finalState);
        var summary = BuildCompletedLiveMatchSummary(finalState, mode, completedScheduledGame);
        _saveState.LastCompletedLiveMatch = summary;
        StoreCompletedGameBoxScore(finalState, completedScheduledGame, summary, setCurrentView: true);
        Save();
    }

    private CompletedLiveMatchSummaryState BuildCompletedLiveMatchSummary(MatchState finalState, LiveMatchMode mode, ScheduleImportDto? completedScheduledGame)
    {
        var awayRuns = finalState.AwayTeam.Runs;
        var homeRuns = finalState.HomeTeam.Runs;
        var winningTeam = awayRuns == homeRuns
            ? "Tie"
            : (awayRuns > homeRuns ? finalState.AwayTeam.Name : finalState.HomeTeam.Name);
        var awayRecord = GetTeamRecordSummary(finalState.AwayTeam.Name);
        var homeRecord = GetTeamRecordSummary(finalState.HomeTeam.Name);
        var selectedTeamName = mode == LiveMatchMode.Franchise ? SelectedTeam?.Name ?? string.Empty : string.Empty;
        var nextGame = mode == LiveMatchMode.Franchise ? GetNextScheduledGameForSelectedTeam() : null;

        return new CompletedLiveMatchSummaryState
        {
            AwayTeamName = finalState.AwayTeam.Name,
            HomeTeamName = finalState.HomeTeam.Name,
            AwayAbbreviation = finalState.AwayTeam.Abbreviation,
            HomeAbbreviation = finalState.HomeTeam.Abbreviation,
            AwayRuns = awayRuns,
            HomeRuns = homeRuns,
            AwayHits = finalState.AwayTeam.Hits,
            HomeHits = finalState.HomeTeam.Hits,
            AwayPitchCount = finalState.AwayTeam.PitchCount,
            HomePitchCount = finalState.HomeTeam.PitchCount,
            AwayStartingPitcherName = finalState.AwayTeam.StartingPitcher.FullName,
            HomeStartingPitcherName = finalState.HomeTeam.StartingPitcher.FullName,
            ScheduledDate = completedScheduledGame?.Date.Date ?? DateTime.Today,
            GameNumber = completedScheduledGame?.GameNumber ?? 1,
            Venue = string.IsNullOrWhiteSpace(completedScheduledGame?.Venue) ? $"{finalState.HomeTeam.Name} Ballpark" : completedScheduledGame.Venue,
            AwayRecord = BuildRecordSummaryLabel(awayRecord),
            HomeRecord = BuildRecordSummaryLabel(homeRecord),
            FinalInningNumber = finalState.Inning.Number,
            EndedInTopHalf = finalState.Inning.IsTopHalf,
            CompletedPlays = finalState.CompletedPlays,
            WasFranchiseMatch = mode == LiveMatchMode.Franchise,
            WinningTeamName = winningTeam,
            SelectedTeamName = selectedTeamName,
            SelectedTeamResultLabel = BuildSelectedTeamResultLabel(finalState, selectedTeamName),
            NextGameLabel = BuildNextGameLabel(nextGame),
            FranchiseDateAfterGame = mode == LiveMatchMode.Franchise ? GetCurrentFranchiseDate() : DateTime.MinValue,
            FinalPlayDescription = finalState.LatestEvent.Description,
            CompletedAtUtc = DateTime.UtcNow
        };
    }

    public OrganizationRosterCompositionView GetSelectedTeamAffiliateRosterComposition(MinorLeagueAffiliateLevel affiliateLevel)
    {
        if (SelectedTeam == null)
        {
            var emptyLabel = GetAffiliateLevelLabel(affiliateLevel);
            return CreateEmptyRosterComposition($"{emptyLabel} Affiliate", "Select a team to review an affiliate roster.", AffiliateRosterSize);
        }

        var affiliateLabel = GetAffiliateLevelLabel(affiliateLevel);
        var players = GetSelectedTeamOrganizationRoster()
            .Where(player => player.AffiliateLevel == affiliateLevel)
            .ToList();

        return players.Count == 0
            ? CreateEmptyRosterComposition($"{affiliateLabel} Affiliate", $"No players are currently assigned to the {SelectedTeam.Name} {affiliateLabel} affiliate.", AffiliateRosterSize)
            : BuildRosterComposition(
                $"{affiliateLabel} Affiliate",
                $"Viewing the {SelectedTeam.Name} {affiliateLabel} affiliate roster as its own 26-man group.",
                players,
                AffiliateRosterSize);
    }

    private void StoreCompletedGameBoxScore(MatchState finalState, ScheduleImportDto? completedScheduledGame, CompletedLiveMatchSummaryState summary, bool setCurrentView)
    {
        var key = BuildCompletedGameBoxScoreKey(completedScheduledGame, summary);
        var scheduledDate = summary.ScheduledDate == default ? GetCurrentFranchiseDate().Date : summary.ScheduledDate.Date;

        _saveState.CompletedGameBoxScores[key] = new CompletedGameBoxScoreState
        {
            GameKey = key,
            Summary = summary,
            AwayRunsByInning = finalState.AwayRunsByInning.Count > 0 ? [.. finalState.AwayRunsByInning] : BuildFallbackLinescore(finalState.AwayTeam.Runs, finalState.Inning.Number),
            HomeRunsByInning = finalState.HomeRunsByInning.Count > 0 ? [.. finalState.HomeRunsByInning] : BuildFallbackLinescore(finalState.HomeTeam.Runs, finalState.Inning.Number),
            AwayErrors = Math.Max(0, finalState.AwayErrors),
            HomeErrors = Math.Max(0, finalState.HomeErrors),
            AwayPitchingLines = BuildPitchingLines(finalState.AwayTeam, scheduledDate, finalState.AwayTeam.Abbreviation),
            HomePitchingLines = BuildPitchingLines(finalState.HomeTeam, scheduledDate, finalState.HomeTeam.Abbreviation),
            NotablePlayers = BuildNotablePlayerHighlights(finalState, scheduledDate)
        };

        if (setCurrentView || string.IsNullOrWhiteSpace(_saveState.CurrentViewedBoxScoreGameKey))
        {
            _saveState.CurrentViewedBoxScoreGameKey = key;
        }
    }

    private List<CompletedPitchingLineState> BuildPitchingLines(MatchTeamState team, DateTime gameDate, string teamAbbreviation)
    {
        var pitcherEntries = team.PitchCountsByPitcher
            .Where(pair => pair.Value > 0)
            .Select(pair => new { Pitcher = team.FindPlayer(pair.Key), PitchCount = pair.Value })
            .Where(entry => entry.Pitcher != null)
            .Select(entry => new { Pitcher = entry.Pitcher!, entry.PitchCount })
            .ToList();

        if (pitcherEntries.Count == 0)
        {
            pitcherEntries.Add(new { Pitcher = team.StartingPitcher, PitchCount = Math.Max(0, team.PitchCount) });
        }

        return pitcherEntries
            .Select(entry =>
            {
                var recentLine = GetMostRecentGameLine(entry.Pitcher.Id, gameDate);
                return new CompletedPitchingLineState
                {
                    TeamAbbreviation = teamAbbreviation,
                    PitcherName = entry.Pitcher.FullName,
                    IsStartingPitcher = entry.Pitcher.Id == team.StartingPitcher.Id,
                    PitchCount = Math.Max(0, entry.PitchCount),
                    InningsPitchedOuts = Math.Max(0, recentLine?.InningsPitchedOuts ?? 0),
                    EarnedRuns = Math.Max(0, recentLine?.EarnedRuns ?? 0),
                    Strikeouts = Math.Max(0, recentLine?.PitcherStrikeouts ?? 0)
                };
            })
            .OrderByDescending(line => line.IsStartingPitcher)
            .ThenByDescending(line => line.PitchCount)
            .ThenBy(line => line.PitcherName)
            .ToList();
    }

    private List<CompletedPlayerHighlightState> BuildNotablePlayerHighlights(MatchState finalState, DateTime gameDate)
    {
        var players = finalState.AwayTeam.Lineup
            .Concat(finalState.AwayTeam.BenchPlayers)
            .Concat(finalState.AwayTeam.PitchCountsByPitcher.Keys.Select(id => finalState.AwayTeam.FindPlayer(id)).OfType<MatchPlayerSnapshot>())
            .Concat(finalState.HomeTeam.Lineup)
            .Concat(finalState.HomeTeam.BenchPlayers)
            .Concat(finalState.HomeTeam.PitchCountsByPitcher.Keys.Select(id => finalState.HomeTeam.FindPlayer(id)).OfType<MatchPlayerSnapshot>())
            .GroupBy(player => player.Id)
            .Select(group => group.First())
            .ToList();

        var highlightCandidates = players
            .Select(player =>
            {
                var recentLine = GetMostRecentGameLine(player.Id, gameDate);
                if (recentLine == null)
                {
                    return null;
                }

                var teamAbbreviation = finalState.AwayTeam.FindPlayer(player.Id) != null
                    ? finalState.AwayTeam.Abbreviation
                    : finalState.HomeTeam.Abbreviation;
                return new CompletedPlayerHighlightState
                {
                    TeamAbbreviation = teamAbbreviation,
                    PlayerName = player.FullName,
                    PrimaryPosition = player.PrimaryPosition,
                    RunsScored = Math.Max(0, recentLine.RunsScored),
                    Hits = Math.Max(0, recentLine.Hits),
                    HomeRuns = Math.Max(0, recentLine.HomeRuns),
                    Walks = Math.Max(0, recentLine.Walks),
                    Strikeouts = Math.Max(0, recentLine.PitcherStrikeouts),
                    SummaryLine = BuildHighlightSummaryLine(recentLine)
                };
            })
            .OfType<CompletedPlayerHighlightState>()
            .ToList();

        var notablePlayers = highlightCandidates
            .Where(player => player.HomeRuns > 0 || player.RunsScored > 0 || player.Hits >= 2 || player.Strikeouts >= 3)
            .OrderByDescending(player => player.HomeRuns)
            .ThenByDescending(player => player.RunsScored)
            .ThenByDescending(player => player.Hits)
            .ThenByDescending(player => player.Strikeouts)
            .ThenBy(player => player.PlayerName)
            .Take(6)
            .ToList();

        if (notablePlayers.Count > 0)
        {
            return notablePlayers;
        }

        return highlightCandidates
            .Where(player => player.Hits > 0 || player.Strikeouts > 0)
            .OrderByDescending(player => player.Hits)
            .ThenByDescending(player => player.Strikeouts)
            .ThenBy(player => player.PlayerName)
            .Take(4)
            .ToList();
    }

    private PlayerRecentGameStatState? GetMostRecentGameLine(Guid playerId, DateTime gameDate)
    {
        if (!_saveState.PlayerRecentGameStats.TryGetValue(playerId, out var recentGames) || recentGames.Count == 0)
        {
            return null;
        }

        return recentGames.LastOrDefault(game => game.GameDate.Date == gameDate.Date);
    }

    private static string BuildHighlightSummaryLine(PlayerRecentGameStatState gameLine)
    {
        var parts = new List<string>();
        if (gameLine.RunsScored > 0)
        {
            parts.Add($"{gameLine.RunsScored} R");
        }

        if (gameLine.Hits > 0)
        {
            parts.Add($"{gameLine.Hits} H");
        }

        if (gameLine.HomeRuns > 0)
        {
            parts.Add(gameLine.HomeRuns == 1 ? "HR" : $"{gameLine.HomeRuns} HR");
        }

        if (gameLine.Walks > 0)
        {
            parts.Add($"{gameLine.Walks} BB");
        }

        if (gameLine.PitcherStrikeouts > 0)
        {
            parts.Add($"{gameLine.PitcherStrikeouts} K");
        }

        return parts.Count == 0 ? "Contributed defensively." : string.Join(", ", parts);
    }

    private static List<int> BuildFallbackLinescore(int totalRuns, int inningCount)
    {
        var line = Enumerable.Repeat(0, Math.Max(9, Math.Max(1, inningCount))).ToList();
        if (line.Count > 0)
        {
            line[Math.Min(line.Count - 1, Math.Max(0, inningCount - 1))] = Math.Max(0, totalRuns);
        }

        return line;
    }

    private ScheduleImportDto? ResolveCompletedScheduledGame(MatchState finalState)
    {
        return GetActiveSchedule()
            .Where(game =>
                string.Equals(game.AwayTeamName, finalState.AwayTeam.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(game.HomeTeamName, finalState.HomeTeam.Name, StringComparison.OrdinalIgnoreCase) &&
                _saveState.CompletedScheduleGameKeys.Contains(BuildScheduleGameKey(game)))
            .OrderByDescending(game => game.Date)
            .ThenByDescending(game => game.GameNumber ?? 1)
            .FirstOrDefault();
    }

    private static string BuildCompletedGameBoxScoreKey(ScheduleImportDto? completedScheduledGame, CompletedLiveMatchSummaryState summary)
    {
        if (completedScheduledGame != null)
        {
            return BuildScheduleGameKey(completedScheduledGame);
        }

        var scheduledDate = summary.ScheduledDate == default
            ? summary.CompletedAtUtc.ToLocalTime().Date
            : summary.ScheduledDate.Date;
        return $"{scheduledDate:yyyyMMdd}|{summary.HomeTeamName}|{summary.AwayTeamName}|{Math.Max(1, summary.GameNumber)}";
    }

    private static string BuildRecordSummaryLabel(TeamRecordSummary summary)
    {
        return summary.Streak == "-"
            ? $"{summary.Wins}-{summary.Losses}"
            : $"{summary.Wins}-{summary.Losses} ({summary.Streak})";
    }

    private static string BuildNextGameLabel(ScheduleImportDto? nextGame)
    {
        if (nextGame == null)
        {
            return "Schedule complete - no remaining franchise games.";
        }

        var venueLabel = string.IsNullOrWhiteSpace(nextGame.Venue)
            ? string.Empty
            : $" | {nextGame.Venue}";
        return $"{nextGame.AwayTeamName} at {nextGame.HomeTeamName} on {nextGame.Date:ddd, MMM d}{venueLabel}";
    }

    private static string BuildSelectedTeamResultLabel(MatchState finalState, string selectedTeamName)
    {
        if (string.IsNullOrWhiteSpace(selectedTeamName))
        {
            return string.Empty;
        }

        MatchTeamState? selectedTeam = null;
        MatchTeamState? opponent = null;

        if (string.Equals(finalState.AwayTeam.Name, selectedTeamName, StringComparison.OrdinalIgnoreCase))
        {
            selectedTeam = finalState.AwayTeam;
            opponent = finalState.HomeTeam;
        }
        else if (string.Equals(finalState.HomeTeam.Name, selectedTeamName, StringComparison.OrdinalIgnoreCase))
        {
            selectedTeam = finalState.HomeTeam;
            opponent = finalState.AwayTeam;
        }

        if (selectedTeam == null || opponent == null)
        {
            return string.Empty;
        }

        var resultLabel = selectedTeam.Runs > opponent.Runs
            ? "Win"
            : selectedTeam.Runs < opponent.Runs
                ? "Loss"
                : "Tie";
        var locationLabel = ReferenceEquals(selectedTeam, finalState.HomeTeam) ? "vs" : "at";
        return $"{resultLabel}: {selectedTeam.Name} {selectedTeam.Runs}-{opponent.Runs} {locationLabel} {opponent.Name}";
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
        _saveState.LastCompletedLiveMatch = null;
        _saveState.LastOffseasonSummary = null;
        _saveState.ActiveDraft = null;
        _saveState.CurrentSeasonYear = 0;
        _saveState.CurrentFranchiseDate = DateTime.Today;
        _saveState.CompletedScheduleGameKeys.Clear();
        _saveState.CompletedScheduleGameResults.Clear();
        _saveState.CompletedGameBoxScores.Clear();
        _saveState.CurrentViewedBoxScoreGameKey = null;
        var releasedPlayerIds = GetAllPlayers()
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .Select(player => player.PlayerId)
            .ToList();

        foreach (var playerId in releasedPlayerIds)
        {
            _saveState.PlayerAssignments.Remove(playerId);
            _saveState.PlayerRosterAssignments.Remove(playerId);
            _saveState.FortyManRosterPlayerIds.Remove(playerId);
        }

        _saveState.CreatedPlayers.Clear();

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
        _saveState.LastCompletedLiveMatch = null;
        _saveState.LastOffseasonSummary = null;
        _saveState.ActiveDraft = null;
        _saveState.CurrentSeasonYear = 0;
        _saveState.PlayerRatings.Clear();
        _saveState.PlayerSeasonStats.Clear();
        _saveState.PreviousSeasonStats.Clear();
        _saveState.PlayerRecentGameStats.Clear();
        _saveState.PlayerRecentTrackingTotals.Clear();
        _saveState.PlayerHealth.Clear();
        _saveState.PlayerAges.Clear();
        _saveState.PlayerAssignments.Clear();
        _saveState.PlayerRosterAssignments.Clear();
        _saveState.FortyManRosterPlayerIds.Clear();
        _saveState.CreatedPlayers.Clear();
        _saveState.CompletedScheduleGameKeys.Clear();
        _saveState.CompletedScheduleGameResults.Clear();
        _saveState.CompletedGameBoxScores.Clear();
        _saveState.CurrentViewedBoxScoreGameKey = null;
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
        teamState.ManualMinorLeagueAssignmentLocks ??= new List<Guid>();
        teamState.TrainingReports ??= new List<TrainingReportState>();
        teamState.Economy ??= BuildDefaultTeamEconomy(teamName);
        EnsureTeamEconomyInitialized(teamState, teamName);
        CleanupMinorLeagueAssignmentLocks(teamState, teamName);
        return teamState;
    }

    private IReadOnlyList<FranchiseRosterEntry> GetTeamRoster(string teamName, LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        var teamState = GetOrCreateTeamState(teamName);
        InitializeLineupSlots(teamState, teamName);
        InitializeRotationSlots(teamState, teamName);
        var rostersByPlayerId = _leagueData.Rosters
            .GroupBy(roster => roster.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());
        var lineupSlots = GetLineupSlotsForPreset(teamState, teamName, lineupPresetType);
        var rotationSlots = teamState.RotationSlots;
        var designatedHitterPlayerId = GetDesignatedHitterPlayerIdForPreset(teamState, lineupPresetType);
        var lineupMap = BuildSlotMap(lineupSlots);
        var rotationMap = BuildSlotMap(rotationSlots);

        return GetAllPlayers()
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .Where(player => IsPlayerOnActiveTeamRoster(player.PlayerId))
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
                    rotationMap.TryGetValue(player.PlayerId, out var rotationSlot) ? rotationSlot : null,
                    BattingProfileFactory.ParseThrows(player.Throws),
                        designatedHitterPlayerId == player.PlayerId);
            })
            .OrderBy(entry => entry.PlayerName)
            .ToList();
    }

    private bool IsPlayerOnMajorLeagueRoster(Guid playerId)
    {
        return string.Equals(GetPlayerRosterAssignment(playerId), MajorLeagueRosterAssignment, StringComparison.OrdinalIgnoreCase)
            && IsPlayerOnFortyMan(playerId);
    }

    private bool IsPlayerOnActiveTeamRoster(Guid playerId)
    {
        return string.Equals(GetPlayerRosterAssignment(playerId), MajorLeagueRosterAssignment, StringComparison.OrdinalIgnoreCase)
            && IsPlayerOnFortyMan(playerId);
    }

    private List<Guid?> BuildLineupSlots(string teamName, LineupPresetType lineupPresetType = LineupPresetType.VsRightHandedPitcher)
    {
        var teamState = GetOrCreateTeamState(teamName);
        InitializeLineupSlots(teamState, teamName);
        return GetLineupSlotsForPreset(teamState, teamName, lineupPresetType);
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
        return GetAllPlayers().FirstOrDefault(player => player.PlayerId == playerId);
    }

    private IReadOnlyList<PlayerImportDto> GetAllPlayers()
    {
        if (_saveState.CreatedPlayers.Count == 0)
        {
            return _leagueData.Players;
        }

        return _leagueData.Players
            .Concat(_saveState.CreatedPlayers.Values.Select(player => new PlayerImportDto
            {
                PlayerId = player.PlayerId,
                FullName = player.FullName,
                PrimaryPosition = player.PrimaryPosition,
                SecondaryPosition = player.SecondaryPosition,
                TeamName = player.TeamName,
                Age = player.Age,
                Throws = player.Throws,
                BattingStyle = player.BattingStyle
            }))
            .ToList();
    }

    private bool HasRequiredDraftWorkRemaining()
    {
        return _saveState.ActiveDraft != null
            || !HasCompletedDraftForCurrentSeason()
            || HasPendingDraftRosterDecisions();
    }

    private bool HasCompletedDraftForCurrentSeason()
    {
        var seasonYear = GetCurrentSeasonYear();
        return _saveState.LastCompletedDraftSeasonYear == seasonYear && HasRecordedDraftResultsForSeason(seasonYear);
    }

    private bool HasRecordedDraftResultsForSeason(int seasonYear)
    {
        return _saveState.CreatedPlayers.Values.Any(player => player.DraftSeasonYear == seasonYear);
    }

    private bool IsSeasonAdvanceBlockedByPendingDraftWork()
    {
        if (SelectedTeam == null || !HasRequiredDraftWorkRemaining())
        {
            return false;
        }

        var currentDate = GetCurrentFranchiseDate().Date;
        if (currentDate < GetSeasonCalendarEndDate().Date)
        {
            return false;
        }

        return GetRemainingScheduledGamesForDate(currentDate).Count == 0;
    }

    private bool ShouldHoldAtSeasonBoundaryForDraft(DateTime currentDate)
    {
        if (!HasRequiredDraftWorkRemaining())
        {
            return false;
        }

        return currentDate.Date >= GetSeasonCalendarEndDate().Date
            && GetRemainingScheduledGamesForDate(currentDate.Date).Count == 0;
    }

    private bool HasPendingDraftRosterDecisions()
    {
        if (SelectedTeam == null)
        {
            return false;
        }

        return _saveState.CreatedPlayers.Values.Any(player =>
            string.Equals(player.TeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase)
            && player.RequiresRosterDecision);
    }

    private bool TryGetSelectedTeamCreatedPlayer(Guid playerId, out FranchiseCreatedPlayerState createdPlayer, out string statusMessage)
    {
        createdPlayer = new FranchiseCreatedPlayerState();
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before managing drafted players.";
            return false;
        }

        if (!_saveState.CreatedPlayers.TryGetValue(playerId, out var resolvedPlayer)
            || !string.Equals(resolvedPlayer.TeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = "That player is not part of your drafted-player pool.";
            return false;
        }

        createdPlayer = resolvedPlayer;
        statusMessage = string.Empty;
        return true;
    }

    private string GetPlayerRosterAssignment(Guid playerId)
    {
        if (_saveState.PlayerRosterAssignments.TryGetValue(playerId, out var rosterAssignment) && !string.IsNullOrWhiteSpace(rosterAssignment))
        {
            return NormalizeRosterAssignment(rosterAssignment);
        }

        if (_saveState.CreatedPlayers.TryGetValue(playerId, out var createdPlayer) && !string.IsNullOrWhiteSpace(createdPlayer.RosterAssignment))
        {
            return NormalizeRosterAssignment(createdPlayer.RosterAssignment);
        }

        return ReserveRosterAssignment;
    }

    private void SetPlayerRosterAssignment(Guid playerId, string rosterAssignment)
    {
        rosterAssignment = NormalizeRosterAssignment(rosterAssignment);
        _saveState.PlayerRosterAssignments[playerId] = rosterAssignment;
        if (_saveState.CreatedPlayers.TryGetValue(playerId, out var createdPlayer))
        {
            createdPlayer.RosterAssignment = rosterAssignment;
        }
    }

    private bool IsPlayerOnFortyMan(Guid playerId)
    {
        return _saveState.FortyManRosterPlayerIds.Contains(playerId);
    }

    private void EnsureImportedPlayerMappingsAreSynchronized()
    {
        var hasChanges = false;
        hasChanges |= EnsurePlayerAssignmentsGenerated();
        hasChanges |= EnsurePlayerRosterAssignmentsGenerated();
        hasChanges |= EnsureFortyManRosterGenerated();

        if (hasChanges)
        {
            Save();
        }
    }

    private void AddPlayerToFortyManRoster(Guid playerId)
    {
        _saveState.FortyManRosterPlayerIds.Add(playerId);
    }

    private void RemovePlayerFromFortyManRoster(Guid playerId)
    {
        _saveState.FortyManRosterPlayerIds.Remove(playerId);
    }

    private bool TryGetSelectedTeamPlayer(Guid playerId, out PlayerImportDto player, out string statusMessage)
    {
        player = new PlayerImportDto();
        if (SelectedTeam == null)
        {
            statusMessage = "Select a team before managing roster assignments.";
            return false;
        }

        var resolvedPlayer = FindPlayerImport(playerId);
        if (resolvedPlayer == null || !string.Equals(GetAssignedTeamName(playerId, resolvedPlayer.TeamName), SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = "That player is not currently on your roster.";
            return false;
        }

        player = resolvedPlayer;
        statusMessage = string.Empty;
        return true;
    }

    private void RefreshRosterSlots(string teamName)
    {
        var teamState = GetOrCreateTeamState(teamName);
        InitializeLineupSlots(teamState, teamName);
        InitializeRotationSlots(teamState, teamName);
    }

    private string GetRosterAssignmentLabel(FranchiseCreatedPlayerState createdPlayer)
    {
        return GetRosterAssignmentLabel(createdPlayer.TeamName, createdPlayer.RosterAssignment);
    }

    private string GetRosterAssignmentLabel(string teamName, string rosterAssignment)
    {
        rosterAssignment = NormalizeRosterAssignment(rosterAssignment);
        if (IsMinorLeagueRosterAssignment(rosterAssignment))
        {
            return $"{rosterAssignment} Affiliate";
        }

        if (string.Equals(rosterAssignment, MajorLeagueRosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return "First Team";
        }

        if (string.Equals(rosterAssignment, ReserveRosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return "Organization Roster";
        }

        if (string.Equals(rosterAssignment, PendingRosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return "Decision Needed";
        }

        return MajorLeagueRosterAssignment;
    }

    private bool EnsureFortyManRosterGenerated()
    {
        var hasChanges = false;
        var validPlayerIds = GetAllPlayers().Select(player => player.PlayerId).ToHashSet();
        hasChanges |= _saveState.FortyManRosterPlayerIds.RemoveWhere(playerId => !validPlayerIds.Contains(playerId)) > 0;

        if (_saveState.FortyManRosterPlayerIds.Count == 0)
        {
            var seededRoster = BuildInitialFortyManRosterMembership();
            foreach (var playerId in seededRoster)
            {
                if (_saveState.FortyManRosterPlayerIds.Add(playerId))
                {
                    hasChanges = true;
                }
            }
        }

        return hasChanges;
    }

    private DraftState? LoadDraftState()
    {
        if (_saveState.ActiveDraft == null)
        {
            return null;
        }

        var draftState = DraftState.Restore(
            _saveState.ActiveDraft.DraftOrder,
            _saveState.ActiveDraft.AvailableProspects.Select(prospect => new DraftProspect(
                prospect.PlayerId,
                prospect.PlayerName,
                prospect.PrimaryPosition,
                prospect.SecondaryPosition,
                prospect.Age,
                prospect.OverallRating,
                prospect.PotentialRating,
                prospect.Source,
                prospect.Summary,
                prospect.ScoutSummary,
                prospect.PotentialSummary,
                prospect.SourceTeamName,
                prospect.SourceStatsSummary,
                prospect.TalentOutcome)).ToList(),
            _saveState.ActiveDraft.DraftedPicks.Select(pick => new DraftPick(
                pick.RoundNumber,
                pick.PickNumberInRound,
                pick.OverallPickNumber,
                pick.TeamName,
                pick.PlayerId,
                pick.PlayerName,
                pick.PrimaryPosition,
                pick.OverallRating,
                pick.IsUserPick)).ToList(),
            _saveState.ActiveDraft.TotalRounds,
            _saveState.ActiveDraft.IsSnakeDraft,
            _saveState.ActiveDraft.CurrentRound,
            _saveState.ActiveDraft.CurrentPickNumber);

        return _getDraftStateUseCase.Execute(draftState);
    }

    private void SaveDraftState(DraftState? draftState)
    {
        if (draftState == null)
        {
            _saveState.ActiveDraft = null;
            return;
        }

        _saveState.ActiveDraft = new DraftSessionState
        {
            TotalRounds = draftState.TotalRounds,
            IsSnakeDraft = draftState.IsSnakeDraft,
            CurrentRound = draftState.CurrentRound,
            CurrentPickNumber = draftState.CurrentPickNumber,
            DraftOrder = draftState.DraftOrder.ToList(),
            AvailableProspects = draftState.AvailableProspects.Select(prospect => new DraftProspectState
            {
                PlayerId = prospect.PlayerId,
                PlayerName = prospect.PlayerName,
                PrimaryPosition = prospect.PrimaryPosition,
                SecondaryPosition = prospect.SecondaryPosition,
                Age = prospect.Age,
                OverallRating = prospect.OverallRating,
                PotentialRating = prospect.PotentialRating,
                Source = prospect.Source,
                Summary = prospect.Summary,
                ScoutSummary = prospect.ScoutSummary,
                PotentialSummary = prospect.PotentialSummary,
                SourceTeamName = prospect.SourceTeamName,
                SourceStatsSummary = prospect.SourceStatsSummary,
                TalentOutcome = prospect.TalentOutcome
            }).ToList(),
            DraftedPicks = draftState.DraftedPicks.Select(pick => new DraftPickState
            {
                RoundNumber = pick.RoundNumber,
                PickNumberInRound = pick.PickNumberInRound,
                OverallPickNumber = pick.OverallPickNumber,
                TeamName = pick.TeamName,
                PlayerId = pick.PlayerId,
                PlayerName = pick.PlayerName,
                PrimaryPosition = pick.PrimaryPosition,
                OverallRating = pick.OverallRating,
                IsUserPick = pick.IsUserPick
            }).ToList()
        };
    }

    private IReadOnlyList<string> BuildDraftOrder()
    {
        return GetStandings()
            .OrderBy(team => team.GamesPlayed == 0 ? 0.5d : team.Wins / (double)team.GamesPlayed)
            .ThenBy(team => team.Wins)
            .ThenByDescending(team => team.Losses)
            .ThenBy(team => team.TeamName)
            .Select(team => team.TeamName)
            .ToList();
    }

    private DraftCpuTeamContext BuildDraftCpuTeamContext(string teamName)
    {
        return new DraftCpuTeamContext(
            teamName,
            GetAllPlayers()
                .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
                .Select(player => player.PrimaryPosition)
                .ToList());
    }

    private void EnsureDraftCreatedPlayer(DraftProspect prospect)
    {
        if (!_saveState.CreatedPlayers.ContainsKey(prospect.PlayerId))
        {
            _saveState.CreatedPlayers[prospect.PlayerId] = new FranchiseCreatedPlayerState
            {
                PlayerId = prospect.PlayerId,
                FullName = prospect.PlayerName,
                PrimaryPosition = prospect.PrimaryPosition,
                SecondaryPosition = prospect.SecondaryPosition,
                Age = prospect.Age,
                Throws = "R",
                BattingStyle = "R",
                TeamName = string.Empty,
                Source = prospect.Source,
                DraftOverallRating = prospect.OverallRating,
                PotentialRating = prospect.PotentialRating,
                Summary = prospect.Summary,
                ScoutSummary = prospect.ScoutSummary,
                PotentialSummary = prospect.PotentialSummary,
                SourceTeamName = prospect.SourceTeamName,
                SourceStatsSummary = prospect.SourceStatsSummary,
                TalentOutcome = prospect.TalentOutcome,
                DraftSeasonYear = GetCurrentSeasonYear(),
                RosterAssignment = PendingRosterAssignment,
                RequiresRosterDecision = false,
                MinorLeagueOptionsRemaining = 3,
                LastOptionedSeasonYear = 0
            };
        }

        if (!_saveState.PlayerRatings.ContainsKey(prospect.PlayerId))
        {
            _saveState.PlayerRatings[prospect.PlayerId] = CreateDraftPlayerRatings(prospect);
        }

        _saveState.PlayerHealth.TryAdd(prospect.PlayerId, new PlayerHealthState());
        _saveState.PlayerSeasonStats.TryAdd(prospect.PlayerId, new PlayerSeasonStatsState());
        _saveState.PlayerRecentTrackingTotals.TryAdd(prospect.PlayerId, new PlayerRecentTotalsState());
        _saveState.PlayerAges[prospect.PlayerId] = prospect.Age;
    }

    private PlayerHiddenRatingsState CreateDraftPlayerRatings(DraftProspect prospect)
    {
        var ratings = PlayerRatingsGenerator.Generate(prospect.PlayerId, prospect.PlayerName, prospect.PrimaryPosition, prospect.SecondaryPosition, prospect.Age);
        var delta = prospect.OverallRating - ratings.OverallRating;
        if (delta == 0)
        {
            return ratings;
        }

        ratings.ContactRating = Math.Clamp(ratings.ContactRating + delta, 20, 99);
        ratings.PowerRating = Math.Clamp(ratings.PowerRating + delta, 20, 99);
        ratings.DisciplineRating = Math.Clamp(ratings.DisciplineRating + delta, 20, 99);
        ratings.SpeedRating = Math.Clamp(ratings.SpeedRating + delta, 20, 99);
        ratings.FieldingRating = Math.Clamp(ratings.FieldingRating + delta, 20, 99);
        ratings.ArmRating = Math.Clamp(ratings.ArmRating + delta, 20, 99);
        ratings.PitchingRating = Math.Clamp(ratings.PitchingRating + delta, 20, 99);
        ratings.StaminaRating = Math.Clamp(ratings.StaminaRating + delta, 20, 99);
        ratings.DurabilityRating = Math.Clamp(ratings.DurabilityRating + delta, 20, 99);
        ratings.RecalculateDerivedRatings();
        return ratings;
    }

    private void ApplyDraftPick(DraftPick pick)
    {
        var draftedPlayer = FindPlayerImport(pick.PlayerId);
        if (draftedPlayer == null)
        {
            throw new InvalidOperationException("The drafted prospect could not be resolved.");
        }

        if (_saveState.CreatedPlayers.TryGetValue(pick.PlayerId, out var createdPlayer))
        {
            createdPlayer.TeamName = pick.TeamName;
            if (SelectedTeam != null && string.Equals(pick.TeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
            {
                createdPlayer.RosterAssignment = PendingRosterAssignment;
                createdPlayer.RequiresRosterDecision = true;
            }
            else
            {
                createdPlayer.RosterAssignment = pick.OverallRating >= 68 ? MajorLeagueRosterAssignment : SingleARosterAssignment;
                createdPlayer.RequiresRosterDecision = false;
                if (pick.OverallRating >= 68)
                {
                    AddPlayerToFortyManRoster(pick.PlayerId);
                }
            }

            _saveState.PlayerRosterAssignments[pick.PlayerId] = createdPlayer.RosterAssignment;
        }

        _saveState.PlayerAssignments[pick.PlayerId] = pick.TeamName;
        _saveState.PlayerAges[pick.PlayerId] = draftedPlayer.Age;
        _saveState.PlayerHealth.TryAdd(pick.PlayerId, new PlayerHealthState());
        _saveState.PlayerSeasonStats.TryAdd(pick.PlayerId, new PlayerSeasonStatsState());

        var teamState = GetOrCreateTeamState(pick.TeamName);
        var rookieSalary = 650_000m + Math.Max(0, pick.OverallRating - 45) * 55_000m + Math.Max(0, 20 - pick.OverallPickNumber) * 20_000m;
        var rookieYears = pick.RoundNumber == 1 ? 4 : 3;
        _signPlayerContractUseCase.Execute(teamState.Economy, pick.PlayerId, pick.PlayerName, rookieSalary, rookieYears, GetCurrentFranchiseDate());
        AddTransferRecord(pick.TeamName, $"Drafted {pick.PlayerName} ({pick.PrimaryPosition}) in Round {pick.RoundNumber}, Pick {pick.PickNumberInRound}.");
    }

    private bool FinalizeDraftProgress(DraftState draftState, string baseMessage, out string statusMessage)
    {
        if (!draftState.IsComplete)
        {
            SaveDraftState(draftState);
            Save();
            statusMessage = baseMessage;
            return true;
        }

        _finishDraftUseCase.Execute(draftState);
        CleanupUndraftedProspects(draftState);
        _saveState.LastCompletedDraftSeasonYear = GetCurrentSeasonYear();
        SaveDraftState(null);
        Save();
        statusMessage = $"{baseMessage} Draft complete after {draftState.DraftedPicks.Count} pick(s).";
        return true;
    }

    private void CleanupUndraftedProspects(DraftState draftState)
    {
        foreach (var undrafted in draftState.AvailableProspects)
        {
            _saveState.CreatedPlayers.Remove(undrafted.PlayerId);
            _saveState.PlayerRatings.Remove(undrafted.PlayerId);
            _saveState.PlayerSeasonStats.Remove(undrafted.PlayerId);
            _saveState.PreviousSeasonStats.Remove(undrafted.PlayerId);
            _saveState.PlayerRecentGameStats.Remove(undrafted.PlayerId);
            _saveState.PlayerRecentTrackingTotals.Remove(undrafted.PlayerId);
            _saveState.PlayerHealth.Remove(undrafted.PlayerId);
            _saveState.PlayerAges.Remove(undrafted.PlayerId);
            _saveState.PlayerAssignments.Remove(undrafted.PlayerId);
            _saveState.PlayerRosterAssignments.Remove(undrafted.PlayerId);
            _saveState.FortyManRosterPlayerIds.Remove(undrafted.PlayerId);
        }
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
        var validPlayerIds = GetAllPlayers().Select(player => player.PlayerId).ToHashSet();

        foreach (var stalePlayerId in _saveState.PlayerAssignments.Keys.Where(playerId => !validPlayerIds.Contains(playerId)).ToList())
        {
            _saveState.PlayerAssignments.Remove(stalePlayerId);
            hasChanges = true;
        }

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

    private bool EnsurePlayerRosterAssignmentsGenerated()
    {
        var hasChanges = false;
        var generatedAssignments = BuildInitialRosterAssignments();
        var validPlayerIds = GetAllPlayers().Select(player => player.PlayerId).ToHashSet();

        foreach (var stalePlayerId in _saveState.PlayerRosterAssignments.Keys.Where(playerId => !validPlayerIds.Contains(playerId)).ToList())
        {
            _saveState.PlayerRosterAssignments.Remove(stalePlayerId);
            hasChanges = true;
        }

        foreach (var player in _leagueData.Players)
        {
            if (_saveState.PlayerRosterAssignments.TryGetValue(player.PlayerId, out var rosterAssignment) && !string.IsNullOrWhiteSpace(rosterAssignment))
            {
                var normalizedAssignment = NormalizeRosterAssignment(rosterAssignment);
                if (!string.Equals(rosterAssignment, normalizedAssignment, StringComparison.OrdinalIgnoreCase))
                {
                    _saveState.PlayerRosterAssignments[player.PlayerId] = normalizedAssignment;
                    hasChanges = true;
                }

                continue;
            }

            _saveState.PlayerRosterAssignments[player.PlayerId] = generatedAssignments.GetValueOrDefault(player.PlayerId, ReserveRosterAssignment);
            hasChanges = true;
        }

        foreach (var player in _saveState.CreatedPlayers.Values)
        {
            var resolvedAssignment = string.IsNullOrWhiteSpace(player.RosterAssignment)
                ? (player.RequiresRosterDecision ? PendingRosterAssignment : ReserveRosterAssignment)
                : NormalizeRosterAssignment(player.RosterAssignment);

            if (!_saveState.PlayerRosterAssignments.TryGetValue(player.PlayerId, out var rosterAssignment) || string.IsNullOrWhiteSpace(rosterAssignment))
            {
                _saveState.PlayerRosterAssignments[player.PlayerId] = resolvedAssignment;
                hasChanges = true;
            }
            else
            {
                var normalizedAssignment = NormalizeRosterAssignment(rosterAssignment);
                if (!string.Equals(rosterAssignment, normalizedAssignment, StringComparison.OrdinalIgnoreCase))
                {
                    _saveState.PlayerRosterAssignments[player.PlayerId] = normalizedAssignment;
                    rosterAssignment = normalizedAssignment;
                    hasChanges = true;
                }

                if (!string.Equals(player.RosterAssignment, rosterAssignment, StringComparison.OrdinalIgnoreCase))
                {
                    player.RosterAssignment = rosterAssignment;
                    hasChanges = true;
                }
            }
        }

        return hasChanges;
    }

    private Dictionary<Guid, string> BuildInitialRosterAssignments()
    {
        var assignments = new Dictionary<Guid, string>();
        var playersByTeam = _leagueData.Players
            .Where(player => !string.IsNullOrWhiteSpace(GetImportedPlayerTeamName(player)))
            .GroupBy(GetImportedPlayerTeamName, StringComparer.OrdinalIgnoreCase);

        foreach (var teamGroup in playersByTeam)
        {
            var teamPlayers = teamGroup.ToList();
            if (teamPlayers.Count == 0)
            {
                continue;
            }

            var firstTeam = SelectInitialFirstTeamPlayers(teamPlayers);

            foreach (var player in firstTeam)
            {
                assignments[player.PlayerId] = MajorLeagueRosterAssignment;
            }

            var remainingPlayers = teamPlayers
                .Where(player => !assignments.ContainsKey(player.PlayerId))
                .ToList();

            AssignAffiliateTier(assignments, remainingPlayers, MinorLeagueAffiliateLevel.TripleA, targetAge: 25, veteranSlots: 5);
            AssignAffiliateTier(assignments, remainingPlayers, MinorLeagueAffiliateLevel.DoubleA, targetAge: 23, veteranSlots: 3);
            AssignAffiliateTier(assignments, remainingPlayers, MinorLeagueAffiliateLevel.SingleA, targetAge: 21, veteranSlots: 2);

            foreach (var player in remainingPlayers)
            {
                assignments[player.PlayerId] = ReserveRosterAssignment;
            }
        }

        return assignments;
    }

    private HashSet<Guid> BuildInitialFortyManRosterMembership()
    {
        var fortyManRoster = new HashSet<Guid>();
        var playersByTeam = _leagueData.Players
            .Where(player => !string.IsNullOrWhiteSpace(GetImportedPlayerTeamName(player)))
            .GroupBy(GetImportedPlayerTeamName, StringComparer.OrdinalIgnoreCase);

        foreach (var teamGroup in playersByTeam)
        {
            var teamPlayers = teamGroup.ToList();
            if (teamPlayers.Count == 0)
            {
                continue;
            }

            var firstTeam = SelectInitialFirstTeamPlayers(teamPlayers);

            foreach (var player in firstTeam)
            {
                fortyManRoster.Add(player.PlayerId);
            }

            var extraCandidates = teamPlayers
                .Where(player => !fortyManRoster.Contains(player.PlayerId))
                .OrderBy(player => GetAffiliateFortyManPriority(player))
                .ThenByDescending(GetMajorLeagueReadinessScore)
                .ThenBy(player => player.FullName, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, MaxRosterSize - firstTeam.Count))
                .ToList();

            foreach (var player in extraCandidates)
            {
                fortyManRoster.Add(player.PlayerId);
            }
        }

        return fortyManRoster;
    }

    private List<PlayerImportDto> SelectInitialFirstTeamPlayers(IReadOnlyList<PlayerImportDto> teamPlayers)
    {
        var orderedPlayers = teamPlayers
            .OrderByDescending(GetMajorLeagueReadinessScore)
            .ThenByDescending(player => player.Age)
            .ThenBy(player => player.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedPlayers = new List<PlayerImportDto>();
        var selectedPlayerIds = new HashSet<Guid>();

        AddInitialFirstTeamPlayers(selectedPlayers, selectedPlayerIds, orderedPlayers.Where(player => IsPitcherPosition(player.PrimaryPosition)), 13);
        AddInitialFirstTeamPlayers(selectedPlayers, selectedPlayerIds, orderedPlayers.Where(IsImportedCatcher), 2);
        AddInitialFirstTeamPlayers(selectedPlayers, selectedPlayerIds, orderedPlayers.Where(IsImportedInfielder), 6);
        AddInitialFirstTeamPlayers(selectedPlayers, selectedPlayerIds, orderedPlayers.Where(IsImportedOutfielder), 5);
        AddInitialFirstTeamPlayers(selectedPlayers, selectedPlayerIds, orderedPlayers, FirstTeamRosterSize - selectedPlayers.Count);

        return selectedPlayers;
    }

    private static void AddInitialFirstTeamPlayers(
        List<PlayerImportDto> selectedPlayers,
        HashSet<Guid> selectedPlayerIds,
        IEnumerable<PlayerImportDto> candidates,
        int playerCount)
    {
        if (playerCount <= 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            if (selectedPlayers.Count >= FirstTeamRosterSize || playerCount <= 0 || !selectedPlayerIds.Add(candidate.PlayerId))
            {
                continue;
            }

            selectedPlayers.Add(candidate);
            playerCount--;
        }
    }

    private static bool IsImportedCatcher(PlayerImportDto player)
    {
        return HasPosition(player, "C");
    }

    private static bool IsImportedInfielder(PlayerImportDto player)
    {
        return HasAnyPosition(player, ["1B", "2B", "3B", "SS", "IF"]);
    }

    private static bool IsImportedOutfielder(PlayerImportDto player)
    {
        return HasAnyPosition(player, ["LF", "CF", "RF", "OF"]);
    }

    private static bool HasAnyPosition(PlayerImportDto player, IReadOnlyList<string> positions)
    {
        return positions.Any(position => HasPosition(player, position));
    }

    private static bool HasPosition(PlayerImportDto player, string position)
    {
        return string.Equals(player.PrimaryPosition, position, StringComparison.OrdinalIgnoreCase)
            || string.Equals(player.SecondaryPosition, position, StringComparison.OrdinalIgnoreCase);
    }

    private void AssignAffiliateTier(
        Dictionary<Guid, string> assignments,
        List<PlayerImportDto> remainingPlayers,
        MinorLeagueAffiliateLevel affiliateLevel,
        int targetAge,
        int veteranSlots)
    {
        if (remainingPlayers.Count == 0)
        {
            return;
        }

        var targetCount = Math.Min(AffiliateRosterSize, remainingPlayers.Count);
        var veteranCount = Math.Min(veteranSlots, Math.Max(0, targetCount / 4));
        var selectedPlayers = new List<PlayerImportDto>(targetCount);

        if (veteranCount > 0)
        {
            var veterans = remainingPlayers
                .OrderByDescending(player => player.Age)
                .ThenByDescending(GetAffiliateDevelopmentScore)
                .ThenBy(player => player.FullName, StringComparer.OrdinalIgnoreCase)
                .Take(veteranCount)
                .ToList();

            foreach (var veteran in veterans)
            {
                remainingPlayers.Remove(veteran);
                selectedPlayers.Add(veteran);
            }
        }

        var prospectCount = Math.Min(targetCount - selectedPlayers.Count, remainingPlayers.Count);
        var prospects = remainingPlayers
            .OrderBy(player => Math.Abs(player.Age - targetAge))
            .ThenByDescending(GetAffiliateDevelopmentScore)
            .ThenBy(player => player.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(prospectCount)
            .ToList();

        foreach (var prospect in prospects)
        {
            remainingPlayers.Remove(prospect);
            selectedPlayers.Add(prospect);
        }

        var assignment = GetRosterAssignmentForAffiliateLevel(affiliateLevel);
        foreach (var player in selectedPlayers)
        {
            assignments[player.PlayerId] = assignment;
        }
    }

    private string GetImportedPlayerTeamName(PlayerImportDto player)
    {
        if (_saveState.PlayerAssignments.TryGetValue(player.PlayerId, out var teamName) && !string.IsNullOrWhiteSpace(teamName))
        {
            return teamName;
        }

        if (!string.IsNullOrWhiteSpace(player.TeamName))
        {
            return player.TeamName;
        }

        return _leagueData.Rosters.FirstOrDefault(roster => roster.PlayerId == player.PlayerId)?.TeamName ?? string.Empty;
    }

    private int GetMajorLeagueReadinessScore(PlayerImportDto player)
    {
        var ratings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var ageAdjustment = Math.Clamp(player.Age - 24, 0, 8);
        var pitcherAdjustment = player.PrimaryPosition == "SP" ? 2 : 0;
        return ratings.OverallRating * 10 + ageAdjustment + pitcherAdjustment;
    }

    private int GetAffiliateDevelopmentScore(PlayerImportDto player)
    {
        var ratings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var youthBonus = Math.Clamp(27 - player.Age, -6, 8);
        return ratings.OverallRating * 10 + youthBonus;
    }

    private int GetAffiliateFortyManPriority(PlayerImportDto player)
    {
        var assignment = _saveState.PlayerRosterAssignments.GetValueOrDefault(player.PlayerId, ReserveRosterAssignment);
        var affiliateLevel = GetAffiliateLevel(assignment);
        return affiliateLevel switch
        {
            MinorLeagueAffiliateLevel.TripleA => 0,
            MinorLeagueAffiliateLevel.DoubleA => 1,
            _ => 2
        };
    }

    private bool EnsureCreatedPlayerRosterAssignmentsGenerated()
    {
        var hasChanges = false;

        foreach (var player in _saveState.CreatedPlayers.Values)
        {
            var normalizedAssignment = NormalizeRosterAssignment(player.RosterAssignment);
            if (!string.Equals(player.RosterAssignment, normalizedAssignment, StringComparison.OrdinalIgnoreCase))
            {
                player.RosterAssignment = normalizedAssignment;
                _saveState.PlayerRosterAssignments[player.PlayerId] = normalizedAssignment;
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(player.TeamName))
            {
                continue;
            }

            if (!string.Equals(player.RosterAssignment, PendingRosterAssignment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (player.RequiresRosterDecision)
            {
                continue;
            }

            player.RosterAssignment = ReserveRosterAssignment;
            _saveState.PlayerRosterAssignments[player.PlayerId] = ReserveRosterAssignment;
            hasChanges = true;
        }

        return hasChanges;
    }

    private static string NormalizeRosterAssignment(string rosterAssignment)
    {
        if (string.Equals(rosterAssignment, LegacyAffiliateRosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return TripleARosterAssignment;
        }

        if (string.Equals(rosterAssignment, TripleARosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return TripleARosterAssignment;
        }

        if (string.Equals(rosterAssignment, DoubleARosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return DoubleARosterAssignment;
        }

        if (string.Equals(rosterAssignment, SingleARosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return SingleARosterAssignment;
        }

        return rosterAssignment;
    }

    private static bool IsMinorLeagueRosterAssignment(string rosterAssignment)
    {
        rosterAssignment = NormalizeRosterAssignment(rosterAssignment);
        return string.Equals(rosterAssignment, TripleARosterAssignment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rosterAssignment, DoubleARosterAssignment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rosterAssignment, SingleARosterAssignment, StringComparison.OrdinalIgnoreCase);
    }

    private static MinorLeagueAffiliateLevel? GetAffiliateLevel(string rosterAssignment)
    {
        rosterAssignment = NormalizeRosterAssignment(rosterAssignment);
        if (string.Equals(rosterAssignment, TripleARosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return MinorLeagueAffiliateLevel.TripleA;
        }

        if (string.Equals(rosterAssignment, DoubleARosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return MinorLeagueAffiliateLevel.DoubleA;
        }

        if (string.Equals(rosterAssignment, SingleARosterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return MinorLeagueAffiliateLevel.SingleA;
        }

        return null;
    }

    private static string GetRosterAssignmentForAffiliateLevel(MinorLeagueAffiliateLevel affiliateLevel)
    {
        return affiliateLevel switch
        {
            MinorLeagueAffiliateLevel.DoubleA => DoubleARosterAssignment,
            MinorLeagueAffiliateLevel.SingleA => SingleARosterAssignment,
            _ => TripleARosterAssignment
        };
    }

    private static string GetAffiliateLevelLabel(MinorLeagueAffiliateLevel affiliateLevel)
    {
        return affiliateLevel switch
        {
            MinorLeagueAffiliateLevel.DoubleA => DoubleARosterAssignment,
            MinorLeagueAffiliateLevel.SingleA => SingleARosterAssignment,
            _ => TripleARosterAssignment
        };
    }

    private static string BuildAffiliateSummary(int affiliateCount, IEnumerable<MinorLeagueAffiliateLevel?> affiliateLevels)
    {
        if (affiliateCount <= 0)
        {
            return "0 on affiliates";
        }

        return $"{affiliateCount} on affiliates ({BuildAffiliateBreakdownLabel(affiliateLevels)})";
    }

    private static string BuildAffiliateBreakdownLabel(IEnumerable<OrganizationRosterPlayerView> players)
    {
        return BuildAffiliateBreakdownLabel(players.Select(player => player.AffiliateLevel));
    }

    private static string BuildAffiliateBreakdownLabel(IEnumerable<MinorLeagueAffiliateLevel?> affiliateLevels)
    {
        var levels = affiliateLevels.Where(level => level.HasValue).Select(level => level!.Value).ToList();
        var tripleACount = levels.Count(level => level == MinorLeagueAffiliateLevel.TripleA);
        var doubleACount = levels.Count(level => level == MinorLeagueAffiliateLevel.DoubleA);
        var singleACount = levels.Count(level => level == MinorLeagueAffiliateLevel.SingleA);
        return $"AAA {tripleACount} | AA {doubleACount} | A {singleACount}";
    }

    private void ApplySelectedTeamAutomaticMinorLeaguePromotions()
    {
        if (SelectedTeam == null)
        {
            return;
        }

        if (ApplyAutomaticMinorLeaguePromotions(SelectedTeam.Name))
        {
            RefreshRosterSlots(SelectedTeam.Name);
        }
    }

    private bool ApplyAutomaticMinorLeaguePromotions(string teamName)
    {
        var teamState = GetOrCreateTeamState(teamName);
        if (!teamState.AutoManageMinorLeaguePromotions)
        {
            return false;
        }

        CleanupMinorLeagueAssignmentLocks(teamState, teamName);
        var lockedPlayerIds = teamState.ManualMinorLeagueAssignmentLocks.ToHashSet();
        var changed = false;

        foreach (var player in GetAllPlayers()
                     .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
                     .Where(player => !lockedPlayerIds.Contains(player.PlayerId)))
        {
            var currentAssignment = GetPlayerRosterAssignment(player.PlayerId);
            if (!GetAffiliateLevel(currentAssignment).HasValue)
            {
                continue;
            }

            var targetAssignment = GetRosterAssignmentForAffiliateLevel(GetRecommendedAffiliateLevel(player));
            if (string.Equals(currentAssignment, targetAssignment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SetPlayerRosterAssignment(player.PlayerId, targetAssignment);
            changed = true;
        }

        return changed;
    }

    private MinorLeagueAffiliateLevel GetRecommendedAffiliateLevel(PlayerImportDto player)
    {
        var ratings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var overallRating = ratings.OverallRating;
        if (overallRating >= 68 || (overallRating >= 64 && player.Age <= 24))
        {
            return MinorLeagueAffiliateLevel.TripleA;
        }

        if (overallRating >= 56 || (overallRating >= 52 && player.Age <= 22))
        {
            return MinorLeagueAffiliateLevel.DoubleA;
        }

        return MinorLeagueAffiliateLevel.SingleA;
    }

    private bool IsMinorLeagueAssignmentLocked(string teamName, Guid playerId)
    {
        return GetOrCreateTeamState(teamName).ManualMinorLeagueAssignmentLocks.Contains(playerId);
    }

    private void SetMinorLeagueAssignmentLock(string teamName, Guid playerId, bool isLocked)
    {
        var teamState = GetOrCreateTeamState(teamName);
        if (isLocked)
        {
            if (!teamState.ManualMinorLeagueAssignmentLocks.Contains(playerId))
            {
                teamState.ManualMinorLeagueAssignmentLocks.Add(playerId);
            }

            return;
        }

        teamState.ManualMinorLeagueAssignmentLocks.RemoveAll(id => id == playerId);
    }

    private void CleanupMinorLeagueAssignmentLocks(TeamFranchiseState teamState, string teamName)
    {
        teamState.ManualMinorLeagueAssignmentLocks.RemoveAll(playerId =>
        {
            var player = FindPlayerImport(playerId);
            if (player == null)
            {
                return true;
            }

            if (!string.Equals(GetAssignedTeamName(playerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !GetAffiliateLevel(GetPlayerRosterAssignment(playerId)).HasValue;
        });
    }

    private bool EnsureDraftCompletionStateConsistency()
    {
        var seasonYear = GetCurrentSeasonYear();
        if (_saveState.ActiveDraft != null)
        {
            return false;
        }

        if (_saveState.LastCompletedDraftSeasonYear != seasonYear)
        {
            return false;
        }

        if (HasRecordedDraftResultsForSeason(seasonYear))
        {
            return false;
        }

        _saveState.LastCompletedDraftSeasonYear = seasonYear - 1;
        return true;
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

    private bool EnsureScoutingDepartmentGenerated()
    {
        var hasChanges = false;
        foreach (var team in _leagueData.Teams)
        {
            hasChanges |= EnsureScoutDepartmentInitialized(GetOrCreateTeamState(team.Name), team.Name);
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
        teamState.Economy.CashOnHand = Math.Max(teamState.Economy.CashOnHand, 1_000_000m);
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

    private decimal CalculateTransferFee(PlayerImportDto player, Dictionary<Guid, Contract> contractsByPlayerId)
    {
        var ratings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        contractsByPlayerId.TryGetValue(player.PlayerId, out var contract);
        var salary = contract?.AnnualSalary ?? EstimatePlayerSalary(player);
        var yearsRemaining = contract?.YearsRemaining ?? 2;
        return ComputeTransferFee(ratings.OverallRating, player.Age, salary, yearsRemaining);
    }

    private decimal CalculateAcquisitionCost(PlayerImportDto player, string owningTeamName, Dictionary<Guid, Contract> contractsByPlayerId)
    {
        var baseFee = CalculateTransferFee(player, contractsByPlayerId);
        var retentionMultiplier = GetPositionNeedMultiplier(owningTeamName, player.PrimaryPosition);
        return decimal.Round(baseFee * retentionMultiplier, 0);
    }

    private Dictionary<Guid, Contract> GetContractsByPlayerId()
    {
        return _leagueData.Teams
            .SelectMany(team => GetOrCreateTeamState(team.Name).Economy.PlayerContracts)
            .GroupBy(contract => contract.SubjectId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private bool HasRosterRoom(string teamName, int incomingPlayers, int outgoingPlayers)
    {
        var projectedCount = GetTeamFortyManCount(teamName) + incomingPlayers - outgoingPlayers;
        return projectedCount <= MaxRosterSize;
    }

    private int GetTeamRosterCount(string teamName)
    {
        return GetAllPlayers().Count(player =>
            string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase)
            && IsPlayerOnActiveTeamRoster(player.PlayerId));
    }

    private int GetTeamFortyManCount(string teamName)
    {
        return _leagueData.Players.Count(player =>
            string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase)
            && IsPlayerOnFortyMan(player.PlayerId))
            + _saveState.CreatedPlayers.Values.Count(player =>
                string.Equals(player.TeamName, teamName, StringComparison.OrdinalIgnoreCase)
                && IsPlayerOnFortyMan(player.PlayerId));
    }

    private decimal GetPositionNeedMultiplier(string teamName, string position)
    {
        var normalizedPosition = NormalizeRosterPosition(position);
        var roster = GetTeamRoster(teamName);
        var countAtPosition = roster.Count(entry => string.Equals(NormalizeRosterPosition(entry.PrimaryPosition), normalizedPosition, StringComparison.OrdinalIgnoreCase));
        var targetDepth = GetTargetDepth(normalizedPosition);

        if (countAtPosition <= targetDepth - 2)
        {
            return 1.35m;
        }

        if (countAtPosition <= targetDepth - 1)
        {
            return 1.18m;
        }

        if (countAtPosition == targetDepth)
        {
            return 1.05m;
        }

        if (countAtPosition == targetDepth + 1)
        {
            return 0.94m;
        }

        return 0.82m;
    }

    private static int GetTargetDepth(string normalizedPosition)
    {
        return normalizedPosition switch
        {
            "SP" => 5,
            "RP" => 7,
            "C" => 2,
            "1B" => 2,
            "2B" => 2,
            "3B" => 2,
            "SS" => 2,
            "OF" => 5,
            "DH" => 1,
            _ => 2
        };
    }

    private static string NormalizeRosterPosition(string position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return "OF";
        }

        return position.ToUpperInvariant() switch
        {
            "LF" or "CF" or "RF" => "OF",
            var value => value
        };
    }

    private static decimal ComputeTransferFee(int overallRating, int age, decimal annualSalary, int yearsRemaining)
    {
        var ovrFactor = 1.0m + (Math.Max(0, overallRating - 50) * 0.028m);
        var ageFactor = age switch
        {
            <= 25 => 1.35m,
            <= 28 => 1.15m,
            <= 31 => 1.0m,
            <= 34 => 0.85m,
            _ => 0.70m
        };
        var yearsFactor = 1.0m + (Math.Clamp(yearsRemaining, 1, 5) * 0.10m);
        return decimal.Round(annualSalary * ovrFactor * ageFactor * yearsFactor, 0);
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

    private bool EnsureScoutDepartmentInitialized(TeamFranchiseState teamState, string teamName)
    {
        var hasChanges = false;
        teamState.AssistantScouts ??= new List<AssistantScoutState>();
        teamState.ScoutedPlayers ??= new List<ScoutedPlayerState>();

        for (var slotIndex = 0; slotIndex < 3; slotIndex++)
        {
            var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
            if (scout == null)
            {
                scout = new AssistantScoutState
                {
                    SlotIndex = slotIndex,
                    Country = ScoutingCountryOptions[Math.Min(slotIndex, ScoutingCountryOptions.Length - 1)],
                    PositionFocus = slotIndex switch
                    {
                        1 => "OF",
                        2 => "SP",
                        _ => "Any"
                    },
                    TraitFocus = slotIndex switch
                    {
                        1 => "Power Hitter",
                        2 => "Location Pitcher",
                        _ => "Best Athlete"
                    },
                    AssignmentMode = "Unassigned"
                };
                teamState.AssistantScouts.Add(scout);
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(scout.Country))
            {
                scout.Country = ScoutingCountryOptions[Math.Min(slotIndex, ScoutingCountryOptions.Length - 1)];
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(scout.PositionFocus))
            {
                scout.PositionFocus = "Any";
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(scout.TraitFocus))
            {
                scout.TraitFocus = "Best Athlete";
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(scout.AssignmentMode) || !ScoutAssignmentModeOptions.Any(option => string.Equals(option, scout.AssignmentMode, StringComparison.OrdinalIgnoreCase)))
            {
                scout.AssignmentMode = "Unassigned";
                hasChanges = true;
            }

            scout.AssignmentTarget ??= string.Empty;
            scout.DaysUntilNextDiscovery = Math.Max(0, scout.DaysUntilNextDiscovery);
        }

        teamState.AssistantScouts = teamState.AssistantScouts
            .OrderBy(scout => scout.SlotIndex)
            .Take(3)
            .ToList();

        return hasChanges;
    }

    private List<AssistantScoutState> BuildScoutCandidatePool(string teamName, int slotIndex)
    {
        return BuildCoachCandidatePool(teamName, $"Regional Scout {slotIndex + 1}")
            .Select(candidate => new AssistantScoutState
            {
                SlotIndex = slotIndex,
                Name = candidate.Name,
                Specialty = candidate.Specialty,
                Voice = candidate.Voice
            })
            .ToList();
    }

    private string CycleAssistantScoutSetting(int slotIndex, int direction, string[] options, Func<AssistantScoutState, string> getter, Action<AssistantScoutState, string> setter, string label)
    {
        if (SelectedTeam == null)
        {
            return "Select a team before changing your scouts.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null)
        {
            return "That scout slot is not available right now.";
        }

        var currentValue = getter(scout);
        var currentIndex = Array.FindIndex(options, option => string.Equals(option, currentValue, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var step = direction >= 0 ? 1 : -1;
        var nextValue = options[(currentIndex + step + options.Length) % options.Length];
        setter(scout, nextValue);
        if (string.Equals(scout.AssignmentMode, "Region Search", StringComparison.OrdinalIgnoreCase))
        {
            scout.AssignmentTarget = $"{scout.Country}|{scout.PositionFocus}|{scout.TraitFocus}";
            scout.DaysUntilNextDiscovery = GetScoutDiscoveryDays(SelectedTeam.Name, scout);
        }
        else if (string.Equals(scout.AssignmentMode, "Auto Need Search", StringComparison.OrdinalIgnoreCase))
        {
            ApplyAutoScoutNeedPlan(SelectedTeam.Name, scout);
            scout.DaysUntilNextDiscovery = GetScoutDiscoveryDays(SelectedTeam.Name, scout);
        }
        Save();

        var scoutLabel = string.IsNullOrWhiteSpace(scout.Name) ? $"Scout {slotIndex + 1}" : scout.Name;
        return $"{scoutLabel} {label} set to {nextValue}.";
    }

    private string SetAssistantScoutSetting(int slotIndex, string selectedValue, string[] options, Func<AssistantScoutState, string> getter, Action<AssistantScoutState, string> setter, string label)
    {
        if (SelectedTeam == null)
        {
            return "Select a team before changing your scouts.";
        }

        if (!options.Any(option => string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase)))
        {
            return $"That {label} option is not available right now.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var scout = teamState.AssistantScouts.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
        if (scout == null)
        {
            return "That scout slot is not available right now.";
        }

        var normalizedValue = options.First(option => string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(getter(scout), normalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            var scoutName = string.IsNullOrWhiteSpace(scout.Name) ? $"Scout {slotIndex + 1}" : scout.Name;
            return $"{scoutName} is already set to {normalizedValue}.";
        }

        setter(scout, normalizedValue);
        if (string.Equals(scout.AssignmentMode, "Region Search", StringComparison.OrdinalIgnoreCase))
        {
            scout.AssignmentTarget = $"{scout.Country}|{scout.PositionFocus}|{scout.TraitFocus}";
            scout.DaysUntilNextDiscovery = GetScoutDiscoveryDays(SelectedTeam.Name, scout);
        }
        else if (string.Equals(scout.AssignmentMode, "Auto Need Search", StringComparison.OrdinalIgnoreCase))
        {
            ApplyAutoScoutNeedPlan(SelectedTeam.Name, scout);
            scout.DaysUntilNextDiscovery = GetScoutDiscoveryDays(SelectedTeam.Name, scout);
        }
        Save();

        var scoutLabel = string.IsNullOrWhiteSpace(scout.Name) ? $"Scout {slotIndex + 1}" : scout.Name;
        return $"{scoutLabel} {label} set to {normalizedValue}.";
    }

    private string SetScoutedPlayerTargetStatus(string prospectKey, bool isOnTargetList)
    {
        if (SelectedTeam == null)
        {
            return "Select a team before managing your target list.";
        }

        var teamState = GetOrCreateTeamState(SelectedTeam.Name);
        EnsureScoutDepartmentInitialized(teamState, SelectedTeam.Name);
        var player = teamState.ScoutedPlayers.FirstOrDefault(entry => string.Equals(entry.ProspectKey, prospectKey, StringComparison.OrdinalIgnoreCase));
        if (player == null)
        {
            return "That player is not on your scouted board yet.";
        }

        if (player.IsOnTargetList == isOnTargetList)
        {
            return isOnTargetList
                ? $"{player.PlayerName} is already on your target list."
                : $"{player.PlayerName} is already back on the general scouting board.";
        }

        player.IsOnTargetList = isOnTargetList;
        Save();
        return isOnTargetList
            ? $"{player.PlayerName} was added to your target list."
            : $"{player.PlayerName} was moved back to the general scouted players list.";
    }

    private AmateurProspectView BuildAmateurProspectView(ScoutedPlayerState player)
    {
        return new AmateurProspectView(
            player.ProspectKey,
            player.PlayerName,
            player.Country,
            player.Source,
            player.PrimaryPosition,
            player.Age,
            player.TraitFocus,
            player.FoundByScoutName,
            player.Projection,
            player.Summary,
            player.EstimatedBonus,
            player.ScoutingProgress,
            player.IsOnTargetList,
            string.IsNullOrWhiteSpace(player.AssignedScoutName) ? "Unassigned" : player.AssignedScoutName);
    }

    private string GetScoutAssignmentTargetLabel(TeamFranchiseState teamState, AssistantScoutState scout)
    {
        return scout.AssignmentMode switch
        {
            "Region Search" => $"{scout.Country} / {scout.PositionFocus} / {scout.TraitFocus}",
            "Auto Need Search" => $"Auto: {scout.Country} / {scout.PositionFocus} / {scout.TraitFocus}",
            "Player Follow" => teamState.ScoutedPlayers.FirstOrDefault(player => string.Equals(player.ProspectKey, scout.AssignmentTarget, StringComparison.OrdinalIgnoreCase))?.PlayerName ?? "Selected player",
            _ => "Not assigned"
        };
    }

    private void ApplyAutoScoutNeedPlan(string teamName, AssistantScoutState scout)
    {
        var neededPosition = GetMostNeededPosition(teamName);
        scout.PositionFocus = NormalizeAutoScoutPositionFocus(neededPosition);
        scout.TraitFocus = GetAutoScoutTraitFocus(scout.PositionFocus);
        if (string.IsNullOrWhiteSpace(scout.Country))
        {
            scout.Country = "U.S. High School";
        }

        scout.AssignmentTarget = $"{scout.Country}|{scout.PositionFocus}|{scout.TraitFocus}";
    }

    private static string NormalizeAutoScoutPositionFocus(string neededPosition)
    {
        return neededPosition switch
        {
            "LF" or "CF" or "RF" => "OF",
            "DH" => "Any",
            _ when string.IsNullOrWhiteSpace(neededPosition) => "Any",
            _ => neededPosition
        };
    }

    private static string GetAutoScoutTraitFocus(string positionFocus)
    {
        return positionFocus switch
        {
            "SP" => "Workhorse Starter",
            "RP" => "Late-Inning Arm",
            "C" => "Disciplined Hitter",
            "2B" or "SS" => "Speed / Defense",
            "1B" or "3B" or "OF" => "Power Hitter",
            _ => "Best Athlete"
        };
    }

    private void ClearScoutPlayerAssignment(TeamFranchiseState teamState, int slotIndex)
    {
        teamState.ScoutedPlayers ??= new List<ScoutedPlayerState>();
        foreach (var player in teamState.ScoutedPlayers.Where(entry => entry.AssignedScoutSlotIndex == slotIndex))
        {
            player.AssignedScoutSlotIndex = null;
            player.AssignedScoutName = string.Empty;
        }
    }

    private int GetScoutDiscoveryDays(string teamName, AssistantScoutState scout)
    {
        var random = CreateStableRandom(teamName, scout.SlotIndex.ToString(), scout.Country, scout.PositionFocus, scout.TraitFocus, "discovery-days");
        return string.Equals(scout.Country, "U.S. High School", StringComparison.OrdinalIgnoreCase)
            ? 4 + random.Next(0, 4)
            : 5 + random.Next(0, 5);
    }

    private void ProcessScoutingBetweenDates(DateTime currentDate, DateTime targetDate)
    {
        for (var date = currentDate.Date; date < targetDate.Date; date = date.AddDays(1))
        {
            foreach (var team in _leagueData.Teams)
            {
                AdvanceScoutDepartmentForDate(team.Name, date);
            }
        }
    }

    private void AdvanceScoutDepartmentForDate(string teamName, DateTime scoutingDate)
    {
        var teamState = GetOrCreateTeamState(teamName);
        EnsureScoutDepartmentInitialized(teamState, teamName);

        foreach (var scout in teamState.AssistantScouts.Where(entry => !string.IsNullOrWhiteSpace(entry.Name)))
        {
            switch (scout.AssignmentMode)
            {
                case "Region Search":
                    if (scout.DaysUntilNextDiscovery <= 0)
                    {
                        scout.DaysUntilNextDiscovery = GetScoutDiscoveryDays(teamName, scout);
                    }

                    scout.DaysUntilNextDiscovery--;
                    if (scout.DaysUntilNextDiscovery <= 0)
                    {
                        var discoveryIndex = teamState.ScoutedPlayers.Count(player => player.FoundByScoutSlotIndex == scout.SlotIndex);
                        teamState.ScoutedPlayers.Add(BuildScoutedPlayerDiscovery(teamName, scout, scoutingDate, discoveryIndex));
                        scout.DaysUntilNextDiscovery = GetScoutDiscoveryDays(teamName, scout);
                    }
                    break;

                case "Auto Need Search":
                    ApplyAutoScoutNeedPlan(teamName, scout);
                    if (scout.DaysUntilNextDiscovery <= 0)
                    {
                        scout.DaysUntilNextDiscovery = GetScoutDiscoveryDays(teamName, scout);
                    }

                    scout.DaysUntilNextDiscovery--;
                    if (scout.DaysUntilNextDiscovery <= 0)
                    {
                        ApplyAutoScoutNeedPlan(teamName, scout);
                        var discoveryIndex = teamState.ScoutedPlayers.Count(player => player.FoundByScoutSlotIndex == scout.SlotIndex);
                        teamState.ScoutedPlayers.Add(BuildScoutedPlayerDiscovery(teamName, scout, scoutingDate, discoveryIndex));
                        ApplyAutoScoutNeedPlan(teamName, scout);
                        scout.DaysUntilNextDiscovery = GetScoutDiscoveryDays(teamName, scout);
                    }
                    break;

                case "Player Follow":
                    var followedPlayer = teamState.ScoutedPlayers.FirstOrDefault(player => string.Equals(player.ProspectKey, scout.AssignmentTarget, StringComparison.OrdinalIgnoreCase));
                    if (followedPlayer == null)
                    {
                        scout.AssignmentMode = "Unassigned";
                        scout.AssignmentTarget = string.Empty;
                        scout.DaysUntilNextDiscovery = 0;
                        break;
                    }

                    followedPlayer.AssignedScoutSlotIndex = scout.SlotIndex;
                    followedPlayer.AssignedScoutName = scout.Name;
                    if (followedPlayer.ScoutingProgress < 100)
                    {
                        var random = CreateStableRandom(teamName, scout.Name, followedPlayer.ProspectKey, scoutingDate.ToString("yyyyMMdd"), "player-follow");
                        var gain = 8 + random.Next(0, 9);
                        followedPlayer.ScoutingProgress = Math.Min(100, followedPlayer.ScoutingProgress + gain);
                    }
                    break;
            }
        }
    }

    private ScoutedPlayerState BuildScoutedPlayerDiscovery(string teamName, AssistantScoutState scout, DateTime scoutingDate, int discoveryIndex)
    {
        var random = CreateStableRandom(teamName, scout.SlotIndex.ToString(), scout.Name, scout.Country, scout.PositionFocus, scout.TraitFocus, scoutingDate.ToString("yyyyMMdd"), discoveryIndex.ToString());
        var source = string.Equals(scout.Country, "U.S. High School", StringComparison.OrdinalIgnoreCase) ? "High School" : "International";
        var primaryPosition = ResolveScoutedPosition(scout.PositionFocus, scout.TraitFocus, random);
        var age = source == "High School" ? 16 + random.Next(0, 3) : 18 + random.Next(0, 5);
        var playerName = $"{Pick(random, ProspectFirstNames)} {Pick(random, ProspectLastNames)}";

        return new ScoutedPlayerState
        {
            ProspectKey = $"{scout.SlotIndex}:{scoutingDate:yyyyMMdd}:{discoveryIndex}:{GetStableHash(playerName + scout.Country)}",
            PlayerName = playerName,
            Country = scout.Country,
            Source = source,
            PrimaryPosition = primaryPosition,
            Age = age,
            TraitFocus = scout.TraitFocus,
            FoundByScoutName = scout.Name,
            FoundByScoutSlotIndex = scout.SlotIndex,
            Projection = BuildProspectProjection(primaryPosition, scout.TraitFocus, random),
            Summary = BuildProspectSummary(scout.Country, primaryPosition, scout.TraitFocus, source, random),
            EstimatedBonus = BuildProspectBonusLabel(source, scout.TraitFocus, random),
            FoundDate = scoutingDate.Date,
            ScoutingProgress = 22 + random.Next(0, 19)
        };
    }

    private static string ResolveScoutedPosition(string positionFocus, string traitFocus, Random random)
    {
        if (!string.Equals(positionFocus, "Any", StringComparison.OrdinalIgnoreCase))
        {
            return positionFocus switch
            {
                "OF" => Pick(random, ["LF", "CF", "RF"]),
                _ => positionFocus
            };
        }

        return traitFocus switch
        {
            "Power Pitcher" or "Location Pitcher" or "Workhorse Starter" => Pick(random, ["SP", "SP", "RP"]),
            "Late-Inning Arm" => "RP",
            "Power Hitter" => Pick(random, ["1B", "3B", "LF", "RF", "C"]),
            "Contact Hitter" => Pick(random, ["2B", "SS", "CF", "C"]),
            "Disciplined Hitter" => Pick(random, ["2B", "3B", "SS", "C"]),
            "Speed / Defense" => Pick(random, ["CF", "SS", "2B"]),
            _ => Pick(random, ["C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "SP", "RP"])
        };
    }

    private static string BuildProspectProjection(string primaryPosition, string traitFocus, Random random)
    {
        var isPitcher = primaryPosition is "SP" or "RP";
        if (isPitcher)
        {
            return traitFocus switch
            {
                "Power Pitcher" => Pick(random, ["Power-arm upside", "Late-inning velocity look", "Explosive mound ceiling"]),
                "Location Pitcher" => Pick(random, ["Strike-throwing starter look", "Command-first profile", "Polished pitchability arm"]),
                "Workhorse Starter" => Pick(random, ["Mid-rotation starter frame", "Innings-eating starter look", "Durable rotation projection"]),
                _ => Pick(random, ["Pitching prospect to monitor", "Project arm with upside", "Interesting bullpen / starter split"])
            };
        }

        return traitFocus switch
        {
            "Power Hitter" => Pick(random, ["Middle-of-order power upside", "Run-producing bat", "Impact corner bat projection"]),
            "Contact Hitter" => Pick(random, ["Top-of-order bat-to-ball look", "Line-drive contact profile", "High-average offensive shape"]),
            "Disciplined Hitter" => Pick(random, ["On-base leaning profile", "Patient offensive approach", "Controlled strike-zone bat"]),
            "Speed / Defense" => Pick(random, ["Premium athlete profile", "Up-the-middle defender", "Table-setter speed look"]),
            _ => Pick(random, ["Balanced everyday upside", "Project regular look", "Interesting all-around profile"])
        };
    }

    private static string BuildProspectSummary(string country, string primaryPosition, string traitFocus, string source, Random random)
    {
        var originText = source == "High School"
            ? "The body is still filling out, but the athleticism stands out already."
            : $"The look out of {country} has a little more present polish than most young finds.";

        var skillText = traitFocus switch
        {
            "Power Hitter" => Pick(random, ["The raw power jumps in batting practice and he already creates loud contact.", "There is real carry off the barrel and the frame hints at more thump coming."]),
            "Contact Hitter" => Pick(random, ["He stays on the baseball and has a short, repeatable stroke.", "The barrel feel is advanced and he rarely looks rushed in the box."]),
            "Disciplined Hitter" => Pick(random, ["He works counts well and does not expand the zone much for his age.", "The at-bat quality is mature and the swing decisions are ahead of schedule."]),
            "Speed / Defense" => Pick(random, ["The first step and closing speed give him a chance to stay in a premium spot.", "The athleticism is easy to spot and the defensive range already plays."]),
            "Power Pitcher" => Pick(random, ["The ball comes out with life and the fastball has real force behind it.", "There is clear arm strength here, with the kind of power stuff clubs bet on."]),
            "Location Pitcher" => Pick(random, ["He repeats the delivery well and works to the edges with confidence.", "The command base is better than you usually see from a young arm."]),
            "Workhorse Starter" => Pick(random, ["He looks built to hold a starter's workload if the development keeps trending up.", "The frame and tempo suggest he could stay on turn every fifth day."]),
            "Late-Inning Arm" => Pick(random, ["The effort and finish fit a leverage bullpen look right away.", "There is enough bite and intent to picture him in short bursts late in games."]),
            _ => Pick(random, ["The overall athlete is worth tracking because several tools could still jump.", "There is enough across-the-board ability here to keep him on the follow list."])
        };

        var roleText = primaryPosition is "SP" or "RP"
            ? "The delivery still needs reps, but the mound traits are promising."
            : "The swing / glove combination still needs reps, but the foundation is worth following.";

        return $"{originText} {skillText} {roleText}";
    }

    private static string BuildProspectBonusLabel(string source, string traitFocus, Random random)
    {
        var baseBonus = source == "High School" ? 180_000 : 450_000;
        var traitBonus = traitFocus switch
        {
            "Power Hitter" or "Power Pitcher" => 250_000,
            "Workhorse Starter" or "Speed / Defense" => 175_000,
            _ => 110_000
        };

        var total = baseBonus + traitBonus + (random.Next(0, 6) * 40_000);
        return $"Est. bonus: ${total / 1000m:0}K";
    }

    private static string GetScoutFocusText(string positionFocus, string traitFocus)
    {
        var positionText = string.Equals(positionFocus, "Any", StringComparison.OrdinalIgnoreCase) ? "all positions" : positionFocus;
        return $"{positionText} with a {traitFocus.ToLowerInvariant()} lean";
    }

    private void InitializeLineupSlots(TeamFranchiseState teamState, string teamName)
    {
        var seededConfiguration = teamState.LineupSlots.Count == 9
            ? new LineupPresetConfiguration(
                CloneSlots(teamState.LineupSlots),
                ResolveDesignatedHitterPlayerId(teamName, teamState.LineupSlots, teamState.DesignatedHitterPlayerId))
            : BuildDefaultLineupConfiguration(teamName);

        if (teamState.VsLeftHandedPitcherLineupSlots.Count != 9)
        {
            teamState.VsLeftHandedPitcherLineupSlots = CloneSlots(seededConfiguration.Slots);
        }

        if (!teamState.VsLeftHandedPitcherDesignatedHitterPlayerId.HasValue)
        {
            teamState.VsLeftHandedPitcherDesignatedHitterPlayerId = seededConfiguration.DesignatedHitterPlayerId;
        }

        if (teamState.VsRightHandedPitcherLineupSlots.Count != 9)
        {
            teamState.VsRightHandedPitcherLineupSlots = CloneSlots(seededConfiguration.Slots);
        }

        if (!teamState.VsRightHandedPitcherDesignatedHitterPlayerId.HasValue)
        {
            teamState.VsRightHandedPitcherDesignatedHitterPlayerId = seededConfiguration.DesignatedHitterPlayerId;
        }

        SanitizeSlotsForCurrentRoster(teamState.VsLeftHandedPitcherLineupSlots, teamName);
        SanitizeSlotsForCurrentRoster(teamState.VsRightHandedPitcherLineupSlots, teamName);
        SanitizeDesignatedHitterForPreset(teamState, teamName, LineupPresetType.VsLeftHandedPitcher);
        SanitizeDesignatedHitterForPreset(teamState, teamName, LineupPresetType.VsRightHandedPitcher);

        if (!BuildLineupValidation(teamName, teamState.VsLeftHandedPitcherLineupSlots, teamState.VsLeftHandedPitcherDesignatedHitterPlayerId).IsValid)
        {
            var autoConfiguration = BuildAutoLineupConfiguration(teamName);
            teamState.VsLeftHandedPitcherLineupSlots = CloneSlots(autoConfiguration.Slots);
            teamState.VsLeftHandedPitcherDesignatedHitterPlayerId = autoConfiguration.DesignatedHitterPlayerId;
        }

        if (!BuildLineupValidation(teamName, teamState.VsRightHandedPitcherLineupSlots, teamState.VsRightHandedPitcherDesignatedHitterPlayerId).IsValid)
        {
            var autoConfiguration = BuildAutoLineupConfiguration(teamName);
            teamState.VsRightHandedPitcherLineupSlots = CloneSlots(autoConfiguration.Slots);
            teamState.VsRightHandedPitcherDesignatedHitterPlayerId = autoConfiguration.DesignatedHitterPlayerId;
        }

        teamState.LineupSlots = CloneSlots(teamState.VsRightHandedPitcherLineupSlots);
        teamState.DesignatedHitterPlayerId = teamState.VsRightHandedPitcherDesignatedHitterPlayerId;
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
        var validIds = GetAllPlayers()
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .Where(player => IsPlayerOnActiveTeamRoster(player.PlayerId))
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

    private static List<Guid?> CloneSlots(IReadOnlyList<Guid?> sourceSlots)
    {
        return sourceSlots.Take(9).Concat(Enumerable.Repeat<Guid?>(null, Math.Max(0, 9 - sourceSlots.Count))).Take(9).ToList();
    }

    private LineupPresetConfiguration BuildDefaultLineupConfiguration(string teamName)
    {
        var slots = Enumerable.Repeat<Guid?>(null, 9).ToList();
        Guid? designatedHitterPlayerId = null;
        foreach (var roster in _leagueData.Rosters.Where(roster =>
                     string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase) &&
                     roster.LineupSlot is >= 1 and <= 9))
        {
            slots[roster.LineupSlot!.Value - 1] = roster.PlayerId;
            if (string.Equals(roster.DefensivePosition, "DH", StringComparison.OrdinalIgnoreCase))
            {
                designatedHitterPlayerId = roster.PlayerId;
            }
        }

        designatedHitterPlayerId = ResolveDesignatedHitterPlayerId(teamName, slots, designatedHitterPlayerId);
        var importedConfiguration = new LineupPresetConfiguration(slots, designatedHitterPlayerId);
        return BuildLineupValidation(teamName, importedConfiguration.Slots, importedConfiguration.DesignatedHitterPlayerId).IsValid
            ? importedConfiguration
            : BuildAutoLineupConfiguration(teamName);
    }

    private List<Guid?> GetLineupSlotsForPreset(TeamFranchiseState teamState, string teamName, LineupPresetType lineupPresetType)
    {
        return lineupPresetType == LineupPresetType.VsLeftHandedPitcher
            ? teamState.VsLeftHandedPitcherLineupSlots
            : teamState.VsRightHandedPitcherLineupSlots;
    }

    private LineupSlotAssignments CaptureLineupSlotAssignments(TeamFranchiseState teamState, string teamName, Guid playerId)
    {
        var vsLeftSlots = GetLineupSlotsForPreset(teamState, teamName, LineupPresetType.VsLeftHandedPitcher);
        var vsRightSlots = GetLineupSlotsForPreset(teamState, teamName, LineupPresetType.VsRightHandedPitcher);
        return new LineupSlotAssignments(
            FindPlayerSlotIndex(vsLeftSlots, playerId),
            FindPlayerSlotIndex(vsRightSlots, playerId),
            teamState.VsLeftHandedPitcherDesignatedHitterPlayerId == playerId,
            teamState.VsRightHandedPitcherDesignatedHitterPlayerId == playerId);
    }

    private void ClearPlayerFromAllLineupSlots(TeamFranchiseState teamState, string teamName, Guid playerId)
    {
        ClearPlayerFromSlots(GetLineupSlotsForPreset(teamState, teamName, LineupPresetType.VsLeftHandedPitcher), playerId);
        ClearPlayerFromSlots(GetLineupSlotsForPreset(teamState, teamName, LineupPresetType.VsRightHandedPitcher), playerId);
        if (teamState.VsLeftHandedPitcherDesignatedHitterPlayerId == playerId)
        {
            teamState.VsLeftHandedPitcherDesignatedHitterPlayerId = null;
        }

        if (teamState.VsRightHandedPitcherDesignatedHitterPlayerId == playerId)
        {
            teamState.VsRightHandedPitcherDesignatedHitterPlayerId = null;
        }
    }

    private void TryAssignPlayerToPreviousSlots(TeamFranchiseState teamState, string teamName, Guid playerId, string primaryPosition, LineupSlotAssignments lineupAssignments, int rotationIndex)
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

        AssignPlayerToPreviousLineupSlot(GetLineupSlotsForPreset(teamState, teamName, LineupPresetType.VsLeftHandedPitcher), playerId, lineupAssignments.VsLeftHandedPitcherIndex);
        AssignPlayerToPreviousLineupSlot(GetLineupSlotsForPreset(teamState, teamName, LineupPresetType.VsRightHandedPitcher), playerId, lineupAssignments.VsRightHandedPitcherIndex);
        if (lineupAssignments.IsVsLeftHandedPitcherDesignatedHitter)
        {
            SetDesignatedHitterPlayerIdForPreset(teamState, LineupPresetType.VsLeftHandedPitcher, playerId);
        }

        if (lineupAssignments.IsVsRightHandedPitcherDesignatedHitter)
        {
            SetDesignatedHitterPlayerIdForPreset(teamState, LineupPresetType.VsRightHandedPitcher, playerId);
        }
    }

    private static void AssignPlayerToPreviousLineupSlot(List<Guid?> lineupSlots, Guid playerId, int lineupIndex)
    {
        if (lineupIndex >= 0)
        {
            AssignPlayerToSlotWithSwap(lineupSlots, playerId, lineupIndex);
            return;
        }

        var firstOpenLineup = lineupSlots.FindIndex(slot => !slot.HasValue);
        if (firstOpenLineup >= 0)
        {
            AssignPlayerToSlotWithSwap(lineupSlots, playerId, firstOpenLineup);
        }
    }

    private Guid? GetDesignatedHitterPlayerId(string teamName, LineupPresetType lineupPresetType)
    {
        var teamState = GetOrCreateTeamState(teamName);
        InitializeLineupSlots(teamState, teamName);
        return GetDesignatedHitterPlayerIdForPreset(teamState, lineupPresetType);
    }

    private bool IsDesignatedHitter(string teamName, LineupPresetType lineupPresetType, Guid playerId)
    {
        return GetDesignatedHitterPlayerId(teamName, lineupPresetType) == playerId;
    }

    private Guid? GetDesignatedHitterPlayerIdForPreset(TeamFranchiseState teamState, LineupPresetType lineupPresetType)
    {
        return lineupPresetType == LineupPresetType.VsLeftHandedPitcher
            ? teamState.VsLeftHandedPitcherDesignatedHitterPlayerId
            : teamState.VsRightHandedPitcherDesignatedHitterPlayerId;
    }

    private void SetDesignatedHitterPlayerIdForPreset(TeamFranchiseState teamState, LineupPresetType lineupPresetType, Guid? playerId)
    {
        if (lineupPresetType == LineupPresetType.VsLeftHandedPitcher)
        {
            teamState.VsLeftHandedPitcherDesignatedHitterPlayerId = playerId;
            return;
        }

        teamState.VsRightHandedPitcherDesignatedHitterPlayerId = playerId;
        teamState.DesignatedHitterPlayerId = playerId;
    }

    private void SanitizeDesignatedHitterForPreset(TeamFranchiseState teamState, string teamName, LineupPresetType lineupPresetType)
    {
        var lineupSlots = GetLineupSlotsForPreset(teamState, teamName, lineupPresetType);
        var designatedHitterPlayerId = ResolveDesignatedHitterPlayerId(teamName, lineupSlots, GetDesignatedHitterPlayerIdForPreset(teamState, lineupPresetType));
        SetDesignatedHitterPlayerIdForPreset(teamState, lineupPresetType, designatedHitterPlayerId);
    }

    private Guid? ResolveDesignatedHitterPlayerId(string teamName, IReadOnlyList<Guid?> lineupSlots, Guid? designatedHitterPlayerId)
    {
        var validPlayerIds = lineupSlots
            .Where(playerId => playerId.HasValue)
            .Select(playerId => playerId!.Value)
            .ToHashSet();

        if (designatedHitterPlayerId.HasValue && validPlayerIds.Contains(designatedHitterPlayerId.Value))
        {
            return designatedHitterPlayerId;
        }

        var importedDesignatedHitter = _leagueData.Rosters.FirstOrDefault(roster =>
            string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase)
            && roster.LineupSlot is >= 1 and <= 9
            && validPlayerIds.Contains(roster.PlayerId)
            && string.Equals(roster.DefensivePosition, "DH", StringComparison.OrdinalIgnoreCase));
        if (importedDesignatedHitter != null)
        {
            return importedDesignatedHitter.PlayerId;
        }

        return GetAllPlayers()
            .Where(player => validPlayerIds.Contains(player.PlayerId))
            .OrderByDescending(player => string.Equals(player.PrimaryPosition, "DH", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(player => string.Equals(player.SecondaryPosition, "DH", StringComparison.OrdinalIgnoreCase))
            .Select(player => (Guid?)player.PlayerId)
            .FirstOrDefault();
    }

    private LineupPresetConfiguration BuildAutoLineupConfiguration(string teamName)
    {
        var rosterImportsByPlayerId = _leagueData.Rosters
            .GroupBy(roster => roster.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());

        var candidates = GetAllPlayers()
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .Where(player => IsPlayerOnActiveTeamRoster(player.PlayerId))
            .Where(player => player.PrimaryPosition is not "SP" and not "RP")
            .Select(player =>
            {
                rosterImportsByPlayerId.TryGetValue(player.PlayerId, out var rosterImport);
                var primaryPosition = string.IsNullOrWhiteSpace(rosterImport?.PrimaryPosition) ? player.PrimaryPosition : rosterImport.PrimaryPosition;
                var secondaryPosition = string.IsNullOrWhiteSpace(rosterImport?.SecondaryPosition) ? player.SecondaryPosition : rosterImport.SecondaryPosition;
                var ratings = GetPlayerRatings(player.PlayerId, player.FullName, primaryPosition, secondaryPosition, player.Age);
                return new DefaultLineupCandidate(
                    player.PlayerId,
                    player.FullName,
                    primaryPosition,
                    secondaryPosition,
                    player.Age,
                    ratings.OverallRating,
                    ratings.EffectiveContactRating,
                    ratings.EffectivePowerRating,
                    ratings.EffectiveDisciplineRating,
                    ratings.EffectiveSpeedRating);
            })
            .OrderByDescending(candidate => candidate.OverallRating)
            .ThenBy(candidate => candidate.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return new LineupPresetConfiguration(Enumerable.Repeat<Guid?>(null, 9).ToList(), null);
        }

        var selectedPlayers = new List<DefaultLineupCandidate>();
        var selectedPlayerIds = new HashSet<Guid>();
        foreach (var position in new[] { "C", "SS", "CF", "2B", "3B", "1B", "LF", "RF" })
        {
            var bestCandidate = candidates
                .Where(candidate => !selectedPlayerIds.Contains(candidate.PlayerId))
                .Select(candidate => new { Candidate = candidate, Score = GetLineupDefenseFitScore(candidate, position) })
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenByDescending(entry => entry.Candidate.OverallRating)
                .ThenBy(entry => entry.Candidate.PlayerName, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Candidate)
                .FirstOrDefault();
            if (bestCandidate == null)
            {
                continue;
            }

            selectedPlayers.Add(bestCandidate);
            selectedPlayerIds.Add(bestCandidate.PlayerId);
        }

        var designatedHitter = candidates
            .Where(candidate => !selectedPlayerIds.Contains(candidate.PlayerId))
            .OrderByDescending(GetDesignatedHitterScore)
            .ThenBy(candidate => candidate.PlayerName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (designatedHitter != null)
        {
            selectedPlayers.Add(designatedHitter);
            selectedPlayerIds.Add(designatedHitter.PlayerId);
        }

        foreach (var remainingCandidate in candidates)
        {
            if (selectedPlayers.Count >= 9 || !selectedPlayerIds.Add(remainingCandidate.PlayerId))
            {
                continue;
            }

            selectedPlayers.Add(remainingCandidate);
        }

        var battingOrder = selectedPlayers
            .Take(9)
            .OrderByDescending(GetBattingOrderScore)
            .ThenBy(candidate => candidate.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LineupPresetConfiguration(
            battingOrder.Select(candidate => (Guid?)candidate.PlayerId).Concat(Enumerable.Repeat<Guid?>(null, Math.Max(0, 9 - battingOrder.Count))).Take(9).ToList(),
            designatedHitter?.PlayerId);
    }

    private LineupValidationView BuildLineupValidation(string teamName, IReadOnlyList<Guid?> lineupSlots, Guid? designatedHitterPlayerId)
    {
        var rosterImportsByPlayerId = _leagueData.Rosters
            .GroupBy(roster => roster.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());
        var playersById = GetAllPlayers()
            .Where(player => string.Equals(GetAssignedTeamName(player.PlayerId, player.TeamName), teamName, StringComparison.OrdinalIgnoreCase))
            .Where(player => IsPlayerOnActiveTeamRoster(player.PlayerId))
            .ToDictionary(player => player.PlayerId, player => player);

        var lineupPlayers = lineupSlots
            .Select((playerId, index) => new { PlayerId = playerId, Slot = index + 1 })
            .Where(entry => entry.PlayerId.HasValue && playersById.ContainsKey(entry.PlayerId.Value))
            .Select(entry =>
            {
                var player = playersById[entry.PlayerId!.Value];
                rosterImportsByPlayerId.TryGetValue(player.PlayerId, out var rosterImport);
                var primaryPosition = string.IsNullOrWhiteSpace(rosterImport?.PrimaryPosition) ? player.PrimaryPosition : rosterImport.PrimaryPosition;
                var secondaryPosition = string.IsNullOrWhiteSpace(rosterImport?.SecondaryPosition) ? player.SecondaryPosition : rosterImport.SecondaryPosition;
                return new FranchiseRosterEntry(
                    player.PlayerId,
                    player.FullName,
                    primaryPosition,
                    secondaryPosition,
                    player.Age,
                    entry.Slot,
                    null,
                    BattingProfileFactory.ParseThrows(player.Throws),
                    designatedHitterPlayerId == player.PlayerId);
            })
            .ToList();

        return BuildLineupValidation(lineupPlayers);
    }

    private static int GetLineupDefenseFitScore(DefaultLineupCandidate candidate, string position)
    {
        if (string.Equals(candidate.PrimaryPosition, position, StringComparison.OrdinalIgnoreCase))
        {
            return 300;
        }

        if (string.Equals(candidate.SecondaryPosition, position, StringComparison.OrdinalIgnoreCase))
        {
            return 220;
        }

        return position switch
        {
            "LF" or "CF" or "RF" when string.Equals(candidate.PrimaryPosition, "OF", StringComparison.OrdinalIgnoreCase) => 180,
            "LF" or "CF" or "RF" when string.Equals(candidate.SecondaryPosition, "OF", StringComparison.OrdinalIgnoreCase) => 140,
            "1B" or "2B" or "3B" or "SS" when string.Equals(candidate.PrimaryPosition, "IF", StringComparison.OrdinalIgnoreCase) => 180,
            "1B" or "2B" or "3B" or "SS" when string.Equals(candidate.SecondaryPosition, "IF", StringComparison.OrdinalIgnoreCase) => 140,
            _ => 0
        };
    }

    private static int GetDesignatedHitterScore(DefaultLineupCandidate candidate)
    {
        var dhBonus = string.Equals(candidate.PrimaryPosition, "DH", StringComparison.OrdinalIgnoreCase)
            ? 25
            : string.Equals(candidate.SecondaryPosition, "DH", StringComparison.OrdinalIgnoreCase)
                ? 10
                : 0;
        return (candidate.ContactRating * 4) + (candidate.PowerRating * 4) + (candidate.DisciplineRating * 2) + dhBonus;
    }

    private static int GetBattingOrderScore(DefaultLineupCandidate candidate)
    {
        return (candidate.ContactRating * 4) + (candidate.PowerRating * 4) + (candidate.DisciplineRating * 2) + candidate.SpeedRating;
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

        foreach (var scoringPlayerId in result.ScoringPlayerIds.Distinct())
        {
            GetPlayerSeasonStats(scoringPlayerId).RunsScored++;
        }

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
        foreach (var player in team.Lineup)
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

        var participatingPitchers = team.PitchCountsByPitcher
            .Where(pair => pair.Value > 0)
            .Select(pair => new { Pitcher = team.FindPlayer(pair.Key), PitchCount = pair.Value })
            .Where(entry => entry.Pitcher != null)
            .Select(entry => new { Pitcher = entry.Pitcher!, entry.PitchCount })
            .ToList();

        if (participatingPitchers.Count == 0)
        {
            participatingPitchers.Add(new { Pitcher = team.StartingPitcher, PitchCount = team.PitchCount });
        }

        foreach (var pitcherEntry in participatingPitchers)
        {
            if (countedPlayers.Add(pitcherEntry.Pitcher.Id))
            {
                GetPlayerSeasonStats(pitcherEntry.Pitcher.Id).GamesPlayed++;
            }

            ApplyPitcherGameFatigue(pitcherEntry.Pitcher, pitcherEntry.PitchCount);

            var relieverStats = GetPlayerSeasonStats(pitcherEntry.Pitcher.Id);
            relieverStats.GamesPitched++;
        }

        var pitcherStats = GetPlayerSeasonStats(team.CurrentPitcher.Id);
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
        foreach (var player in finalState.AwayTeam.Lineup
                     .Concat(finalState.AwayTeam.PitchCountsByPitcher.Keys.Select(id => finalState.AwayTeam.FindPlayer(id)).OfType<MatchPlayerSnapshot>())
                     .Concat(finalState.HomeTeam.Lineup)
                     .Concat(finalState.HomeTeam.PitchCountsByPitcher.Keys.Select(id => finalState.HomeTeam.FindPlayer(id)).OfType<MatchPlayerSnapshot>())
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
                RunsScored = Math.Max(0, currentStats.RunsScored - previousTotals.RunsScored),
                AtBats = Math.Max(0, currentStats.AtBats - previousTotals.AtBats),
                Hits = Math.Max(0, currentStats.Hits - previousTotals.Hits),
                Doubles = Math.Max(0, currentStats.Doubles - previousTotals.Doubles),
                Triples = Math.Max(0, currentStats.Triples - previousTotals.Triples),
                HomeRuns = Math.Max(0, currentStats.HomeRuns - previousTotals.HomeRuns),
                Walks = Math.Max(0, currentStats.Walks - previousTotals.Walks),
                Strikeouts = Math.Max(0, currentStats.Strikeouts - previousTotals.Strikeouts),
                PitcherStrikeouts = Math.Max(0, currentStats.StrikeoutsPitched - previousTotals.PitcherStrikeouts)
            };

            if (recentGameLine.GamesPlayed > 0 || recentGameLine.InningsPitchedOuts > 0 || recentGameLine.RunsScored > 0 || recentGameLine.Hits > 0 || recentGameLine.HomeRuns > 0 || recentGameLine.Wins > 0 || recentGameLine.Losses > 0)
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
                RunsScored = currentStats.RunsScored,
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

    private void EnsureSeasonYearInitialized()
    {
        if (_saveState.CurrentSeasonYear > 0)
        {
            return;
        }

        _saveState.CurrentSeasonYear = GetBaseScheduleYear();
    }

    private void SyncPlayerAgesFromSave()
    {
        foreach (var player in _leagueData.Players)
        {
            if (_saveState.PlayerAges.TryGetValue(player.PlayerId, out var savedAge) && savedAge > 0)
            {
                player.Age = savedAge;
            }
        }
    }

    private bool EnsurePlayerAgesGenerated()
    {
        var hasChanges = false;

        foreach (var player in _leagueData.Players)
        {
            if (_saveState.PlayerAges.TryGetValue(player.PlayerId, out var savedAge) && savedAge > 0)
            {
                if (player.Age != savedAge)
                {
                    player.Age = savedAge;
                }

                continue;
            }

            _saveState.PlayerAges[player.PlayerId] = player.Age;
            hasChanges = true;
        }

        return hasChanges;
    }

    private int GetBaseScheduleYear()
    {
        return _leagueData.Schedule.FirstOrDefault()?.Date.Year ?? DateTime.Today.Year;
    }

    private List<ScheduleImportDto> GetSeasonSchedule()
    {
        var query = SelectedTeam == null
            ? GetActiveSchedule().AsEnumerable()
            : GetActiveSchedule().Where(game =>
                string.Equals(game.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.AwayTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase));

        return query
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameNumber)
            .ToList();
    }

    private int GetSeasonScheduleYear()
    {
        EnsureSeasonYearInitialized();
        return _saveState.CurrentSeasonYear;
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

        var activeSchedule = GetActiveSchedule();
        var next = activeSchedule
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

        return activeSchedule
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
        ProcessScoutingBetweenDates(currentDate, nextDate);
        ApplySelectedTeamAutomaticMinorLeaguePromotions();
        _saveState.CurrentFranchiseDate = nextDate;
        ClearTrainingReportsIfSeasonComplete();
    }

    private void AdvanceFranchiseDateToNextUnplayedGame()
    {
        if (IsSeasonAdvanceBlockedByPendingDraftWork())
        {
            return;
        }

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
                var teamState = GetOrCreateTeamState(team.Name);
                EnsureTeamEconomyInitialized(teamState, team.Name);
                var economy = teamState.Economy;
                _processMonthlyFinanceUseCase.Execute(economy, nextMonth, BuildMonthlyFinanceNotes(nextMonth, economy));
            }

            nextMonth = nextMonth.AddMonths(1);
        }
    }

    private static string BuildMonthlyFinanceNotes(DateTime processDate, TeamEconomy economy)
    {
        var playerPayrollExpense = decimal.Round(economy.PlayerPayroll / 12m, 2);
        var coachPayrollExpense = decimal.Round(economy.CoachPayroll / 12m, 2);
        return $"{processDate:MMMM yyyy} operating cycle (Player payroll: {GetBudgetDisplay(playerPayrollExpense)}, Staff payroll: {GetBudgetDisplay(coachPayrollExpense)}).";
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
        SimulateGamesInParallel(GetRemainingScheduledGamesForDate(date));
    }

    private void SimulateGamesInParallel(IReadOnlyList<ScheduleImportDto> scheduledGames)
    {
        if (scheduledGames.Count == 0)
        {
            return;
        }

        if (scheduledGames.Count == 1)
        {
            TrySimulateScheduledGame(scheduledGames[0], advanceDateAfterGame: false, out _);
            return;
        }

        var parallelizableGames = scheduledGames
            .Where(game => SelectedTeam == null ||
                           (!string.Equals(game.HomeTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(game.AwayTeamName, SelectedTeam.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var selectedTeamGames = scheduledGames
            .Except(parallelizableGames)
            .ToList();

        foreach (var game in selectedTeamGames)
        {
            TrySimulateScheduledGame(game, advanceDateAfterGame: false, out _);
        }

        if (parallelizableGames.Count == 0)
        {
            return;
        }

        var preparedGames = new List<PreparedGameSimulation>();
        foreach (var game in parallelizableGames)
        {
            if (TryPrepareGameSimulation(game, out var preparedGame, out _))
            {
                preparedGames.Add(preparedGame);
            }
        }

        var outcomes = new System.Collections.Concurrent.ConcurrentBag<SimulatedGameOutcome>();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        };

        Parallel.ForEach(preparedGames, options, preparedGame =>
        {
            if (TrySimulatePreparedGame(preparedGame, applyPerformanceDevelopmentDuringSim: false, out var finalState, out var summary))
            {
                outcomes.Add(new SimulatedGameOutcome(preparedGame.ScheduledGame, finalState, summary));
            }
        });

        foreach (var outcome in outcomes
                     .OrderBy(result => result.ScheduledGame.Date)
                     .ThenBy(result => result.ScheduledGame.GameNumber ?? 1)
                     .ThenBy(result => result.ScheduledGame.HomeTeamName))
        {
            RecordCompletedGame(outcome.FinalState);
            FinalizeFranchiseScheduledGame(outcome.FinalState, outcome.ScheduledGame, advanceDateAfterGame: false);
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

    public MatchTeamState CreateMatchTeamState(TeamImportDto team, string opposingTeamName, bool preferFranchiseSelections)
    {
        return BuildTeamSnapshot(team, opposingTeamName, preferFranchiseSelections);
    }

    private MatchTeamState BuildTeamSnapshot(TeamImportDto team, string opposingTeamName, bool preferFranchiseSelections)
    {
        List<MatchPlayerSnapshot> lineup;
        List<MatchPlayerSnapshot> bench;
        List<MatchPlayerSnapshot> bullpen;
        MatchPlayerSnapshot? pitcher;

        if (preferFranchiseSelections)
        {
            var lineupPresetType = GetLineupPresetForOpponent(opposingTeamName);
            lineup = BuildSelectedTeamLineup(lineupPresetType);
            bench = BuildSelectedTeamBench(lineup, lineupPresetType);
            bullpen = BuildSelectedTeamBullpen();
            pitcher = BuildSelectedTeamPitcher();
        }
        else
        {
            var aiPlan = BuildAiManagedTeamPlan(team.Name);
            lineup = aiPlan.Lineup.ToList();
            bench = aiPlan.Bench.ToList();
            bullpen = aiPlan.Bullpen.ToList();
            pitcher = aiPlan.StartingPitcher;
        }

        if (lineup.Count == 0)
        {
            lineup = BuildPlaceholderLineup(team.Name);
        }

        pitcher ??= lineup.First();

        return new MatchTeamState(team.Name, team.Abbreviation, lineup, pitcher, bench, bullpen);
    }

    private List<MatchPlayerSnapshot> BuildSelectedTeamLineup(LineupPresetType lineupPresetType)
    {
        var lineupPlayers = GetLineupPlayers(lineupPresetType)
            .Where(player => !ShouldRestPlayer(player.PlayerId, player.PrimaryPosition))
            .ToList();

        var remainingPlayers = GetSelectedTeamRoster(lineupPresetType)
            .Where(player => lineupPlayers.All(existing => existing.PlayerId != player.PlayerId) && player.PrimaryPosition is not "SP" and not "RP")
            .OrderBy(player => GetAvailabilityPriority(player.PlayerId, player.PrimaryPosition))
            .ThenBy(player => player.PlayerName)
            .ToList();

        foreach (var player in remainingPlayers)
        {
            if (lineupPlayers.Count >= 9)
            {
                break;
            }

            lineupPlayers.Add(player);
        }

        var defensiveAssignments = BuildLineupDefensiveAssignments(lineupPlayers);
        var lineup = lineupPlayers
            .OrderBy(player => player.LineupSlot ?? 99)
            .ThenBy(player => player.PlayerName)
            .Select(player => CreatePlayerSnapshot(
                player.PlayerId,
                player.PlayerName,
                player.PrimaryPosition,
                player.SecondaryPosition,
                player.Age,
                defensiveAssignments.GetValueOrDefault(player.PlayerId, player.IsDesignatedHitter ? "DH" : player.PrimaryPosition)))
            .ToList();

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

    private List<MatchPlayerSnapshot> BuildSelectedTeamBench(IReadOnlyList<MatchPlayerSnapshot> lineup, LineupPresetType lineupPresetType)
    {
        return GetBenchPlayers(lineupPresetType)
            .Where(player => lineup.All(existing => existing.Id != player.PlayerId) && !ShouldRestPlayer(player.PlayerId, player.PrimaryPosition))
            .OrderBy(player => GetAvailabilityPriority(player.PlayerId, player.PrimaryPosition))
            .ThenBy(player => player.PlayerName)
            .Select(player => CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age))
            .ToList();
    }

    private List<MatchPlayerSnapshot> BuildSelectedTeamBullpen()
    {
        return GetBullpenPlayers()
            .Where(player => !ShouldRestPlayer(player.PlayerId, player.PrimaryPosition))
            .OrderBy(player => GetAvailabilityPriority(player.PlayerId, player.PrimaryPosition))
            .ThenBy(player => player.PlayerName)
            .Select(player => CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age))
            .ToList();
    }

    private ManagerAiTeamPlan BuildAiManagedTeamPlan(string teamName)
    {
        var candidates = GetTeamRoster(teamName)
            .Select(player => new ManagerAiRosterCandidate(
                CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age),
                GetAvailabilityPriority(player.PlayerId, player.PrimaryPosition),
                !ShouldRestPlayer(player.PlayerId, player.PrimaryPosition),
                player.LineupSlot,
                player.RotationSlot))
            .ToList();
        return _managerAi.BuildTeamPlan(candidates);
    }

    private List<MatchPlayerSnapshot> BuildImportedTeamLineup(string teamName)
    {
        var lineup = BuildAiManagedTeamPlan(teamName).Lineup.ToList();

        while (lineup.Count < 9)
        {
            lineup.Add(CreatePlaceholderSnapshot($"{teamName} Fill {lineup.Count + 1}", lineup.Count + 1));
        }

        return lineup;
    }

    private MatchPlayerSnapshot? BuildImportedPitcher(string teamName)
    {
        return BuildAiManagedTeamPlan(teamName).StartingPitcher;
    }

    private IReadOnlyDictionary<Guid, string> BuildLineupDefensiveAssignments(IReadOnlyList<FranchiseRosterEntry> lineupPlayers)
    {
        return BuildLineupValidation(lineupPlayers)
            .PositionAssignments
            .Where(assignment => assignment.PlayerId.HasValue)
            .GroupBy(assignment => assignment.PlayerId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Position);
    }

    private MatchPlayerSnapshot CreatePlayerSnapshot(Guid playerId, string name, string primaryPosition, string secondaryPosition, int age, string? defensivePosition = null)
    {
        var ratings = GetPlayerRatings(playerId, name, primaryPosition, secondaryPosition, age);
        var importedPlayer = FindPlayerImport(playerId);
        var throws = BattingProfileFactory.ParseThrows(importedPlayer?.Throws);
        var battingStyle = BattingProfileFactory.ParseBattingStyle(importedPlayer?.BattingStyle);
        var battingProfile = BattingProfileFactory.Create(playerId, battingStyle, ratings.EffectiveContactRating, ratings.EffectivePowerRating, ratings.EffectiveDisciplineRating);

        return new MatchPlayerSnapshot(
            playerId,
            name,
            primaryPosition,
            secondaryPosition,
            string.IsNullOrWhiteSpace(defensivePosition) ? primaryPosition : defensivePosition,
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
                ratings.OverallRating,
                throws,
                battingProfile);
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

    public ScheduleImportDto? FinalizeFranchiseScheduledGame(MatchState finalState)
    {
        return FinalizeFranchiseScheduledGame(finalState, preferredGame: null, advanceDateAfterGame: true);
    }

    private ScheduleImportDto? FinalizeFranchiseScheduledGame(MatchState finalState, ScheduleImportDto? preferredGame, bool advanceDateAfterGame)
    {
        if (SelectedTeam == null)
        {
            return null;
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
            gameToMark = GetActiveSchedule()
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
            return null;
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

        return gameToMark;
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

        var isPitcher = player.PrimaryPosition is "SP" or "RP";
        var random = CreateStableRandom(player.PlayerId.ToString(), "practice-growth", practiceDate.ToString("yyyyMMdd"), focus.ToString(), player.PrimaryPosition);
        var ratingGain = RollPracticeDevelopmentGain(random);
        var economy = GetOrCreateTeamState(teamName).Economy;
        var developmentMultiplier = FranchiseEconomyEffects.GetDevelopmentMultiplier(economy.BudgetAllocation.PlayerDevelopmentBudget, economy.FacilitiesLevel);
        ratingGain = Math.Clamp(ratingGain * developmentMultiplier, 0d, 1.5d);
        ratingGain *= GetPracticeGrowthMultiplier(player.Age, isPitcher);
        ratingGain = RoundToHalfPoint(Math.Clamp(ratingGain, 0d, 1.5d));

        var attribute = PickPracticeDevelopmentAttribute(random, focus, player.PrimaryPosition);
        var coachRole = GetPracticeCoachRole(focus, player.PrimaryPosition, attribute);

        if (ratingGain > 0d)
        {
            AdjustPlayerRatings(player.PlayerId, ratings => ApplyPracticeDevelopmentGain(ratings, attribute, ratingGain));
            return new PracticeDevelopmentResult(coachRole, player.PlayerName, player.PrimaryPosition, attribute, ratingGain);
        }

        var declineChance = GetPracticeDeclineChance(player.Age, isPitcher);
        if (declineChance <= 0d || random.NextDouble() > declineChance)
        {
            return null;
        }

        var declineAmount = RollPracticeDeclineAmount(random, player.Age, isPitcher);
        if (declineAmount <= 0d)
        {
            return null;
        }

        AdjustPlayerRatings(player.PlayerId, ratings => ApplyPracticeDevelopmentGain(ratings, attribute, -declineAmount));
        return new PracticeDevelopmentResult(coachRole, player.PlayerName, player.PrimaryPosition, attribute, -declineAmount);
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

        var teamGames = GetActiveSchedule()
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

        var seasonYear = GetCurrentTrainingReportSeason();
        var coachNotes = GetPracticeReportCoachNotes(results);

        StoreTeamReport(
            SelectedTeam.Name,
            practiceDate.Date,
            seasonYear,
            BuildPracticeReportTitle(practiceDate, practiceSession),
            GetPracticeFocusLabel(practiceSession.Focus),
            results.Count == 0
                ? coachNotes[0]
                : $"{coachNotes.Count} staff update(s) were filed after the workout.",
            coachNotes,
            replaceSameDayReport: true);
    }

    private void StoreOffseasonReports(OffseasonSummaryState offseasonSummary)
    {
        if (SelectedTeam == null)
        {
            return;
        }

        var reportDate = GetCurrentFranchiseDate().Date;
        var seasonYear = offseasonSummary.NewSeasonYear;
        var selectedTeamName = SelectedTeam.Name;

        StoreTeamReport(
            selectedTeamName,
            reportDate,
            seasonYear,
            $"{offseasonSummary.CompletedSeasonYear}-{offseasonSummary.NewSeasonYear} Offseason Summary",
            "Offseason",
            offseasonSummary.Overview,
            BuildOffseasonOverviewNotes(offseasonSummary));

        var contractNotes = BuildOffseasonContractNotes(offseasonSummary);
        if (contractNotes.Count > 0)
        {
            StoreTeamReport(
                selectedTeamName,
                reportDate,
                seasonYear,
                "Contract Activity Report",
                "Contracts",
                $"{offseasonSummary.ExtensionsCompleted} extension(s) and {offseasonSummary.FreeAgentsSigned} signing(s) shaped the market heading into {offseasonSummary.NewSeasonYear}.",
                contractNotes);
        }

        var tradeNotes = BuildOffseasonTradeNotes(offseasonSummary);
        if (tradeNotes.Count > 0)
        {
            StoreTeamReport(
                selectedTeamName,
                reportDate,
                seasonYear,
                "Trade Activity Report",
                "Trades",
                $"{offseasonSummary.TradesCompleted} trade(s) landed around the league during the offseason cycle.",
                tradeNotes);
        }
    }

    private List<string> BuildOffseasonOverviewNotes(OffseasonSummaryState offseasonSummary)
    {
        var notes = new List<string>
        {
            $"{offseasonSummary.ExpiringContracts} contract(s) expired across the league.",
            $"{offseasonSummary.ExtensionOffers} extension offer(s) went out and {offseasonSummary.ExtensionsCompleted} were accepted.",
            $"{offseasonSummary.FreeAgentsSigned} free-agent signing(s) were completed.",
            $"{offseasonSummary.TradesCompleted} trade(s) were finalized across the league."
        };

        notes.AddRange(offseasonSummary.SelectedTeamContractDecisions
            .Select(decision => $"{decision.PlayerName}: {decision.Outcome} ({decision.TeamName})."));
        notes.AddRange(offseasonSummary.SelectedTeamTradeDecisions
            .Select(decision => decision.Description));

        if (notes.Count == 4)
        {
            notes.Add("Your club had a quiet offseason with no direct contract or trade moves logged.");
        }

        return notes;
    }

    private static List<string> BuildOffseasonContractNotes(OffseasonSummaryState offseasonSummary)
    {
        var notes = offseasonSummary.SelectedTeamContractDecisions
            .Select(decision =>
            {
                var salary = decision.AgreedSalary > 0m
                    ? decision.AgreedSalary
                    : (decision.TeamExpectedSalary > 0m ? decision.TeamExpectedSalary : decision.PlayerExpectedSalary);
                var years = decision.AgreedYears > 0
                    ? decision.AgreedYears
                    : (decision.TeamExpectedYears > 0 ? decision.TeamExpectedYears : decision.PlayerExpectedYears);
                var salaryLabel = salary > 0m ? $" at {salary:C0}/yr" : string.Empty;
                var termLabel = years > 0 ? $" for {years} year(s)" : string.Empty;
                return $"{decision.PlayerName}: {decision.Outcome}{termLabel}{salaryLabel}.";
            })
            .ToList();

        if (notes.Count == 0 && offseasonSummary.ExtensionOffers > 0)
        {
            notes.Add($"League front offices issued {offseasonSummary.ExtensionOffers} extension offer(s), but your club did not make a direct contract move.");
        }

        return notes;
    }

    private static List<string> BuildOffseasonTradeNotes(OffseasonSummaryState offseasonSummary)
    {
        var notes = offseasonSummary.SelectedTeamTradeDecisions
            .Select(decision => decision.Description)
            .ToList();

        notes.AddRange(offseasonSummary.LeagueNotes.Take(6));
        return notes;
    }

    private void StoreTeamReport(string teamName, DateTime reportDate, int seasonYear, string title, string focusLabel, string summary, IReadOnlyList<string> coachNotes, bool replaceSameDayReport = false)
    {
        var teamState = GetOrCreateTeamState(teamName);
        teamState.TrainingReports ??= new List<TrainingReportState>();

        if (replaceSameDayReport)
        {
            teamState.TrainingReports.RemoveAll(report => report.SeasonYear != seasonYear || report.ReportDate.Date == reportDate.Date);
        }
        else
        {
            teamState.TrainingReports.RemoveAll(report =>
                report.SeasonYear == seasonYear &&
                report.ReportDate.Date == reportDate.Date &&
                string.Equals(report.Title, title, StringComparison.OrdinalIgnoreCase));
        }

        teamState.TrainingReports.Add(new TrainingReportState
        {
            ReportDate = reportDate.Date,
            SeasonYear = seasonYear,
            Title = title,
            FocusLabel = focusLabel,
            Summary = summary,
            CoachNotes = coachNotes.ToList()
        });

        teamState.TrainingReports = teamState.TrainingReports
            .OrderByDescending(report => report.ReportDate)
            .ThenBy(report => report.Title)
            .Take(24)
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
        if (result.Amount < 0d)
        {
            return result.Attribute switch
            {
                PracticeDevelopmentAttribute.Contact => result.Amount <= -1d
                    ? $"{playerName} looked late on most swings"
                    : $"{playerName} lost a little timing at the plate",
                PracticeDevelopmentAttribute.Power => result.Amount <= -1d
                    ? $"{playerName} was not driving the ball like usual"
                    : $"{playerName} showed less pop in the swing",
                PracticeDevelopmentAttribute.Discipline => result.Amount <= -1d
                    ? $"{playerName} chased far more than usual"
                    : $"{playerName} looked less selective in counts",
                PracticeDevelopmentAttribute.Speed => result.Amount <= -1d
                    ? $"{playerName} clearly lost a step today"
                    : $"{playerName} looked a touch slower out of the box",
                PracticeDevelopmentAttribute.Fielding => result.Amount <= -1d
                    ? $"{playerName} had a rough day with glove consistency"
                    : $"{playerName}'s defensive footwork slipped a bit",
                PracticeDevelopmentAttribute.Arm => result.Amount <= -1d
                    ? $"{playerName}'s throws lacked their usual carry"
                    : $"{playerName}'s arm strength dipped a little",
                PracticeDevelopmentAttribute.Pitching => result.Amount <= -1d
                    ? $"{playerName} did not have his normal life on pitches"
                    : $"{playerName}'s command backed up slightly",
                PracticeDevelopmentAttribute.Stamina => result.Amount <= -1d
                    ? $"{playerName} tired out much earlier than expected"
                    : $"{playerName}'s stamina dipped a bit",
                _ => result.Amount <= -1d
                    ? $"{playerName} looked worn down by the workload"
                    : $"{playerName} looked a little slower to recover"
            };
        }

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

    private static double GetPracticeGrowthMultiplier(int age, bool isPitcher)
    {
        var peakAge = isPitcher ? 30 : 29;
        if (age <= peakAge - 4)
        {
            return 1.15d;
        }

        if (age <= peakAge)
        {
            return 1.00d;
        }

        if (age <= peakAge + 3)
        {
            return 0.75d;
        }

        if (age <= peakAge + 6)
        {
            return 0.45d;
        }

        if (age <= peakAge + 9)
        {
            return 0.20d;
        }

        return 0.05d;
    }

    private static double GetPracticeDeclineChance(int age, bool isPitcher)
    {
        var peakAge = isPitcher ? 30 : 29;
        if (age <= peakAge + 6)
        {
            return 0d;
        }

        if (age <= peakAge + 8)
        {
            return 0.10d;
        }

        if (age <= peakAge + 10)
        {
            return 0.20d;
        }

        if (age <= peakAge + 12)
        {
            return 0.32d;
        }

        if (age <= 39)
        {
            return 0.45d;
        }

        return 0.60d;
    }

    private static double RollPracticeDeclineAmount(Random random, int age, bool isPitcher)
    {
        var peakAge = isPitcher ? 30 : 29;
        if (age >= 40)
        {
            return random.NextDouble() < 0.45d ? 1.5d : 1d;
        }

        if (age >= peakAge + 12)
        {
            return random.NextDouble() < 0.28d ? 1d : 0.5d;
        }

        return 0.5d;
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

        var completedGames = GetActiveSchedule().Count(game =>
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

        var teamSchedule = GetActiveSchedule()
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

    private OffseasonContractEvaluation EvaluateContractForTeam(PlayerImportDto player, string teamName, decimal currentAnnualSalary)
    {
        var ratings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var currentStats = _saveState.PreviousSeasonStats.TryGetValue(player.PlayerId, out var previousSeasonStats)
            ? previousSeasonStats
            : GetPlayerSeasonStats(player.PlayerId);

        return _offseasonContractEvaluator.Evaluate(new OffseasonContractContext(
            player.PrimaryPosition is "SP" or "RP",
            player.Age,
            ratings.OverallRating,
            GetTeamNeedScore(teamName, player.PrimaryPosition),
            GetTransferBudget(teamName),
            currentAnnualSalary,
            BuildPerformanceSnapshot(currentStats)));
    }

    private static OffseasonPerformanceSnapshot BuildPerformanceSnapshot(PlayerSeasonStatsState stats)
    {
        return new OffseasonPerformanceSnapshot(
            stats.PlateAppearances,
            stats.AtBats,
            stats.Hits,
            stats.Doubles,
            stats.Triples,
            stats.HomeRuns,
            stats.Walks,
            stats.RunsScored,
            stats.RunsBattedIn,
            stats.InningsPitchedOuts,
            stats.EarnedRuns,
            stats.StrikeoutsPitched,
            stats.Wins,
            stats.Losses);
    }

    private static OffseasonContractDecisionState BuildContractDecision(string playerName, string teamName, string outcome, OffseasonContractEvaluation evaluation)
    {
        return new OffseasonContractDecisionState
        {
            PlayerName = playerName,
            TeamName = teamName,
            Outcome = outcome,
            PlayerExpectedSalary = evaluation.PlayerExpectation.AnnualSalary,
            PlayerExpectedYears = evaluation.PlayerExpectation.Years,
            TeamExpectedSalary = evaluation.TeamExpectation.AnnualSalary,
            TeamExpectedYears = evaluation.TeamExpectation.Years,
            AgreedSalary = evaluation.AgreedAnnualSalary,
            AgreedYears = evaluation.AgreedYears
        };
    }

    private void SignPlayerToTeam(PlayerImportDto player, string teamName, decimal annualSalary, int years)
    {
        _saveState.PlayerAssignments[player.PlayerId] = teamName;
        var teamState = GetOrCreateTeamState(teamName);
        _signPlayerContractUseCase.Execute(teamState.Economy, player.PlayerId, player.FullName, annualSalary, years, GetCurrentFranchiseDate());
        InitializeLineupSlots(teamState, teamName);
        InitializeRotationSlots(teamState, teamName);
    }

    private decimal GetTransferBudget(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return 0m;
        }

        var economy = GetOrCreateTeamState(teamName).Economy;
        return Math.Max(0m, decimal.Round(economy.CashOnHand - (economy.PlayerPayroll + economy.CoachPayroll), 2));
    }

    private decimal GetOffseasonMarketScore(PlayerImportDto player)
    {
        var ratings = GetPlayerRatings(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var stats = _saveState.PreviousSeasonStats.TryGetValue(player.PlayerId, out var previous) ? previous : GetPlayerSeasonStats(player.PlayerId);
        var evaluation = _offseasonContractEvaluator.Evaluate(new OffseasonContractContext(
            player.PrimaryPosition is "SP" or "RP",
            player.Age,
            ratings.OverallRating,
            2,
            30_000_000m,
            EstimatePlayerSalary(player),
            BuildPerformanceSnapshot(stats)));

        return evaluation.AgreedAnnualSalary + (ratings.OverallRating * 100_000m);
    }

    private bool TryExecuteOffseasonTrade(string teamName, HashSet<string> lockedTeams, out OffseasonTradeOutcome tradeOutcome)
    {
        tradeOutcome = default;
        var neededPosition = GetMostNeededPosition(teamName);
        if (string.IsNullOrWhiteSpace(neededPosition))
        {
            return false;
        }

        var buyingRoster = GetTeamRoster(teamName);
        foreach (var tradePartner in _leagueData.Teams.OrderBy(team => team.Name))
        {
            if (string.Equals(tradePartner.Name, teamName, StringComparison.OrdinalIgnoreCase) || lockedTeams.Contains(tradePartner.Name))
            {
                continue;
            }

            var partnerRoster = GetTeamRoster(tradePartner.Name);
            var target = partnerRoster
                .Where(player => string.Equals(NormalizeRosterPosition(player.PrimaryPosition), neededPosition, StringComparison.OrdinalIgnoreCase) && GetTeamPositionSurplus(tradePartner.Name, player.PrimaryPosition) > 0)
                .OrderByDescending(GetRosterPlayerOverall)
                .FirstOrDefault();
            if (target == null)
            {
                continue;
            }

            var outgoing = buyingRoster
                .Where(player =>
                    player.PlayerId != target.PlayerId &&
                    GetTeamPositionSurplus(teamName, player.PrimaryPosition) > 0 &&
                    GetTeamNeedScore(tradePartner.Name, player.PrimaryPosition) >= 1)
                .OrderBy(player => Math.Abs(GetRosterPlayerOverall(player) - GetRosterPlayerOverall(target)))
                .ThenByDescending(GetRosterPlayerOverall)
                .FirstOrDefault();
            outgoing ??= buyingRoster
                .Where(player =>
                    player.PlayerId != target.PlayerId &&
                    GetTeamPositionSurplus(teamName, player.PrimaryPosition) >= 0)
                .OrderBy(player => Math.Abs(GetRosterPlayerOverall(player) - GetRosterPlayerOverall(target)))
                .ThenBy(player => player.Age)
                .FirstOrDefault();
            if (outgoing == null)
            {
                continue;
            }

            if (Math.Abs(GetRosterPlayerOverall(target) - GetRosterPlayerOverall(outgoing)) > 20)
            {
                continue;
            }

            _saveState.PlayerAssignments[target.PlayerId] = teamName;
            _saveState.PlayerAssignments[outgoing.PlayerId] = tradePartner.Name;

            var buyerEconomy = GetOrCreateTeamState(teamName).Economy;
            var partnerEconomy = GetOrCreateTeamState(tradePartner.Name).Economy;
            MovePlayerContract(target.PlayerId, partnerEconomy, buyerEconomy, target.PlayerName);
            MovePlayerContract(outgoing.PlayerId, buyerEconomy, partnerEconomy, outgoing.PlayerName);

            InitializeLineupSlots(GetOrCreateTeamState(teamName), teamName);
            InitializeRotationSlots(GetOrCreateTeamState(teamName), teamName);
            InitializeLineupSlots(GetOrCreateTeamState(tradePartner.Name), tradePartner.Name);
            InitializeRotationSlots(GetOrCreateTeamState(tradePartner.Name), tradePartner.Name);

            var description = $"{teamName} swapped {outgoing.PlayerName} for {target.PlayerName} with {tradePartner.Name}.";
            AddTransferRecord(teamName, $"Offseason trade: acquired {target.PlayerName} from {tradePartner.Name} for {outgoing.PlayerName}.");
            AddTransferRecord(tradePartner.Name, $"Offseason trade: sent {target.PlayerName} to {teamName} for {outgoing.PlayerName}.");
            lockedTeams.Add(teamName);
            lockedTeams.Add(tradePartner.Name);
            tradeOutcome = new OffseasonTradeOutcome(teamName, tradePartner.Name, description);
            return true;
        }

        return false;
    }

    private string GetMostNeededPosition(string teamName)
    {
        var positions = new[] { "C", "1B", "2B", "3B", "SS", "OF", "SP", "RP" };
        return positions
            .Select(position => new
            {
                Position = position,
                Need = GetTeamNeedScore(teamName, position),
                Quality = GetTeamAverageOverallAtPosition(teamName, position)
            })
            .OrderByDescending(entry => entry.Need)
            .ThenBy(entry => entry.Quality)
            .Select(entry => entry.Position)
            .FirstOrDefault() ?? string.Empty;
    }

    private int GetTeamPositionSurplus(string teamName, string position)
    {
        var normalizedPosition = NormalizeRosterPosition(position);
        var countAtPosition = GetTeamRoster(teamName).Count(player => string.Equals(NormalizeRosterPosition(player.PrimaryPosition), normalizedPosition, StringComparison.OrdinalIgnoreCase));
        return countAtPosition - GetTargetDepth(normalizedPosition);
    }

    private int GetTeamNeedScore(string teamName, string position)
    {
        var normalizedPosition = NormalizeRosterPosition(position);
        var countAtPosition = GetTeamRoster(teamName).Count(player => string.Equals(NormalizeRosterPosition(player.PrimaryPosition), normalizedPosition, StringComparison.OrdinalIgnoreCase));
        return Math.Clamp(GetTargetDepth(normalizedPosition) - countAtPosition + 2, 0, 4);
    }

    private int GetTeamAverageOverallAtPosition(string teamName, string position)
    {
        var players = GetTeamRoster(teamName)
            .Where(player => string.Equals(NormalizeRosterPosition(player.PrimaryPosition), NormalizeRosterPosition(position), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (players.Count == 0)
        {
            return 0;
        }

        return (int)Math.Round(players.Average(GetRosterPlayerOverall), MidpointRounding.AwayFromZero);
    }

    private int GetRosterPlayerOverall(FranchiseRosterEntry player)
    {
        return GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).OverallRating;
    }

    private void ApplyOffseasonAging()
    {
        foreach (var player in _leagueData.Players)
        {
            player.Age = Math.Clamp(player.Age + 1, 17, 45);
            _saveState.PlayerAges[player.PlayerId] = player.Age;
        }
    }

    private void ResetHealthForNewSeason()
    {
        foreach (var health in _saveState.PlayerHealth.Values)
        {
            health.Fatigue = 0;
            health.PitchCountToday = 0;
            health.LastPitchCount = 0;
            health.DaysUntilAvailable = 0;
            health.InjuryDaysRemaining = 0;
            health.InjuryDescription = string.Empty;
        }
    }

    private static Dictionary<Guid, PlayerSeasonStatsState> CloneSeasonStatsMap(Dictionary<Guid, PlayerSeasonStatsState> source)
    {
        return source.ToDictionary(entry => entry.Key, entry => CloneSeasonStats(entry.Value));
    }

    private static PlayerSeasonStatsState CloneSeasonStats(PlayerSeasonStatsState stats)
    {
        return new PlayerSeasonStatsState
        {
            GamesPlayed = stats.GamesPlayed,
            PlateAppearances = stats.PlateAppearances,
            RunsScored = stats.RunsScored,
            AtBats = stats.AtBats,
            Hits = stats.Hits,
            Doubles = stats.Doubles,
            Triples = stats.Triples,
            HomeRuns = stats.HomeRuns,
            RunsBattedIn = stats.RunsBattedIn,
            Walks = stats.Walks,
            Strikeouts = stats.Strikeouts,
            GamesPitched = stats.GamesPitched,
            InningsPitchedOuts = stats.InningsPitchedOuts,
            HitsAllowed = stats.HitsAllowed,
            RunsAllowed = stats.RunsAllowed,
            EarnedRuns = stats.EarnedRuns,
            WalksAllowed = stats.WalksAllowed,
            StrikeoutsPitched = stats.StrikeoutsPitched,
            Wins = stats.Wins,
            Losses = stats.Losses
        };
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

    private static void ReportAsyncOperationProgress(Action<AsyncOperationProgressView>? reportProgress, string title, string status, double progressValue)
    {
        reportProgress?.Invoke(new AsyncOperationProgressView(title, status, progressValue));
    }

    private readonly record struct ExpiringPlayerContract(string TeamName, PlayerImportDto Player, decimal AnnualSalary);

    private readonly record struct FreeAgentOfferChoice(string TeamName, OffseasonContractEvaluation Evaluation, decimal Score);

    private readonly record struct OffseasonTradeOutcome(string ToTeamName, string FromTeamName, string Description);

    private readonly record struct PracticeSessionInfo(TeamPracticeFocus Focus, bool IsLightWorkout, bool IsSpringTraining);

    private readonly record struct PracticeDevelopmentResult(string CoachRole, string PlayerName, string PrimaryPosition, PracticeDevelopmentAttribute Attribute, double Amount);

    private readonly record struct PreparedGameSimulation(ScheduleImportDto ScheduledGame, MatchTeamState AwaySnapshot, MatchTeamState HomeSnapshot, string AwayAbbreviation, string HomeAbbreviation);

    private readonly record struct SimulatedGameOutcome(ScheduleImportDto ScheduledGame, MatchState FinalState, string Summary);

    private readonly record struct TeamGameResult(DateTime Date, int GameNumber, bool WonGame);

    private readonly record struct TeamRecordSummary(int Wins, int Losses, string Streak);

    private sealed record AttributeInsight(string Key, int Rating);
}
