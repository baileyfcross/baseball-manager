namespace BaseballManager.Sim.Fielding;

public sealed class FieldState
{
    public string StadiumId { get; set; } = string.Empty;

    public float BallX { get; set; } = 0.5f;

    public float BallY { get; set; } = 0.42f;

    public bool BallVisible { get; set; } = true;

    public string BallLabel { get; set; } = "Pitcher";

    public string HighlightedFielder { get; set; } = string.Empty;

    public void ResetToPitcher()
    {
        BallX = 0.5f;
        BallY = 0.42f;
        BallVisible = true;
        BallLabel = "Pitcher";
        HighlightedFielder = "P";
    }
}
