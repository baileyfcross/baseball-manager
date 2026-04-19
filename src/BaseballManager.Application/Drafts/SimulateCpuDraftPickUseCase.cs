using BaseballManager.Core.Drafts;

namespace BaseballManager.Application.Drafts;

public sealed class SimulateCpuDraftPickUseCase
{
    private readonly DraftCpuPicker _cpuPicker = new();

    public DraftPick Execute(DraftState draftState, DraftCpuTeamContext teamContext)
    {
        var prospect = _cpuPicker.SelectProspect(draftState, teamContext);
        return draftState.MakePick(teamContext.TeamName, prospect.PlayerId, isUserPick: false);
    }
}
