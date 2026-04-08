using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Tests.Support;
using Xunit;

namespace BaseballManager.Sim.Tests.Engine;

public sealed class MatchTeamStateTests
{
    [Fact]
    public void FindFielder_ReturnsTheStartingPitcher_ForPositionP()
    {
        var pitcher = MatchStateFactory.CreatePlayer("Ace", "SP", pitchingRating: 82, armRating: 70);
        var team = MatchStateFactory.CreateTeam("Visitors", "VIS", startingPitcher: pitcher);

        var fielder = team.FindFielder("P");

        Assert.NotNull(fielder);
        Assert.Equal(pitcher.Id, fielder!.Id);
    }

    [Fact]
    public void FindFielder_PrefersAnExactPrimaryOrSecondaryPositionMatch()
    {
        var shortstop = MatchStateFactory.CreatePlayer("Shortstop", "IF", secondaryPosition: "SS", armRating: 64);
        var lineup = new[]
        {
            MatchStateFactory.CreatePlayer("Left Fielder", "LF"),
            shortstop,
            MatchStateFactory.CreatePlayer("Catcher", "C")
        };
        var team = MatchStateFactory.CreateTeam("Visitors", "VIS", lineup);

        var fielder = team.FindFielder("SS");

        Assert.NotNull(fielder);
        Assert.Equal(shortstop.Id, fielder!.Id);
    }

    [Fact]
    public void FindFielder_FallsBackToAGenericOutfielder_WhenNoExactMatchExists()
    {
        var outfielder = MatchStateFactory.CreatePlayer("Utility Outfielder", "OF", armRating: 61);
        var lineup = new[]
        {
            MatchStateFactory.CreatePlayer("Infielder", "IF"),
            outfielder,
            MatchStateFactory.CreatePlayer("Catcher", "C")
        };
        var team = MatchStateFactory.CreateTeam("Visitors", "VIS", lineup);

        var fielder = team.FindFielder("CF");

        Assert.NotNull(fielder);
        Assert.Equal(outfielder.Id, fielder!.Id);
    }

    [Fact]
    public void FindFielder_ReturnsNull_ForBlankPositionLabels()
    {
        var team = MatchStateFactory.CreateTeam("Visitors", "VIS");

        Assert.Null(team.FindFielder(string.Empty));
        Assert.Null(team.FindFielder("   "));
    }
}
