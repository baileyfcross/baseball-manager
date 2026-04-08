using BaseballManager.Contracts.ImportDtos;
using BaseballManager.Game.Data;
using BaseballManager.Sim.Engine;
using Microsoft.Xna.Framework;

namespace BaseballManager.Game.Screens.LiveMatch;

public sealed class LiveMatchPresenter
{
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseSession _franchiseSession;
    private MatchEngine _engine = null!;
    private float _secondsUntilNextPitch;
    private float _ballHighlightTimer;
    private bool _isPaused;
    private LiveMatchMode _mode = LiveMatchMode.QuickMatch;

    public LiveMatchPresenter(ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _leagueData = leagueData;
        _franchiseSession = franchiseSession;
    }

    public LiveMatchViewModel ViewModel { get; private set; } = new();

    public void ResetMatch(LiveMatchMode mode)
    {
        _mode = mode;

        if (!TryRestoreSavedMatch())
        {
            _engine = CreateMatchEngine();
            _secondsUntilNextPitch = 0.85f;
            _ballHighlightTimer = 1.2f;
            _isPaused = false;
            SaveMatchProgress();
        }

        UpdateFieldView();
        UpdateOverlays();
    }

    public void Update(GameTime gameTime)
    {
        if (!_isPaused && !_engine.CurrentState.IsGameOver)
        {
            _secondsUntilNextPitch -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_secondsUntilNextPitch <= 0f)
            {
                ResolvePitchOutcome();
            }
        }

