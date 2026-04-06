namespace BaseballManager.Game.Data;

public sealed class FranchiseSaveState
{
    public string? SelectedTeamName { get; set; }

    public Dictionary<string, TeamFranchiseState> Teams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TeamFranchiseState
{
    public List<Guid?> LineupSlots { get; set; } = new();

    public List<Guid?> RotationSlots { get; set; } = new();
}