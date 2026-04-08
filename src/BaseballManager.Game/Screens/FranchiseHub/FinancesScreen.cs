using BaseballManager.Core.Economy;
using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class FinancesScreen : GameScreen
{
    private static readonly (string Key, string Label, string Hint)[] BudgetRows =
    [
        ("scouting", "Scouting", "Sharper reads on prospects and trade targets."),
        ("development", "Development", "Better practice gains and long-term growth."),
        ("medical", "Medical", "Improves recovery and keeps bodies fresher."),
        ("facilities", "Facilities", "Boosts club standards across the organization.")
    ];

    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private int _selectedHistoryIndex;
    private string _statusMessage = "Budget choices shape scouting accuracy, player development, and recovery over the full season.";

    public FinancesScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _selectedHistoryIndex = 0;
        _statusMessage = "Budget choices shape scouting accuracy, player development, and recovery over the full season.";
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;
        if (_ignoreClicksUntilRelease)
        {
            if (currentMouseState.LeftButton == ButtonState.Released)
            {
                _ignoreClicksUntilRelease = false;
            }

            _previousMouseState = currentMouseState;
            return;
        }

        if (_previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed)
        {
            var mousePosition = currentMouseState.Position;
            if (GetBackButtonBounds().Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (TryAdjustBudget(mousePosition))
            {
            }
            else if (TrySelectHistory(mousePosition))
            {
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);
        var economy = _franchiseSession.GetSelectedTeamEconomy();
        var history = _franchiseSession.GetRecentFinancialSnapshots(8);
        EnsureSelectionIsValid(history.Count);
        var mousePosition = Mouse.GetState().Position;

        uiRenderer.DrawText("Team Finances", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds($"{_franchiseSession.SelectedTeamName} | Market: {economy.MarketSize} | Fan Interest: {economy.FanInterest}/100", new Rectangle(168, 82, Math.Max(420, _viewport.X - 220), 20), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds("Revenue rises with wins and turnout. Monthly budget choices now feed into scouting, development, and medical quality.", new Rectangle(48, 112, Math.Max(620, _viewport.X - 96), 40), Color.White, uiRenderer.UiSmallFont, 2);

        var summaryBounds = GetSummaryPanelBounds();
        var budgetBounds = GetBudgetPanelBounds();
        var historyBounds = GetHistoryPanelBounds();
        var statusBounds = GetStatusPanelBounds();

        uiRenderer.DrawButton(string.Empty, summaryBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, budgetBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, historyBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, statusBounds, new Color(38, 48, 56), Color.White);

        DrawSummary(uiRenderer, economy);
        DrawBudgetControls(uiRenderer, economy, mousePosition);
        DrawFinanceHistory(uiRenderer, history, mousePosition);

        uiRenderer.DrawTextInBounds("Front Office Note", new Rectangle(statusBounds.X + 12, statusBounds.Y + 6, statusBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(statusBounds.X + 12, statusBounds.Y + 28, statusBounds.Width - 24, statusBounds.Height - 36), Color.White, uiRenderer.UiSmallFont, 3);
        uiRenderer.DrawButton(_backButton.Label, GetBackButtonBounds(), GetBackButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private void DrawSummary(UiRenderer uiRenderer, TeamEconomy economy)
    {
        var summaryBounds = GetSummaryPanelBounds();
        uiRenderer.DrawTextInBounds("Club Snapshot", new Rectangle(summaryBounds.X + 12, summaryBounds.Y + 8, 200, 18), Color.Gold, uiRenderer.UiSmallFont);

        var contentX = summaryBounds.X + 12;
        var contentY = summaryBounds.Y + 34;
        var columnWidth = (summaryBounds.Width - 36) / 2;
        var rowHeight = 40;

        DrawSummaryMetric(uiRenderer, "Cash On Hand", FormatMoney(economy.CashOnHand), new Rectangle(contentX, contentY, columnWidth, rowHeight));
        DrawSummaryMetric(uiRenderer, "Projected Budget", FormatMoney(economy.ProjectedBudget), new Rectangle(contentX + columnWidth + 12, contentY, columnWidth, rowHeight));
        DrawSummaryMetric(uiRenderer, "Player Payroll", FormatMoney(economy.PlayerPayroll), new Rectangle(contentX, contentY + rowHeight, columnWidth, rowHeight));
        DrawSummaryMetric(uiRenderer, "Coach Payroll", FormatMoney(economy.CoachPayroll), new Rectangle(contentX + columnWidth + 12, contentY + rowHeight, columnWidth, rowHeight));
        DrawSummaryMetric(uiRenderer, "Monthly Investment", FormatMoney(economy.BudgetAllocation.TotalMonthlyInvestment), new Rectangle(contentX, contentY + rowHeight * 2, columnWidth, rowHeight));
        DrawSummaryMetric(uiRenderer, "Ticket Price", FormatMoney(economy.TicketPrice), new Rectangle(contentX + columnWidth + 12, contentY + rowHeight * 2, columnWidth, rowHeight));
        DrawSummaryMetric(uiRenderer, "Facilities", $"Level {economy.FacilitiesLevel}", new Rectangle(contentX, contentY + rowHeight * 3, columnWidth, rowHeight));
        DrawSummaryMetric(uiRenderer, "Capacity", economy.StadiumCapacity.ToString("N0"), new Rectangle(contentX + columnWidth + 12, contentY + rowHeight * 3, columnWidth, rowHeight));
    }

    private void DrawSummaryMetric(UiRenderer uiRenderer, string label, string value, Rectangle bounds)
    {
        uiRenderer.DrawTextInBounds(label, new Rectangle(bounds.X, bounds.Y, bounds.Width, 16), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(value, new Rectangle(bounds.X, bounds.Y + 16, bounds.Width, 20), Color.White, uiRenderer.UiSmallFont);
    }

    private void DrawBudgetControls(UiRenderer uiRenderer, TeamEconomy economy, Point mousePosition)
    {
        var budgetBounds = GetBudgetPanelBounds();
        uiRenderer.DrawTextInBounds("Monthly Investment Choices", new Rectangle(budgetBounds.X + 12, budgetBounds.Y + 8, budgetBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);

        for (var i = 0; i < BudgetRows.Length; i++)
        {
            var row = BudgetRows[i];
            var rowBounds = GetBudgetRowBounds(i);
            uiRenderer.DrawButton(string.Empty, rowBounds, new Color(54, 62, 70), Color.White);
            uiRenderer.DrawTextInBounds(row.Label, new Rectangle(rowBounds.X + 8, rowBounds.Y + 4, 170, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(GetBudgetValue(economy.BudgetAllocation, row.Key), new Rectangle(rowBounds.X + 180, rowBounds.Y + 4, 120, 16), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds(row.Hint, new Rectangle(rowBounds.X + 8, rowBounds.Y + 22, rowBounds.Width - 96, rowBounds.Height - 24), Color.White, uiRenderer.UiSmallFont, 2);

            var downBounds = GetBudgetAdjustButtonBounds(i, false);
            var upBounds = GetBudgetAdjustButtonBounds(i, true);
            uiRenderer.DrawButton("-", downBounds, downBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
            uiRenderer.DrawButton("+", upBounds, upBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        }
    }

    private void DrawFinanceHistory(UiRenderer uiRenderer, IReadOnlyList<FinancialSnapshot> history, Point mousePosition)
    {
        var historyBounds = GetHistoryPanelBounds();
        uiRenderer.DrawTextInBounds("Recent Finance Log", new Rectangle(historyBounds.X + 12, historyBounds.Y + 8, historyBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);

        var listBounds = GetHistoryListBounds();
        var detailBounds = GetHistoryDetailBounds();
        uiRenderer.DrawButton(string.Empty, listBounds, new Color(32, 40, 48), Color.White);
        uiRenderer.DrawButton(string.Empty, detailBounds, new Color(32, 40, 48), Color.White);

        if (history.Count == 0)
        {
            uiRenderer.DrawWrappedTextInBounds("No finance events are logged yet. Sim a home game or advance into a new month to start building revenue and expense history.", new Rectangle(listBounds.X + 10, listBounds.Y + 10, listBounds.Width - 20, listBounds.Height - 20), Color.White, uiRenderer.UiSmallFont, 4);
            uiRenderer.DrawWrappedTextInBounds("Once entries appear, you can review attendance, revenue, expenses, and cash flow here.", new Rectangle(detailBounds.X + 10, detailBounds.Y + 10, detailBounds.Width - 20, detailBounds.Height - 20), Color.White, uiRenderer.UiSmallFont, 4);
            return;
        }

        var visibleRows = Math.Min(history.Count, GetHistoryRowCount());
        for (var i = 0; i < visibleRows; i++)
        {
            var snapshot = history[i];
            var rowBounds = GetHistoryRowBounds(i);
            var isSelected = i == _selectedHistoryIndex;
            var isHovered = rowBounds.Contains(mousePosition);
            var background = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DimGray : new Color(54, 62, 70));
            uiRenderer.DrawButton(string.Empty, rowBounds, background, Color.White);
            uiRenderer.DrawTextInBounds($"{snapshot.EffectiveDate:MMM d} - {snapshot.Category}", new Rectangle(rowBounds.X + 8, rowBounds.Y + 4, rowBounds.Width - 16, 16), Color.White, uiRenderer.UiSmallFont);
            var netText = snapshot.NetIncome >= 0 ? $"+{FormatMoney(snapshot.NetIncome)}" : $"-{FormatMoney(Math.Abs(snapshot.NetIncome))}";
            var netColor = snapshot.NetIncome >= 0 ? Color.LightGreen : Color.IndianRed;
            uiRenderer.DrawTextInBounds(netText, new Rectangle(rowBounds.X + 8, rowBounds.Y + 20, rowBounds.Width - 16, 16), netColor, uiRenderer.UiSmallFont);
        }

        var selectedSnapshot = history[_selectedHistoryIndex];
        uiRenderer.DrawTextInBounds(selectedSnapshot.Category, new Rectangle(detailBounds.X + 10, detailBounds.Y + 8, detailBounds.Width - 20, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Date: {selectedSnapshot.EffectiveDate:dddd, MMM d, yyyy}", new Rectangle(detailBounds.X + 10, detailBounds.Y + 28, detailBounds.Width - 20, 16), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Revenue: {FormatMoney(selectedSnapshot.Revenue)}", new Rectangle(detailBounds.X + 10, detailBounds.Y + 54, detailBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Expenses: {FormatMoney(selectedSnapshot.Expenses)}", new Rectangle(detailBounds.X + 10, detailBounds.Y + 74, detailBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Attendance: {selectedSnapshot.Attendance:N0}", new Rectangle(detailBounds.X + 10, detailBounds.Y + 94, detailBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Fan Interest: {selectedSnapshot.FanInterest}/100", new Rectangle(detailBounds.X + 10, detailBounds.Y + 114, detailBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Cash After: {FormatMoney(selectedSnapshot.CashAfter)}", new Rectangle(detailBounds.X + 10, detailBounds.Y + 134, detailBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(selectedSnapshot.Notes, new Rectangle(detailBounds.X + 10, detailBounds.Y + 160, detailBounds.Width - 20, detailBounds.Height - 170), Color.White, uiRenderer.UiSmallFont, 5);
    }

    private bool TryAdjustBudget(Point mousePosition)
    {
        for (var i = 0; i < BudgetRows.Length; i++)
        {
            if (GetBudgetAdjustButtonBounds(i, false).Contains(mousePosition))
            {
                _statusMessage = _franchiseSession.AdjustBudgetAllocation(BudgetRows[i].Key, -1);
                return true;
            }

            if (GetBudgetAdjustButtonBounds(i, true).Contains(mousePosition))
            {
                _statusMessage = _franchiseSession.AdjustBudgetAllocation(BudgetRows[i].Key, 1);
                return true;
            }
        }

        return false;
    }

    private bool TrySelectHistory(Point mousePosition)
    {
        var history = _franchiseSession.GetRecentFinancialSnapshots(8);
        var visibleRows = Math.Min(history.Count, GetHistoryRowCount());
        for (var i = 0; i < visibleRows; i++)
        {
            if (!GetHistoryRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedHistoryIndex = i;
            return true;
        }

        return false;
    }

    private void EnsureSelectionIsValid(int historyCount)
    {
        _selectedHistoryIndex = historyCount <= 0 ? 0 : Math.Clamp(_selectedHistoryIndex, 0, historyCount - 1);
    }

    private Rectangle GetBackButtonBounds() => new(24, 34, 120, 36);

    private Rectangle GetSummaryPanelBounds() => new(48, 160, Math.Max(480, _viewport.X - 96), 200);

    private Rectangle GetBudgetPanelBounds()
    {
        var summaryBounds = GetSummaryPanelBounds();
        var width = Math.Clamp((_viewport.X / 2) - 62, 420, 560);
        return new Rectangle(48, summaryBounds.Bottom + 16, width, Math.Max(220, _viewport.Y - summaryBounds.Bottom - 110));
    }

    private Rectangle GetHistoryPanelBounds()
    {
        var budgetBounds = GetBudgetPanelBounds();
        return new Rectangle(budgetBounds.Right + 16, budgetBounds.Y, Math.Max(340, _viewport.X - budgetBounds.Right - 64), budgetBounds.Height);
    }

    private Rectangle GetStatusPanelBounds()
    {
        var budgetBounds = GetBudgetPanelBounds();
        return new Rectangle(48, budgetBounds.Bottom + 12, Math.Max(480, _viewport.X - 96), Math.Max(60, _viewport.Y - budgetBounds.Bottom - 24));
    }

    private Rectangle GetBudgetRowBounds(int index)
    {
        var budgetBounds = GetBudgetPanelBounds();
        var rowHeight = 54;
        return new Rectangle(budgetBounds.X + 10, budgetBounds.Y + 34 + (index * (rowHeight + 8)), budgetBounds.Width - 20, rowHeight);
    }

    private Rectangle GetBudgetAdjustButtonBounds(int index, bool increase)
    {
        var rowBounds = GetBudgetRowBounds(index);
        var x = increase ? rowBounds.Right - 38 : rowBounds.Right - 76;
        return new Rectangle(x, rowBounds.Y + 10, 28, 28);
    }

    private Rectangle GetHistoryListBounds()
    {
        var historyBounds = GetHistoryPanelBounds();
        var listHeight = Math.Clamp(historyBounds.Height / 2, 150, 220);
        return new Rectangle(historyBounds.X + 10, historyBounds.Y + 30, historyBounds.Width - 20, listHeight);
    }

    private Rectangle GetHistoryDetailBounds()
    {
        var listBounds = GetHistoryListBounds();
        var historyBounds = GetHistoryPanelBounds();
        return new Rectangle(historyBounds.X + 10, listBounds.Bottom + 10, historyBounds.Width - 20, historyBounds.Bottom - listBounds.Bottom - 20);
    }

    private int GetHistoryRowCount()
    {
        return Math.Max(3, (GetHistoryListBounds().Height - 12) / 40);
    }

    private Rectangle GetHistoryRowBounds(int index)
    {
        var listBounds = GetHistoryListBounds();
        return new Rectangle(listBounds.X + 6, listBounds.Y + 6 + (index * 40), listBounds.Width - 12, 34);
    }

    private static string GetBudgetValue(BudgetAllocation allocation, string budgetKey)
    {
        var value = budgetKey.ToLowerInvariant() switch
        {
            "scouting" => allocation.ScoutingBudget,
            "development" => allocation.PlayerDevelopmentBudget,
            "medical" => allocation.MedicalBudget,
            "facilities" => allocation.FacilitiesBudget,
            _ => 0m
        };

        return FormatMoney(value);
    }

    private static string FormatMoney(decimal amount)
    {
        return amount >= 1_000_000m
            ? $"${amount / 1_000_000m:0.0}M"
            : $"${amount:0,0}";
    }
}