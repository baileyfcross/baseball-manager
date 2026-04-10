namespace BaseballManager.Game.Screens.LiveMatch;

public sealed class LiveMatchViewModel
{
    public string AwayTeamName { get; init; } = "Visitors";

    public string HomeTeamName { get; init; } = "Home";

    public string AwayAbbreviation { get; init; } = "VIS";

    public string HomeAbbreviation { get; init; } = "HOME";

    public int AwayScore { get; init; }

    public int HomeScore { get; init; }

    public int AwayHits { get; init; }

    public int HomeHits { get; init; }

    public int AwayErrors { get; init; }

    public int HomeErrors { get; init; }

    public IReadOnlyList<int> AwayRunsByInning { get; init; } = [];

    public IReadOnlyList<int> HomeRunsByInning { get; init; } = [];

    public int InningNumber { get; init; } = 1;

    public bool IsTopHalf { get; init; } = true;

    public int Balls { get; init; }

    public int Strikes { get; init; }

    public int Outs { get; init; }

    public bool RunnerOnFirst { get; init; }

    public bool RunnerOnSecond { get; init; }

    public bool RunnerOnThird { get; init; }

    public string RunnerOnFirstName { get; init; } = string.Empty;

    public string RunnerOnSecondName { get; init; } = string.Empty;

    public string RunnerOnThirdName { get; init; } = string.Empty;

    public string BatterName { get; init; } = string.Empty;

    public string PitcherName { get; init; } = string.Empty;

    public int PitchCount { get; init; }

    public string PitcherFatigueText { get; init; } = "Fresh";

    public string LatestPlayText { get; init; } = "First pitch coming up.";

    public string StatusText { get; init; } = "Live";

    public bool IsPaused { get; init; }

    public bool IsGameOver { get; init; }

    public float BallXNormalized { get; init; } = 0.5f;

    public float BallYNormalized { get; init; } = 0.42f;

    public bool BallVisible { get; init; } = true;

    public string BallLabel { get; init; } = string.Empty;

    public string HighlightedFielder { get; init; } = string.Empty;

    public float BallHighlightAlpha { get; init; } = 1f;

    public bool ManagerMenuVisible { get; init; }

    public string ManagerModeLabel { get; init; } = string.Empty;

    public string ManagerPromptText { get; init; } = string.Empty;

    public string ManagerTargetLabel { get; init; } = string.Empty;

    public IReadOnlyList<string> ManagerOptions { get; init; } = [];

    public int ManagerSelectionIndex { get; init; }

    public string ManagerFeedbackText { get; init; } = string.Empty;
}
