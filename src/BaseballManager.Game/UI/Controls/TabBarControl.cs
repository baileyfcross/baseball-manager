namespace BaseballManager.Game.UI.Controls;

public sealed class TabBarControl
{
    public IReadOnlyList<string> Tabs { get; init; } = Array.Empty<string>();
    public int SelectedIndex { get; set; }
}
