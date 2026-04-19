using BaseballManager.Core.Drafts;

namespace BaseballManager.Application.Drafts;

public sealed class AdvanceDraftUseCase
{
    private readonly SimulateCpuDraftPickUseCase _simulateCpuDraftPickUseCase = new();

    public DraftAdvanceResult Execute(DraftState draftState, string userTeamName, Func<string, DraftCpuTeamContext> teamContextFactory)
    {
        if (draftState.IsComplete)
        {
            return new DraftAdvanceResult(false, true, null, "The draft is already complete.");
        }

        if (string.Equals(draftState.CurrentTeamName, userTeamName, StringComparison.OrdinalIgnoreCase))
        {
            return new DraftAdvanceResult(true, false, null, $"{userTeamName} is on the clock.");
        }

        var teamContext = teamContextFactory(draftState.CurrentTeamName);
        var pick = _simulateCpuDraftPickUseCase.Execute(draftState, teamContext);
        var message = $"{pick.TeamName} selected {pick.PlayerName} ({pick.PrimaryPosition}, {pick.OverallRating} OVR).";
        return new DraftAdvanceResult(false, draftState.IsComplete, pick, message);
    }
}
