using BaseballManager.Core.Drafts;

namespace BaseballManager.Application.Drafts;

public sealed class MakeUserDraftPickUseCase
{
    public DraftPick Execute(DraftState draftState, string userTeamName, Guid playerId)
    {
        return draftState.MakePick(userTeamName, playerId, isUserPick: true);
    }
}
