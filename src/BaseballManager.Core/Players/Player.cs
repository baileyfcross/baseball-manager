namespace BaseballManager.Core.Players;

public sealed class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string PrimaryPosition { get; set; } = string.Empty;
    public Handedness Throws { get; set; } = Handedness.Right;
    public BattingProfile Batting { get; set; } = BattingProfile.FromSingleSide(BattingStyle.RightOnly, 50, 48, 50);
    public BattingStyle BattingStyle => Batting.Style;
}
