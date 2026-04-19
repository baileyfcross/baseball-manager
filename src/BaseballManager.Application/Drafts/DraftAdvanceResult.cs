using BaseballManager.Core.Drafts;

namespace BaseballManager.Application.Drafts;

public sealed record DraftAdvanceResult(
    bool WaitingForUserPick,
    bool DraftCompleted,
    DraftPick? PickMade,
    string Message);
