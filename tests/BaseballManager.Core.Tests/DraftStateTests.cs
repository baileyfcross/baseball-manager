using BaseballManager.Application.Drafts;
using BaseballManager.Core.Drafts;
using Xunit;

namespace BaseballManager.Core.Tests;

public sealed class DraftStateTests
{
    [Fact]
    public void Create_InitializesDraftWithRoundOneAndFirstTeamOnClock()
    {
        var draftState = DraftState.Create(BuildDraftOrder(), BuildProspects(), totalRounds: 3);

        Assert.False(draftState.IsComplete);
        Assert.Equal(1, draftState.CurrentRound);
        Assert.Equal(1, draftState.CurrentPickNumber);
        Assert.Equal("Owls", draftState.CurrentTeamName);
        Assert.Equal(6, draftState.AvailableProspects.Count);
    }

    [Fact]
    public void MakePick_AdvancesToNextTeamAndRemovesPlayerFromPool()
    {
        var prospects = BuildProspects();
        var draftState = DraftState.Create(BuildDraftOrder(), prospects, totalRounds: 2);

        var pick = draftState.MakePick("Owls", prospects[0].PlayerId, isUserPick: true);

        Assert.Equal("Owls", pick.TeamName);
        Assert.Equal(prospects[0].PlayerId, pick.PlayerId);
        Assert.Equal("Bears", draftState.CurrentTeamName);
        Assert.DoesNotContain(draftState.AvailableProspects, prospect => prospect.PlayerId == prospects[0].PlayerId);
    }

    [Fact]
    public void MakePick_PreventsDuplicateDrafting()
    {
        var prospects = BuildProspects();
        var draftState = DraftState.Create(BuildDraftOrder(), prospects, totalRounds: 2);

        draftState.MakePick("Owls", prospects[0].PlayerId, isUserPick: true);

        var exception = Assert.Throws<InvalidOperationException>(() => draftState.MakePick("Bears", prospects[0].PlayerId, isUserPick: false));
        Assert.Equal("That prospect is no longer available.", exception.Message);
    }

    [Fact]
    public void SnakeDraft_ReversesOrderInEvenRounds()
    {
        var draftState = DraftState.Create(BuildDraftOrder(), BuildProspects(), totalRounds: 2, isSnakeDraft: true);

        draftState.MakePick("Owls", draftState.AvailableProspects[0].PlayerId, isUserPick: true);
        draftState.MakePick("Bears", draftState.AvailableProspects[0].PlayerId, isUserPick: false);
        draftState.MakePick("Foxes", draftState.AvailableProspects[0].PlayerId, isUserPick: false);

        Assert.Equal(2, draftState.CurrentRound);
        Assert.Equal(1, draftState.CurrentPickNumber);
        Assert.Equal("Foxes", draftState.CurrentTeamName);
    }

