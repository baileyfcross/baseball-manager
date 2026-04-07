using BaseballManager.Sim.AtBat;
using BaseballManager.Sim.Results;

namespace BaseballManager.Sim.Engine;

public sealed class MatchEngine
{
    private readonly AtBatResolver _atBatResolver = new();
    private readonly Random _random;

    public MatchEngine()
        : this(
            MatchTeamState.CreatePlaceholder("Visitors", "VIS"),
            MatchTeamState.CreatePlaceholder("Home", "HOME"))
    {
    }

    public MatchEngine(MatchTeamState awayTeam, MatchTeamState homeTeam, Random? random = null)
    {
        CurrentState = new MatchState(awayTeam, homeTeam);
        _random = random ?? new Random();
        CurrentState.Field.ResetToPitcher();
    }

    public MatchState CurrentState { get; }

    public ResultEvent Tick()
    {
        if (CurrentState.IsGameOver)
        {
            return CurrentState.LatestEvent;
        }

        var result = _atBatResolver.Resolve(CurrentState, _random);
        CurrentState.LatestEvent = result;
        return result;
    }
}
