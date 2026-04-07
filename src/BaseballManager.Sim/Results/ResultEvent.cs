namespace BaseballManager.Sim.Results;

public sealed class ResultEvent
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool EndsPlateAppearance { get; init; }

    public bool IsBallInPlay { get; init; }

    public bool CountsAsAtBat { get; init; }

    public bool CountsAsHit { get; init; }

    public bool IsWalk { get; init; }

    public bool IsStrikeout { get; init; }

    public int BasesAwarded { get; init; }

    public int RunsScored { get; init; }

    public int OutsRecorded { get; init; }

    public Guid BatterId { get; init; }

    public Guid PitcherId { get; init; }

    public float BallX { get; init; } = 0.5f;

    public float BallY { get; init; } = 0.42f;

    public string Fielder { get; init; } = string.Empty;

    public bool IsGameOver { get; init; }
}