        UpdateRunnerMovement(gameTime);
        UpdateOverlays();
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        SaveMatchProgress();
        UpdateOverlays();
    }

    public void StepPitch()
    {
        if (_engine.CurrentState.IsGameOver)
        {
            return;
        }

        ResolvePitchOutcome();
    }

    public void UpdateFieldView()
    {
        var state = _engine.CurrentState;
        var firstRunnerName = state.GetRunnerName(state.Baserunners.FirstBaseRunnerId);
        var secondRunnerName = state.GetRunnerName(state.Baserunners.SecondBaseRunnerId);
        var thirdRunnerName = state.GetRunnerName(state.Baserunners.ThirdBaseRunnerId);
        var highlightAlpha = Math.Clamp(_ballHighlightTimer / 1.2f, 0.25f, 1f);

        ViewModel = new LiveMatchViewModel
        {
            AwayTeamName = state.AwayTeam.Name,
            HomeTeamName = state.HomeTeam.Name,
            AwayAbbreviation = state.AwayTeam.Abbreviation,
            HomeAbbreviation = state.HomeTeam.Abbreviation,
            AwayScore = state.AwayTeam.Runs,
            HomeScore = state.HomeTeam.Runs,
            InningNumber = state.Inning.Number,
            IsTopHalf = state.Inning.IsTopHalf,
            Balls = state.Count.Balls,
            Strikes = state.Count.Strikes,
            Outs = state.Inning.Outs,
            RunnerOnFirst = state.Baserunners.HasRunnerOnFirst,
            RunnerOnSecond = state.Baserunners.HasRunnerOnSecond,
            RunnerOnThird = state.Baserunners.HasRunnerOnThird,
            RunnerOnFirstName = firstRunnerName,
            RunnerOnSecondName = secondRunnerName,
            RunnerOnThirdName = thirdRunnerName,
            BatterName = state.CurrentBatter.FullName,
            PitcherName = state.CurrentPitcher.FullName,
            PitchCount = state.DefensiveTeam.PitchCount,
            PitcherFatigueText = BuildPitcherFatigueText(state.CurrentPitcher, state.DefensiveTeam.PitchCount),
            LatestPlayText = state.LatestEvent.Description,
            StatusText = BuildStatusText(state),
            IsPaused = _isPaused,
            IsGameOver = state.IsGameOver,
            BallXNormalized = state.Field.BallX,
            BallYNormalized = state.Field.BallY,
            BallVisible = state.Field.BallVisible,
            BallLabel = state.Field.BallLabel,
            HighlightedFielder = state.Field.HighlightedFielder,
            BallHighlightAlpha = highlightAlpha
        };
    }

    public void UpdateRunnerMovement(GameTime gameTime)
    {
        _ballHighlightTimer = Math.Max(0.25f, _ballHighlightTimer - (float)gameTime.ElapsedGameTime.TotalSeconds);
        UpdateFieldView();
    }

    public void UpdateRunnerMovement()
    {
        UpdateFieldView();
    }

    public void ResolvePitchOutcome()
    {
        var batter = _engine.CurrentState.CurrentBatter;
        var pitcher = _engine.CurrentState.CurrentPitcher;
        var defensiveTeam = _engine.CurrentState.DefensiveTeam;
        var result = _engine.Tick();

        if (_mode == LiveMatchMode.Franchise)
        {
            _franchiseSession.ApplyPerformanceDevelopment(batter, pitcher, defensiveTeam, result);
            if (result.IsGameOver)
            {
                _franchiseSession.RecordCompletedGame(_engine.CurrentState);
                _franchiseSession.FinalizeFranchiseScheduledGame(_engine.CurrentState);
            }
        }

        _secondsUntilNextPitch = 0.85f;
        _ballHighlightTimer = 1.2f;
        SaveMatchProgress();
        UpdateFieldView();
        UpdateOverlays();
    }

    public void UpdateOverlays()
    {
        UpdateFieldView();
    }

    public void HandleManagerCommands()
    {
        UpdateOverlays();
    }

    public void SaveMatchProgress()
    {
        if (_engine == null)
        {
            return;
        }

        if (_engine.CurrentState.IsGameOver)
        {
            _franchiseSession.ClearLiveMatchState(_mode);
            return;
        }

        var liveMatchState = LiveMatchStateMapper.FromMatchState(
            _engine.CurrentState,
            _secondsUntilNextPitch,
            _ballHighlightTimer,
            _isPaused);

        _franchiseSession.SaveLiveMatchState(_mode, liveMatchState);
    }

    private bool TryRestoreSavedMatch()
    {
        var savedMatch = _franchiseSession.GetLiveMatchState(_mode);
        if (savedMatch == null || savedMatch.IsGameOver)
        {
            if (savedMatch?.IsGameOver == true)
            {
                _franchiseSession.ClearLiveMatchState(_mode);
            }

            return false;
        }

        var restoredMatch = LiveMatchStateMapper.ToRuntimeState(savedMatch);
        _engine = new MatchEngine(restoredMatch.MatchState);
        _secondsUntilNextPitch = restoredMatch.SecondsUntilNextPitch;
        _ballHighlightTimer = restoredMatch.BallHighlightTimer;
        _isPaused = restoredMatch.IsPaused;
        return true;
    }

    private MatchEngine CreateMatchEngine()
    {
        var (awayTeam, homeTeam) = SelectMatchup();
        var useFranchiseSelectionsForAway = _mode == LiveMatchMode.Franchise && _franchiseSession.SelectedTeam != null && string.Equals(_franchiseSession.SelectedTeam.Name, awayTeam.Name, StringComparison.OrdinalIgnoreCase);
        var useFranchiseSelectionsForHome = _mode == LiveMatchMode.Franchise && _franchiseSession.SelectedTeam != null && string.Equals(_franchiseSession.SelectedTeam.Name, homeTeam.Name, StringComparison.OrdinalIgnoreCase);
        var awaySnapshot = BuildTeamSnapshot(awayTeam, preferFranchiseSelections: useFranchiseSelectionsForAway);
        var homeSnapshot = BuildTeamSnapshot(homeTeam, preferFranchiseSelections: useFranchiseSelectionsForHome);
        return new MatchEngine(awaySnapshot, homeSnapshot);
    }

    private (TeamImportDto Away, TeamImportDto Home) SelectMatchup()
    {
        var fallbackHome = _leagueData.Teams.FirstOrDefault() ?? CreateFallbackTeam("Home", "HOME");
        var fallbackAway = _leagueData.Teams.Skip(1).FirstOrDefault() ?? CreateFallbackTeam("Visitors", "VIS");

        if (_mode == LiveMatchMode.QuickMatch)
        {
            return SelectRandomMatchup(fallbackAway, fallbackHome);
        }

        if (_franchiseSession.SelectedTeam != null)
        {
            var selectedTeam = _franchiseSession.SelectedTeam;
            var scheduledGame = _franchiseSession.GetNextScheduledGame();

            if (scheduledGame != null)
            {
                var awayTeam = FindTeamByName(scheduledGame.AwayTeamName) ?? fallbackAway;
                var homeTeam = FindTeamByName(scheduledGame.HomeTeamName) ?? fallbackHome;
                return (awayTeam, homeTeam);
            }

            var opponent = _leagueData.Teams.FirstOrDefault(team => !string.Equals(team.Name, selectedTeam.Name, StringComparison.OrdinalIgnoreCase)) ?? fallbackAway;
            return (opponent, selectedTeam);
        }

        return (fallbackAway, fallbackHome);
    }

    private (TeamImportDto Away, TeamImportDto Home) SelectRandomMatchup(TeamImportDto fallbackAway, TeamImportDto fallbackHome)
    {
        if (_leagueData.Teams.Count == 0)
        {
            return (fallbackAway, fallbackHome);
        }

        if (_leagueData.Teams.Count == 1)
        {
            return (fallbackAway, _leagueData.Teams[0]);
        }

        var homeIndex = Random.Shared.Next(_leagueData.Teams.Count);
        var awayIndex = homeIndex;

        while (awayIndex == homeIndex)
        {
            awayIndex = Random.Shared.Next(_leagueData.Teams.Count);
        }

        return (_leagueData.Teams[awayIndex], _leagueData.Teams[homeIndex]);
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
        var lineup = _franchiseSession.GetLineupPlayers()
            .Where(player => !ShouldRestPlayer(player.PlayerId, player.PrimaryPosition))
            .OrderBy(player => player.LineupSlot)
            .Select(player => CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age, player.LineupSlot ?? 9, player.RotationSlot ?? 0))
            .ToList();

        var remainingPlayers = _franchiseSession.GetSelectedTeamRoster()
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

            lineup.Add(CreatePlayerSnapshot(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age, lineup.Count + 1, player.RotationSlot ?? 0));
        }

        while (lineup.Count < 9)
        {
            lineup.Add(CreatePlaceholderSnapshot($"Bench Fill {lineup.Count + 1}", lineup.Count + 1));
        }

        return lineup;
    }

    private MatchPlayerSnapshot? BuildSelectedTeamPitcher()
    {
        var pitcher = _franchiseSession.GetScheduledStartingPitcher(_franchiseSession.SelectedTeamName);
        return pitcher == null
            ? null
            : CreatePlayerSnapshot(pitcher.PlayerId, pitcher.PlayerName, pitcher.PrimaryPosition, pitcher.SecondaryPosition, pitcher.Age, pitcher.LineupSlot ?? 9, pitcher.RotationSlot ?? 1);
    }

    private List<MatchPlayerSnapshot> BuildImportedTeamLineup(string teamName)
    {
        var playersById = _leagueData.Players.ToDictionary(player => player.PlayerId, player => player);
        var lineupRows = _leagueData.Rosters
            .Where(roster => string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase) && roster.LineupSlot is >= 1 and <= 9)
            .OrderBy(roster => roster.LineupSlot)
            .ToList();

        var lineup = lineupRows
            .Where(roster => !ShouldRestPlayer(roster.PlayerId, roster.PrimaryPosition))
            .Select(roster =>
            {
                playersById.TryGetValue(roster.PlayerId, out var playerData);
                return CreatePlayerSnapshot(
                    roster.PlayerId,
                    roster.PlayerName,
                    roster.PrimaryPosition,
                    roster.SecondaryPosition,
                    playerData?.Age ?? 27,
                    roster.LineupSlot ?? 9,
                    roster.RotationSlot ?? 0);
            })
            .ToList();

        var fillPlayers = _leagueData.Rosters
            .Where(roster => string.Equals(roster.TeamName, teamName, StringComparison.OrdinalIgnoreCase) && lineup.All(existing => existing.Id != roster.PlayerId) && roster.PrimaryPosition is not "SP" and not "RP")
            .OrderBy(roster => GetAvailabilityPriority(roster.PlayerId, roster.PrimaryPosition))
            .ThenBy(roster => roster.PlayerName)
            .ToList();

        foreach (var roster in fillPlayers)
        {
            if (lineup.Count >= 9)
            {
                break;
            }

            playersById.TryGetValue(roster.PlayerId, out var playerData);
            lineup.Add(CreatePlayerSnapshot(roster.PlayerId, roster.PlayerName, roster.PrimaryPosition, roster.SecondaryPosition, playerData?.Age ?? 27, lineup.Count + 1, roster.RotationSlot ?? 0));
        }

        while (lineup.Count < 9)
        {
            lineup.Add(CreatePlaceholderSnapshot($"{teamName} Fill {lineup.Count + 1}", lineup.Count + 1));
        }

        return lineup;
    }

    private MatchPlayerSnapshot? BuildImportedPitcher(string teamName)
    {
        var pitcherRow = _franchiseSession.GetScheduledStartingPitcher(teamName);
        if (pitcherRow == null)
        {
            return null;
        }

        return CreatePlayerSnapshot(pitcherRow.PlayerId, pitcherRow.PlayerName, pitcherRow.PrimaryPosition, pitcherRow.SecondaryPosition, pitcherRow.Age, pitcherRow.LineupSlot ?? 9, pitcherRow.RotationSlot ?? 1);
    }

    private MatchPlayerSnapshot CreatePlayerSnapshot(Guid playerId, string name, string primaryPosition, string secondaryPosition, int age, int lineupSlot, int rotationSlot)
    {
        var ratings = _franchiseSession.GetPlayerRatings(playerId, name, primaryPosition, secondaryPosition, age);

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

    private bool ShouldRestPlayer(Guid playerId, string primaryPosition)
    {
        var health = _franchiseSession.GetPlayerHealth(playerId);
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
        var health = _franchiseSession.GetPlayerHealth(playerId);
        var injuryPenalty = health.InjuryDaysRemaining * 1000;
        var recoveryPenalty = health.DaysUntilAvailable * 100;
        var fatiguePenalty = health.Fatigue;
        if (primaryPosition is not ("SP" or "RP") && health.Fatigue >= 90)
        {
            fatiguePenalty += 120;
        }

        return injuryPenalty + recoveryPenalty + fatiguePenalty;
    }

    private MatchPlayerSnapshot CreatePlaceholderSnapshot(string name, int lineupSlot)
    {
        return CreatePlayerSnapshot(Guid.NewGuid(), name, lineupSlot % 2 == 0 ? "IF" : "OF", string.Empty, 27, lineupSlot, 0);
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

    private static TeamImportDto CreateFallbackTeam(string name, string abbreviation)
    {
        return new TeamImportDto
        {
            Name = name,
            Abbreviation = abbreviation,
            City = name,
            Division = "Demo",
            League = "Demo"
        };
    }

    private string BuildStatusText(MatchState state)
    {
        if (state.IsGameOver)
        {
            return "Game over - Esc: return to menus";
        }

        var workloadText = $"Pitch Ct {state.DefensiveTeam.PitchCount} | Arm {BuildPitcherFatigueText(state.CurrentPitcher, state.DefensiveTeam.PitchCount)}";
        return _isPaused
            ? $"Paused - Space: resume - Enter: step pitch - Esc: back - {workloadText}"
            : $"Space: pause - Enter: force next pitch - Esc: back - {workloadText}";
    }

    private static string BuildPitcherFatigueText(MatchPlayerSnapshot pitcher, int pitchCount)
    {
        var comfortLimit = 50 + (pitcher.StaminaRating / 2);
        if (pitchCount >= comfortLimit + 20)
        {
            return "Gassed";
        }

        if (pitchCount >= comfortLimit + 8)
        {
            return "Tiring";
        }

        if (pitchCount >= comfortLimit - 8)
        {
            return "Working";
        }

        return "Fresh";
    }
}
