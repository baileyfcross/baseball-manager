using BaseballManager.Sim.AI;
using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Tests.Support;
using Xunit;

namespace BaseballManager.Sim.Tests.AI;

public sealed class ManagerAiTests
{
    private readonly ManagerAi _managerAi = new();

    [Fact]
    public void BuildTeamPlan_prefers_available_hitters_for_starting_lineup()
    {
        var unavailableStar = MatchStateFactory.CreatePlayer("Unavailable Star", "OF", overallRating: 96, contactRating: 92, powerRating: 90, disciplineRating: 85, speedRating: 80);
        var candidates = new List<ManagerAiRosterCandidate>
        {
            new(MatchStateFactory.CreatePlayer("Catcher", "C", overallRating: 70, fieldingRating: 72), 0, true),
            new(MatchStateFactory.CreatePlayer("First", "1B", overallRating: 68, powerRating: 72), 0, true),
            new(MatchStateFactory.CreatePlayer("Second", "2B", overallRating: 67, contactRating: 70, speedRating: 68), 0, true),
            new(MatchStateFactory.CreatePlayer("Third", "3B", overallRating: 69, powerRating: 70), 0, true),
            new(MatchStateFactory.CreatePlayer("Short", "SS", overallRating: 71, fieldingRating: 74, speedRating: 73), 0, true),
            new(MatchStateFactory.CreatePlayer("Left", "LF", overallRating: 66), 0, true),
            new(MatchStateFactory.CreatePlayer("Center", "CF", overallRating: 72, speedRating: 78), 0, true),
            new(MatchStateFactory.CreatePlayer("Right", "RF", overallRating: 67, powerRating: 68), 0, true),
            new(MatchStateFactory.CreatePlayer("Utility", "IF", overallRating: 65), 0, true),
            new(unavailableStar, 500, false)
        };

        var plan = _managerAi.BuildTeamPlan(candidates);

        Assert.Equal(9, plan.Lineup.Count);
        Assert.DoesNotContain(plan.Lineup, player => player.Id == unavailableStar.Id);
        Assert.Contains(plan.Lineup, player => player.PrimaryPosition == "C");
    }

    [Fact]
    public void BuildTeamPlan_selects_best_available_starting_pitcher()
    {
        var ace = MatchStateFactory.CreatePlayer("Ace", "SP", pitchingRating: 88, staminaRating: 84, overallRating: 86);
        var unavailableAce = MatchStateFactory.CreatePlayer("Unavailable Ace", "SP", pitchingRating: 95, staminaRating: 90, overallRating: 93);
        var reliever = MatchStateFactory.CreatePlayer("Late Arm", "RP", pitchingRating: 90, staminaRating: 58, overallRating: 84);

        var plan = _managerAi.BuildTeamPlan(
        [
            new(ace, 0, true, RotationSlot: 2),
            new(unavailableAce, 900, false, RotationSlot: 1),
            new(reliever, 0, true),
            new(MatchStateFactory.CreatePlayer("Catcher", "C", overallRating: 60), 0, true),
            new(MatchStateFactory.CreatePlayer("First", "1B", overallRating: 60), 0, true),
            new(MatchStateFactory.CreatePlayer("Second", "2B", overallRating: 60), 0, true),
            new(MatchStateFactory.CreatePlayer("Third", "3B", overallRating: 60), 0, true),
            new(MatchStateFactory.CreatePlayer("Short", "SS", overallRating: 60), 0, true),
            new(MatchStateFactory.CreatePlayer("Left", "LF", overallRating: 60), 0, true),
            new(MatchStateFactory.CreatePlayer("Center", "CF", overallRating: 60), 0, true),
            new(MatchStateFactory.CreatePlayer("Right", "RF", overallRating: 60), 0, true),
            new(MatchStateFactory.CreatePlayer("Utility", "IF", overallRating: 60), 0, true)
        ]);

        Assert.NotNull(plan.StartingPitcher);
        Assert.Equal(ace.Id, plan.StartingPitcher!.Id);
        Assert.DoesNotContain(plan.Bullpen, player => player.Id == ace.Id);
    }

    [Fact]
    public void TryApplyDefensiveManagerDecision_replaces_overworked_pitcher()
    {
        var starter = MatchStateFactory.CreatePlayer("Starter", "SP", pitchingRating: 72, staminaRating: 74, overallRating: 73);
        var reliever = MatchStateFactory.CreatePlayer("Reliever", "RP", pitchingRating: 82, staminaRating: 60, overallRating: 80);
        var bullpenDepth = MatchStateFactory.CreatePlayer("Depth", "RP", pitchingRating: 68, staminaRating: 58, overallRating: 66);
        var away = MatchStateFactory.CreateTeam(
            "Away",
            "AWY",
            startingPitcher: starter,
            bullpenPlayers: [reliever, bullpenDepth],
            pitchCountsByPitcher: new Dictionary<Guid, int> { [starter.Id] = 104 });
        away.PitchCount = 104;
        var home = MatchStateFactory.CreateTeam("Home", "HME");
        var state = new MatchState(away, home);
        state.Inning.Number = 7;
        state.Inning.IsTopHalf = false;
        state.HomeTeam.Runs = 3;
        state.AwayTeam.Runs = 2;

        var changed = _managerAi.TryApplyDefensiveManagerDecision(state);

        Assert.True(changed);
        Assert.Equal(reliever.Id, away.CurrentPitcher.Id);
        Assert.Equal("SUB", state.LatestEvent.Code);
        Assert.Contains("bullpen", state.LatestEvent.Description, StringComparison.OrdinalIgnoreCase);
    }
}