    [Fact]
    public void CpuPicker_PrioritizesHighestValueProspectWithLightNeedBias()
    {
        var draftState = DraftState.Create(
            BuildDraftOrder(),
            [
                new DraftProspect(Guid.NewGuid(), "Starter Arm", "SP", "RP", 21, 67, 81, "College", "Pitching prospect", "Scout likes the arm.", "Impact regular upside", "Coastal State", "Coastal State: 9-2, 2.41 ERA, 112 K in 96.0 IP", "Normal"),
                new DraftProspect(Guid.NewGuid(), "Power Bat", "1B", string.Empty, 20, 68, 72, "College", "Bat-first prospect", "Scout sees middle-order thump.", "Solid big-league ceiling", "Metro A&M", "Metro A&M: .318 AVG, 15 HR, 2 SB in 53 G", "Normal"),
                new DraftProspect(Guid.NewGuid(), "Center Fielder", "CF", "LF", 19, 66, 83, "High School", "Athletic outfielder", "Scout likes the speed-and-defense package.", "Impact regular upside", "Briarwood Prep", "Briarwood Prep: .341 AVG, 8 HR, 21 SB in 33 G", "Gem")
            ],
            totalRounds: 1);

        var picker = new DraftCpuPicker();
        var teamContext = new DraftCpuTeamContext("Owls", ["C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "RP"]);

        var selected = picker.SelectProspect(draftState, teamContext);

        Assert.Equal("Power Bat", selected.PlayerName);
    }

    [Fact]
    public void AdvanceDraftUseCase_MakesCpuPickWhenUserIsNotOnClock()
    {
        var prospects = BuildProspects();
        var draftState = DraftState.Create(BuildDraftOrder(), prospects, totalRounds: 1);
        draftState.MakePick("Owls", prospects[0].PlayerId, isUserPick: true);

        var useCase = new AdvanceDraftUseCase();
        var result = useCase.Execute(draftState, "Owls", teamName => new DraftCpuTeamContext(teamName, ["C", "SS"]));

        Assert.False(result.WaitingForUserPick);
        Assert.NotNull(result.PickMade);
        Assert.Equal("Foxes", draftState.CurrentTeamName);
        Assert.Single(draftState.DraftedPicks.Where(pick => !pick.IsUserPick));
    }

    [Fact]
    public void DraftCompletesAfterFinalPick()
    {
        var prospects = BuildProspects();
        var draftState = DraftState.Create(BuildDraftOrder(), prospects, totalRounds: 2);

        while (!draftState.IsComplete)
        {
            var playerId = draftState.AvailableProspects[0].PlayerId;
            draftState.MakePick(draftState.CurrentTeamName, playerId, isUserPick: false);
        }

        Assert.True(draftState.IsComplete);
        Assert.Equal(6, draftState.DraftedPicks.Count);
        Assert.Empty(draftState.AvailableProspects);
    }

    [Fact]
    public void ProspectFactory_GeneratesScoutReportsAndSourceStats()
    {
        var factory = new DraftProspectFactory();

        var prospects = factory.CreateProspects(2028, 12);

        Assert.Equal(12, prospects.Count);
        Assert.All(prospects, prospect =>
        {
            Assert.False(string.IsNullOrWhiteSpace(prospect.ScoutSummary));
            Assert.False(string.IsNullOrWhiteSpace(prospect.PotentialSummary));
            Assert.False(string.IsNullOrWhiteSpace(prospect.SourceTeamName));
            Assert.False(string.IsNullOrWhiteSpace(prospect.SourceStatsSummary));
            Assert.Contains(prospect.SourceTeamName, prospect.SourceStatsSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(prospect.TalentOutcome, new[] { "Bust", "Normal", "Gem" });
        });
    }

    private static IReadOnlyList<string> BuildDraftOrder()
    {
        return ["Owls", "Bears", "Foxes"];
    }

    private static IReadOnlyList<DraftProspect> BuildProspects()
    {
        return
        [
            new DraftProspect(Guid.NewGuid(), "Player One", "SS", "2B", 19, 65, 79, "High School", "Middle infielder", "Scout likes the feet and actions.", "Impact regular upside", "East Harbor HS", "East Harbor HS: .327 AVG, 5 HR, 18 SB in 30 G", "Normal"),
            new DraftProspect(Guid.NewGuid(), "Player Two", "SP", "RP", 21, 66, 76, "College", "Starter", "Scout sees a durable starter frame.", "Solid big-league ceiling", "Coastal State", "Coastal State: 8-3, 3.08 ERA, 101 K in 92.0 IP", "Normal"),
            new DraftProspect(Guid.NewGuid(), "Player Three", "CF", "LF", 18, 63, 80, "High School", "Outfielder", "Scout sees twitchy athleticism.", "Impact regular upside", "North Creek HS", "North Creek HS: .334 AVG, 7 HR, 24 SB in 31 G", "Gem"),
            new DraftProspect(Guid.NewGuid(), "Player Four", "1B", string.Empty, 22, 62, 71, "College", "Corner bat", "Scout sees usable power with limited defensive value.", "Solid big-league ceiling", "Western Tech", "Western Tech: .301 AVG, 14 HR, 1 SB in 49 G", "Normal"),
            new DraftProspect(Guid.NewGuid(), "Player Five", "RP", "SP", 20, 61, 74, "International", "Bullpen arm", "Scout sees bat-missing stuff but uneven command.", "Solid big-league ceiling", "Tokyo Blue Stars", "Tokyo Blue Stars: 2-1, 2.87 ERA, 67 K in 48.0 IP", "Normal"),
            new DraftProspect(Guid.NewGuid(), "Player Six", "C", "1B", 19, 60, 75, "High School", "Catcher", "Scout likes the makeup and receiving base.", "Solid big-league ceiling", "St. Brendan's", "St. Brendan's: .289 AVG, 6 HR, 0 SB in 28 G", "Bust")
        ];
    }
}
