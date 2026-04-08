using BaseballManager.Sim.AtBat;
using BaseballManager.Sim.Tests.Support;
using Xunit;

namespace BaseballManager.Sim.Tests.AtBat;

public sealed class AtBatResolverTests
{
    [Fact]
    public void Resolve_Ball_IncrementsBallCount_WithoutEndingThePlateAppearance()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [0]));

        Assert.Equal("Ball", result.Code);
        Assert.False(result.EndsPlateAppearance);
        Assert.Equal(1, state.Count.Balls);
        Assert.Equal(0, state.Count.Strikes);
        Assert.Equal(0, state.Inning.Outs);
    }

    [Fact]
    public void Resolve_CalledStrike_IncrementsStrikeCount_WithoutEndingThePlateAppearance()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [20]));

        Assert.Equal("Strike", result.Code);
        Assert.False(result.EndsPlateAppearance);
        Assert.Equal(0, state.Count.Balls);
        Assert.Equal(1, state.Count.Strikes);
    }

    [Fact]
    public void Resolve_FoulWithFewerThanTwoStrikes_AddsExactlyOneStrike()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();
        state.Count.Strikes = 1;

        var result = resolver.Resolve(
            state,
            new SequenceRandom(nextValues: [60], nextDoubleValues: [0.25d]));

        Assert.Equal("Foul", result.Code);
        Assert.False(result.EndsPlateAppearance);
        Assert.Equal(2, state.Count.Strikes);
    }

    [Fact]
    public void Resolve_FoulWithTwoStrikes_DoesNotCreateStrikeThree()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();
        state.Count.Strikes = 2;

        var result = resolver.Resolve(
            state,
            new SequenceRandom(nextValues: [60], nextDoubleValues: [0.25d]));

        Assert.Equal("Foul", result.Code);
        Assert.False(result.EndsPlateAppearance);
        Assert.Equal(2, state.Count.Strikes);
        Assert.Equal(0, state.Inning.Outs);
    }

    [Fact]
    public void Resolve_FourthBall_ProducesWalk_ForcesRunnersAndResetsTheCount()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();
        var offense = state.OffensiveTeam;
        var batterId = offense.CurrentBatter.Id;

        state.Count.Balls = 3;
        state.Baserunners.FirstBaseRunnerId = offense.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = offense.Lineup[2].Id;
        state.Baserunners.ThirdBaseRunnerId = offense.Lineup[3].Id;

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [0]));

        Assert.True(result.EndsPlateAppearance);
        Assert.True(result.IsWalk);
        Assert.False(result.IsBallInPlay);
        Assert.Equal(1, result.RunsScored);
        Assert.Equal(1, state.AwayTeam.Runs);
        Assert.Equal(0, state.HomeTeam.Runs);
        Assert.Equal(0, state.Count.Balls);
        Assert.Equal(0, state.Count.Strikes);
        Assert.Equal(batterId, state.Baserunners.FirstBaseRunnerId);
        Assert.Equal(offense.Lineup[1].Id, state.Baserunners.SecondBaseRunnerId);
        Assert.Equal(offense.Lineup[2].Id, state.Baserunners.ThirdBaseRunnerId);
        Assert.Equal(1, offense.BattingIndex);
    }

    [Fact]
    public void Resolve_ThirdStrike_RecordsAnOut_ResetsTheCount_AndAdvancesTheBatter()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();
        var offense = state.OffensiveTeam;
        var nextBatterId = offense.Lineup[1].Id;

        state.Count.Balls = 1;
        state.Count.Strikes = 2;

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [20]));

        Assert.True(result.EndsPlateAppearance);
        Assert.True(result.IsStrikeout);
        Assert.False(result.IsBallInPlay);
        Assert.Equal("Strikeout", result.Code);
        Assert.Equal(1, result.OutsRecorded);
        Assert.Equal(1, state.Inning.Outs);
        Assert.Equal(0, state.Count.Balls);
        Assert.Equal(0, state.Count.Strikes);
        Assert.Equal(1, offense.BattingIndex);
        Assert.Equal(nextBatterId, state.CurrentBatter.Id);
    }

    [Fact]
    public void Resolve_ThirdOutInTopHalf_SwitchesToBottomHalf_AndClearsTheBases()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        state.Inning.Outs = 2;
        state.Count.Strikes = 2;
        state.Baserunners.FirstBaseRunnerId = state.AwayTeam.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = state.AwayTeam.Lineup[2].Id;

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [20]));

        Assert.True(result.EndsPlateAppearance);
        Assert.False(state.Inning.IsTopHalf);
        Assert.Equal(0, state.Inning.Outs);
        Assert.False(state.Baserunners.HasRunnerOnFirst);
        Assert.False(state.Baserunners.HasRunnerOnSecond);
        Assert.False(state.Baserunners.HasRunnerOnThird);
        Assert.Contains("Bottom 1", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ThirdOutInBottomHalf_AdvancesToTheNextInning()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        state.Inning.Number = 4;
        state.Inning.IsTopHalf = false;
        state.Inning.Outs = 2;
        state.Count.Strikes = 2;

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [20]));

        Assert.True(result.EndsPlateAppearance);
        Assert.True(state.Inning.IsTopHalf);
        Assert.Equal(5, state.Inning.Number);
        Assert.Equal(0, state.Inning.Outs);
        Assert.Contains("Top 5", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_CompletedPlateAppearance_WrapsTheLineupAfterTheNinthBatter()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        state.OffensiveTeam.BattingIndex = 8;
        state.Count.Strikes = 2;

        resolver.Resolve(state, new SequenceRandom(nextValues: [20]));

        Assert.Equal(0, state.OffensiveTeam.BattingIndex);
        Assert.Equal(state.OffensiveTeam.Lineup[0].Id, state.CurrentBatter.Id);
    }

    [Fact]
    public void Resolve_HomeRunInBottomHalf_CreditsRunsToTheHomeTeam()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        state.Inning.IsTopHalf = false;
        state.Baserunners.FirstBaseRunnerId = state.HomeTeam.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = state.HomeTeam.Lineup[2].Id;

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [99, 99, 0, 0]));

        Assert.Equal("HomeRun", result.Code);
        Assert.True(result.IsBallInPlay);
        Assert.Equal(3, result.RunsScored);
        Assert.Equal(3, state.HomeTeam.Runs);
        Assert.Equal(0, state.AwayTeam.Runs);
        Assert.False(state.Baserunners.HasRunnerOnFirst);
        Assert.False(state.Baserunners.HasRunnerOnSecond);
        Assert.False(state.Baserunners.HasRunnerOnThird);
    }

    [Fact]
    public void Resolve_BasesLoadedWalkInBottomNinth_SetsGameOverForAWalkOffWin()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        state.Inning.Number = 9;
        state.Inning.IsTopHalf = false;
        state.HomeTeam.Runs = 2;
        state.AwayTeam.Runs = 2;
        state.Count.Balls = 3;
        state.Baserunners.FirstBaseRunnerId = state.HomeTeam.Lineup[1].Id;
        state.Baserunners.SecondBaseRunnerId = state.HomeTeam.Lineup[2].Id;
        state.Baserunners.ThirdBaseRunnerId = state.HomeTeam.Lineup[3].Id;

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [0]));

        Assert.True(state.IsGameOver);
        Assert.True(result.IsGameOver);
        Assert.True(result.IsWalk);
        Assert.False(result.IsBallInPlay);
        Assert.Equal(3, state.HomeTeam.Runs);
        Assert.Contains("walk it off", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_ThirdOutInTopNinthWithHomeLead_EndsTheGame()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        state.Inning.Number = 9;
        state.Inning.IsTopHalf = true;
        state.Inning.Outs = 2;
        state.Count.Strikes = 2;
        state.HomeTeam.Runs = 4;
        state.AwayTeam.Runs = 2;

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [20]));

        Assert.True(state.IsGameOver);
        Assert.True(result.IsGameOver);
        Assert.False(state.Inning.IsTopHalf);
        Assert.Contains("win 4-2", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_ThirdOutInBottomNinthWhenTied_AdvancesToTopTenth()
    {
        var state = MatchStateFactory.CreateDefault();
        var resolver = new AtBatResolver();

        state.Inning.Number = 9;
        state.Inning.IsTopHalf = false;
        state.Inning.Outs = 2;
        state.Count.Strikes = 2;
        state.HomeTeam.Runs = 3;
        state.AwayTeam.Runs = 3;

        var result = resolver.Resolve(state, new SequenceRandom(nextValues: [20]));

        Assert.False(state.IsGameOver);
        Assert.True(state.Inning.IsTopHalf);
        Assert.Equal(10, state.Inning.Number);
        Assert.Contains("Top 10", result.Description, StringComparison.Ordinal);
    }
}
