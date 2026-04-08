using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Results;
using BaseballManager.Sim.Tests.Support;
using Xunit;

namespace BaseballManager.Sim.Tests.Engine;

public sealed class MatchEngineTests
{
    [Fact]
    public void Tick_WhenGameIsAlreadyOver_ReturnsTheLatestEventWithoutMutatingState()
    {
        var state = MatchStateFactory.CreateDefault();
        state.IsGameOver = true;
        state.CompletedPlays = 7;
        state.LatestEvent = new ResultEvent
        {
            Code = "Final",
            Description = "Already complete.",
            IsGameOver = true
        };

        var engine = new MatchEngine(state, new SequenceRandom(nextValues: [0]));

        var result = engine.Tick();

        Assert.Same(state.LatestEvent, result);
        Assert.Equal("Final", result.Code);
        Assert.Equal(7, state.CompletedPlays);
    }

    [Fact]
    public void Tick_CompletedPlateAppearance_StoresTheReturnedEventOnCurrentState()
    {
        var state = MatchStateFactory.CreateDefault();
        state.Count.Strikes = 2;
        var engine = new MatchEngine(state, new SequenceRandom(nextValues: [20]));

        var result = engine.Tick();

        Assert.Same(engine.CurrentState.LatestEvent, result);
        Assert.True(result.EndsPlateAppearance);
        Assert.True(result.IsStrikeout);
        Assert.Equal(1, engine.CurrentState.CompletedPlays);
    }

    [Fact]
    public void Tick_MultiplePitches_PreserveTheCountUntilThePlateAppearanceEnds()
    {
        var state = MatchStateFactory.CreateDefault();
        var engine = new MatchEngine(state, new SequenceRandom(nextValues: [0, 20, 20, 20]));

        var first = engine.Tick();
        var second = engine.Tick();
        var third = engine.Tick();
        var fourth = engine.Tick();

        Assert.Equal("Ball", first.Code);
        Assert.Equal("Strike", second.Code);
        Assert.Equal("Strike", third.Code);
        Assert.Equal("Strikeout", fourth.Code);
        Assert.Equal(1, engine.CurrentState.Inning.Outs);
        Assert.Equal(0, engine.CurrentState.Count.Balls);
        Assert.Equal(0, engine.CurrentState.Count.Strikes);
        Assert.Equal(1, engine.CurrentState.CompletedPlays);
    }

    [Fact]
    public void Tick_ThreeStrikeoutPlateAppearancesInARow_SwitchesSidesAfterTheThirdOut()
    {
        var state = MatchStateFactory.CreateDefault();
        var engine = new MatchEngine(
            state,
            new SequenceRandom(nextValues: [20, 20, 20, 20, 20, 20, 20, 20, 20]));

        var events = new List<ResultEvent>();
        for (var index = 0; index < 9; index++)
        {
            events.Add(engine.Tick());
        }

        Assert.Equal(3, events.Count(result => result.EndsPlateAppearance));
        Assert.False(engine.CurrentState.Inning.IsTopHalf);
        Assert.Equal(0, engine.CurrentState.Inning.Outs);
        Assert.Equal(3, engine.CurrentState.CompletedPlays);
        Assert.Equal(0, engine.CurrentState.HomeTeam.BattingIndex);
        Assert.Equal(engine.CurrentState.HomeTeam.Lineup[0].Id, engine.CurrentState.CurrentBatter.Id);
    }
}
