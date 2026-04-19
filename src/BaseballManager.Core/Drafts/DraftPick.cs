namespace BaseballManager.Core.Drafts;

public sealed record DraftPick(
    int RoundNumber,
    int PickNumberInRound,
    int OverallPickNumber,
    string TeamName,
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    int OverallRating,
    bool IsUserPick);
