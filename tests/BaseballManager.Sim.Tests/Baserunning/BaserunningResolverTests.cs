using BaseballManager.Sim.AtBat;
using BaseballManager.Sim.Baserunning;
using BaseballManager.Sim.Tests.Support;
using Xunit;

namespace BaseballManager.Sim.Tests.Baserunning;

public sealed class BaserunningResolverTests
{
    [Fact]
    public void ResolveAdvance_SingleScoresTheRunnerFromThird_AndKeepsOtherRunnersInOrder()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;
        var batterId = offense.Lineup[3].Id;

        state.Baserunners.FirstBaseRunnerId = offense.Lineup[1].Id;
        state.Baserunners.ThirdBaseRunnerId = offense.Lineup[2].Id;

        var result = resolver.ResolveAdvance(state, CreateOutcome("Single", batterId, basesAwarded: 1), new SequenceRandom(nextDoubleValues: [0.99d]));

        Assert.Equal(1, result.RunsScored);
        Assert.Equal(batterId, state.Baserunners.FirstBaseRunnerId);
        Assert.Equal(offense.Lineup[1].Id, state.Baserunners.SecondBaseRunnerId);
        Assert.Null(state.Baserunners.ThirdBaseRunnerId);
    }

    [Fact]
    public void ResolveAdvance_DoubleScoresTheRunnerFromSecond_AndPutsTheLeadRunnerOnThird()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;
        var batterId = offense.Lineup[3].Id;

        state.Baserunners.FirstBaseRunnerId = offense.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = offense.Lineup[2].Id;

        var result = resolver.ResolveAdvance(state, CreateOutcome("Double", batterId, basesAwarded: 2), new SequenceRandom(nextDoubleValues: [0.99d]));

        Assert.Equal(1, result.RunsScored);
        Assert.Null(state.Baserunners.FirstBaseRunnerId);
        Assert.Equal(batterId, state.Baserunners.SecondBaseRunnerId);
        Assert.Equal(offense.Lineup[1].Id, state.Baserunners.ThirdBaseRunnerId);
    }

    [Fact]
    public void ResolveAdvance_TripleClearsTheBases_AndLeavesTheBatterOnThird()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;
        var batterId = offense.Lineup[4].Id;

        state.Baserunners.FirstBaseRunnerId = offense.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = offense.Lineup[2].Id;
        state.Baserunners.ThirdBaseRunnerId = offense.Lineup[3].Id;

        var result = resolver.ResolveAdvance(state, CreateOutcome("Triple", batterId, basesAwarded: 3), new SequenceRandom());

        Assert.Equal(3, result.RunsScored);
        Assert.Null(state.Baserunners.FirstBaseRunnerId);
        Assert.Null(state.Baserunners.SecondBaseRunnerId);
        Assert.Equal(batterId, state.Baserunners.ThirdBaseRunnerId);
    }

    [Fact]
    public void ResolveAdvance_HomeRunScoresEverybody_AndClearsTheBases()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;
        var batterId = offense.Lineup[4].Id;

        state.Baserunners.FirstBaseRunnerId = offense.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = offense.Lineup[2].Id;

        var result = resolver.ResolveAdvance(state, CreateOutcome("HomeRun", batterId, basesAwarded: 4), new SequenceRandom());

        Assert.Equal(3, result.RunsScored);
        Assert.False(state.Baserunners.HasRunnerOnFirst);
        Assert.False(state.Baserunners.HasRunnerOnSecond);
        Assert.False(state.Baserunners.HasRunnerOnThird);
    }

    [Fact]
    public void ResolveAdvance_GroundoutWithLessThanTwoOuts_MovesForcedRunnersUp()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;

        state.Baserunners.FirstBaseRunnerId = offense.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = offense.Lineup[2].Id;

        var result = resolver.ResolveAdvance(
            state,
            CreateOutcome("Groundout", offense.CurrentBatter.Id, basesAwarded: 0, isOut: true, countsAsHit: false, outsBeforePlay: 1, fielder: "2B"),
            new SequenceRandom());

        Assert.Equal(0, result.RunsScored);
        Assert.Null(state.Baserunners.FirstBaseRunnerId);
        Assert.Equal(offense.Lineup[1].Id, state.Baserunners.SecondBaseRunnerId);
        Assert.Equal(offense.Lineup[2].Id, state.Baserunners.ThirdBaseRunnerId);
    }

    [Fact]
    public void ResolveAdvance_FlyoutWithRunnerOnThird_CanScoreATagUpRun()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;

        state.Baserunners.ThirdBaseRunnerId = offense.Lineup[2].Id;

        var result = resolver.ResolveAdvance(
            state,
            CreateOutcome("Flyout", offense.CurrentBatter.Id, basesAwarded: 0, isOut: true, countsAsHit: false, outsBeforePlay: 1, fielder: "LF"),
            new SequenceRandom(nextDoubleValues: [0.0d]));

        Assert.Equal(1, result.RunsScored);
        Assert.Null(state.Baserunners.ThirdBaseRunnerId);
        Assert.Contains("scores", result.AdditionalDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveAdvance_WalkWithoutRunnerOnFirst_DoesNotForceExistingRunners()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;
        var batterId = offense.CurrentBatter.Id;

        state.Baserunners.SecondBaseRunnerId = offense.Lineup[2].Id;
        state.Baserunners.ThirdBaseRunnerId = offense.Lineup[3].Id;

        var result = resolver.ResolveAdvance(state, CreateOutcome("Walk", batterId, basesAwarded: 1, isWalk: true, countsAsHit: false), new SequenceRandom());

        Assert.Equal(0, result.RunsScored);
        Assert.Equal(batterId, state.Baserunners.FirstBaseRunnerId);
        Assert.Equal(offense.Lineup[2].Id, state.Baserunners.SecondBaseRunnerId);
        Assert.Equal(offense.Lineup[3].Id, state.Baserunners.ThirdBaseRunnerId);
    }

    [Fact]
    public void ResolveAdvance_GroundoutWithTwoOuts_DoesNotAdvanceRunners()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;

        state.Baserunners.FirstBaseRunnerId = offense.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = offense.Lineup[2].Id;

        var result = resolver.ResolveAdvance(
            state,
            CreateOutcome("Groundout", offense.CurrentBatter.Id, basesAwarded: 0, isOut: true, countsAsHit: false, outsBeforePlay: 2, fielder: "SS"),
            new SequenceRandom());

        Assert.Equal(0, result.RunsScored);
        Assert.Equal(offense.Lineup[1].Id, state.Baserunners.FirstBaseRunnerId);
        Assert.Equal(offense.Lineup[2].Id, state.Baserunners.SecondBaseRunnerId);
        Assert.Null(state.Baserunners.ThirdBaseRunnerId);
    }

    [Fact]
    public void ResolveAdvance_FlyoutWithTwoOuts_DoesNotAllowTagUpRun()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new BaserunningResolver();
        var offense = state.OffensiveTeam;

        state.Baserunners.ThirdBaseRunnerId = offense.Lineup[2].Id;

        var result = resolver.ResolveAdvance(
            state,
            CreateOutcome("Flyout", offense.CurrentBatter.Id, basesAwarded: 0, isOut: true, countsAsHit: false, outsBeforePlay: 2, fielder: "CF"),
            new SequenceRandom(nextDoubleValues: [0.0d]));

        Assert.Equal(0, result.RunsScored);
        Assert.Equal(offense.Lineup[2].Id, state.Baserunners.ThirdBaseRunnerId);
    }

    private static PlateAppearanceOutcome CreateOutcome(
        string code,
        Guid batterId,
        int basesAwarded,
        bool isOut = false,
        bool isWalk = false,
        bool countsAsHit = true,
        int outsBeforePlay = 0,
        string fielder = "CF")
    {
        return new PlateAppearanceOutcome(
            Code: code,
            Description: code,
            IsOut: isOut,
            IsWalk: isWalk,
            BasesAwarded: basesAwarded,
            CountsAsHit: countsAsHit,
            BallX: 0.5f,
            BallY: 0.5f,
            Fielder: fielder,
            BallLabel: code,
            OutsBeforePlay: outsBeforePlay,
            BatterId: batterId);
    }
}
