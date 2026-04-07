namespace BaseballManager.Sim.AtBat;

public sealed class CountState
{
    public int Balls { get; set; }

    public int Strikes { get; set; }

    public string Display => $"{Balls}-{Strikes}";

    public void Reset()
    {
        Balls = 0;
        Strikes = 0;
    }
}
