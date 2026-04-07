using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Results;

namespace BaseballManager.Game.Data;

public sealed class LiveMatchSaveState
{
    public MatchTeamSaveState AwayTeam { get; set; } = new();

    public MatchTeamSaveState HomeTeam { get; set; } = new();

    public int InningNumber { get; set; } = 1;

    public bool IsTopHalf { get; set; } = true;

    public int Outs { get; set; }

    public int Balls { get; set; }

    public int Strikes { get; set; }

    public Guid? FirstBaseRunnerId { get; set; }

    public Guid? SecondBaseRunnerId { get; set; }

    public Guid? ThirdBaseRunnerId { get; set; }

    public float BallX { get; set; } = 0.5f;

    public float BallY { get; set; } = 0.42f;

    public bool BallVisible { get; set; } = true;

    public string BallLabel { get; set; } = string.Empty;

    public string HighlightedFielder { get; set; } = string.Empty;

    public ResultEvent LatestEvent { get; set; } = new()
    {
        Code = "READY",
        Description = "First pitch coming up."
    };

    public bool IsGameOver { get; set; }

    public int CompletedPlays { get; set; }

    public float SecondsUntilNextPitch { get; set; } = 0.85f;

    public float BallHighlightTimer { get; set; } = 1.2f;

    public bool IsPaused { get; set; }

    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MatchTeamSaveState
{
    public string Name { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public List<MatchPlayerSnapshot> Lineup { get; set; } = new();

    public MatchPlayerSnapshot? StartingPitcher { get; set; }

    public int BattingIndex { get; set; }

    public int Runs { get; set; }

    public int Hits { get; set; }
}

public sealed record LiveMatchRestoreState(
    MatchState MatchState,
    float SecondsUntilNextPitch,
    float BallHighlightTimer,
    bool IsPaused);
