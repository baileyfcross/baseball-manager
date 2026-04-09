using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Tests.Support;
using Xunit;

namespace BaseballManager.Sim.Tests.Engine;

public sealed class MatchTeamStateTests
{
    [Fact]
    public void TrySubstitutePitcher_ReplacesCurrentPitcher_AndRemovesReplacementFromBullpen()
    {
        var startingPitcher = MatchStateFactory.CreatePlayer("Starter", "SP", pitchingRating: 74, staminaRating: 72);
        var reliever = MatchStateFactory.CreatePlayer("Reliever", "RP", pitchingRating: 69, staminaRating: 58);
        var team = MatchStateFactory.CreateTeam("Home", "HOME", startingPitcher: startingPitcher, bullpenPlayers: [reliever]);

        var substituted = team.TrySubstitutePitcher(reliever.Id, out var outgoingPitcher, out var incomingPitcher);

        Assert.True(substituted);
        Assert.Equal(startingPitcher.Id, outgoingPitcher.Id);
        Assert.Equal(reliever.Id, incomingPitcher.Id);
        Assert.Equal(reliever.Id, team.CurrentPitcher.Id);
        Assert.Empty(team.BullpenPlayers);
    }

    [Fact]
    public void TrySubstituteLineupPlayer_ReplacesRequestedSlot_AndRemovesReplacementFromBench()
    {
        var lineup = Enumerable.Range(1, 9)
            .Select(index => MatchStateFactory.CreatePlayer($"Batter {index}", index == 1 ? "CF" : "IF"))
            .ToList();
        var benchPlayer = MatchStateFactory.CreatePlayer("Bench Bat", "OF", contactRating: 61);
        var team = MatchStateFactory.CreateTeam("Away", "AWY", lineup: lineup, benchPlayers: [benchPlayer]);

        var substituted = team.TrySubstituteLineupPlayer(3, benchPlayer.Id, out var outgoingPlayer, out var incomingPlayer);

        Assert.True(substituted);
        Assert.Equal(lineup[3].Id, outgoingPlayer.Id);
        Assert.Equal(benchPlayer.Id, incomingPlayer.Id);
        Assert.Equal(benchPlayer.Id, team.Lineup[3].Id);
        Assert.Empty(team.BenchPlayers);
    }

    [Fact]
    public void FindPlayer_ReturnsPlayersFromBenchBullpenAndCurrentPitcher()
    {
        var starter = MatchStateFactory.CreatePlayer("Starter", "SP");
        var reliever = MatchStateFactory.CreatePlayer("Reliever", "RP");
        var benchPlayer = MatchStateFactory.CreatePlayer("Bench", "OF");
        var team = MatchStateFactory.CreateTeam("Club", "CLB", startingPitcher: starter, benchPlayers: [benchPlayer], bullpenPlayers: [reliever], currentPitcher: reliever);

        Assert.Equal(reliever.Id, team.FindPlayer(reliever.Id)?.Id);
        Assert.Equal(benchPlayer.Id, team.FindPlayer(benchPlayer.Id)?.Id);
        Assert.Equal(reliever.Id, team.FindFielder("P")?.Id);
    }

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
