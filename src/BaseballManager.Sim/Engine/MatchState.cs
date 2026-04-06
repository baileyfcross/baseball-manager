using BaseballManager.Sim.AtBat;
using BaseballManager.Sim.Baserunning;
using BaseballManager.Sim.Fielding;

namespace BaseballManager.Sim.Engine;

public sealed class MatchState
{
    public InningState Inning { get; } = new();
    public CountState Count { get; } = new();
    public BaserunnerState Baserunners { get; } = new();
    public FieldState Field { get; } = new();
}
