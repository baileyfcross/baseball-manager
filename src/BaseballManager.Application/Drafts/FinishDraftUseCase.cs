using BaseballManager.Core.Drafts;

namespace BaseballManager.Application.Drafts;

public sealed class FinishDraftUseCase
{
    public void Execute(DraftState draftState)
    {
        if (!draftState.IsComplete)
        {
            throw new InvalidOperationException("Cannot finish the draft before all picks are completed.");
        }
    }
}
