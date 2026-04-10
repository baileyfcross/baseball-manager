using BaseballManager.Sim.AtBat;
using BaseballManager.Sim.Baserunning;
using BaseballManager.Sim.Fielding;
using BaseballManager.Sim.Results;

namespace BaseballManager.Sim.Engine;

public sealed class MatchState
{
    public MatchState()
        : this(
            MatchTeamState.CreatePlaceholder("Visitors", "VIS"),
            MatchTeamState.CreatePlaceholder("Home", "HOME"))
    {
    }

    public MatchState(MatchTeamState awayTeam, MatchTeamState homeTeam)
    {
        AwayTeam = awayTeam;
        HomeTeam = homeTeam;
        EnsureInningTracked(1);
        LatestEvent = new ResultEvent
        {
            Code = "READY",
            Description = $"{AwayTeam.Name} at {HomeTeam.Name}. First pitch coming up."
        };
    }

    public MatchTeamState AwayTeam { get; }

    public MatchTeamState HomeTeam { get; }

    public InningState Inning { get; } = new();

    public CountState Count { get; } = new();

    public BaserunnerState Baserunners { get; } = new();

    public FieldState Field { get; } = new();

    public List<int> AwayRunsByInning { get; } = [];

    public List<int> HomeRunsByInning { get; } = [];

    public int AwayErrors { get; set; }

    public int HomeErrors { get; set; }

    public ResultEvent LatestEvent { get; set; }

    public bool IsGameOver { get; set; }

    public int CompletedPlays { get; set; }

    public MatchTeamState OffensiveTeam => Inning.IsTopHalf ? AwayTeam : HomeTeam;

    public MatchTeamState DefensiveTeam => Inning.IsTopHalf ? HomeTeam : AwayTeam;

    public MatchPlayerSnapshot CurrentBatter => OffensiveTeam.CurrentBatter;

    public MatchPlayerSnapshot CurrentPitcher => DefensiveTeam.CurrentPitcher;

    public MatchPlayerSnapshot? GetPlayer(Guid? playerId)
    {
        if (!playerId.HasValue)
        {
            return null;
        }

        return AwayTeam.FindPlayer(playerId.Value)
               ?? HomeTeam.FindPlayer(playerId.Value);
    }

    public string GetRunnerName(Guid? runnerId)
    {
        return GetPlayer(runnerId)?.FullName ?? string.Empty;
    }

    public void RecordRunsForCurrentHalf(int runsScored)
    {
        if (runsScored <= 0)
        {
            return;
        }

        EnsureInningTracked(Inning.Number);
        var targetLine = Inning.IsTopHalf ? AwayRunsByInning : HomeRunsByInning;
        targetLine[Inning.Number - 1] += runsScored;
    }

    public void EnsureInningTracked(int inningNumber)
    {
        var targetInning = Math.Max(1, inningNumber);
        while (AwayRunsByInning.Count < targetInning)
        {
            AwayRunsByInning.Add(0);
        }

        while (HomeRunsByInning.Count < targetInning)
        {
            HomeRunsByInning.Add(0);
        }
    }
}
