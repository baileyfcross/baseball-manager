using BaseballManager.Core.Drafts;

namespace BaseballManager.Application.Drafts;

public sealed class StartDraftUseCase
{
    public DraftState Execute(IReadOnlyList<string> draftOrder, IReadOnlyList<DraftProspect> prospects, int totalRounds, bool isSnakeDraft = false)
    {
        return DraftState.Create(draftOrder, prospects, totalRounds, isSnakeDraft);
    }
}
