using BaseballManager.Game.Screens;

namespace BaseballManager.Game.Screens.LiveMatch;

public sealed class LiveMatchScreen : GameScreen
{
    private readonly LiveMatchPresenter _presenter = new();

    public void UpdateFieldLayer()
    {
        _presenter.UpdateFieldView();
    }

    public void UpdatePlayResolution()
    {
        _presenter.ResolvePitchOutcome();
        _presenter.UpdateRunnerMovement();
    }

    public void UpdateOverlayLayer()
    {
        _presenter.UpdateOverlays();
    }

    public void HandlePauseAndManagerCommands()
    {
        _presenter.HandleManagerCommands();
    }
}
