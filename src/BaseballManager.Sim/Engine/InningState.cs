namespace BaseballManager.Sim.Engine;

public sealed class InningState
{
    public int Number { get; set; } = 1;

    public bool IsTopHalf { get; set; } = true;

    public int Outs { get; set; }

    public string HalfLabel => IsTopHalf ? "Top" : "Bottom";
}
