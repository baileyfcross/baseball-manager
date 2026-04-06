namespace BaseballManager.Sim.Engine;

public sealed class MatchEngine
{
    public MatchState CurrentState { get; } = new();

    public void Tick()
    {
    }
}
