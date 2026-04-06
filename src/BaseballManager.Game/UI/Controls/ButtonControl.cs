namespace BaseballManager.Game.UI.Controls;

public sealed class ButtonControl
{
    public string Label { get; set; } = string.Empty;

    public Action? OnClick { get; set; }

    public void Click()
    {
        OnClick?.Invoke();
    }
}
