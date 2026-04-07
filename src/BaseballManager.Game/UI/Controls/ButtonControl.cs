namespace BaseballManager.Game.UI.Controls;

public sealed class ButtonControl
{
    public string Label { get; set; } = string.Empty;

    public Action? OnClick { get; set; }

    public void Click()
    {
        OnClick?.Invoke();
    }

    public static int GetSuggestedWidth(string label, int minimumWidth = 120, int horizontalPadding = 28, int approximateCharacterWidth = 9)
    {
        var safeLength = string.IsNullOrWhiteSpace(label) ? 0 : label.Length;
        return Math.Max(minimumWidth, (safeLength * approximateCharacterWidth) + horizontalPadding);
    }
}
