using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class ScoutingScreen : GameScreen
{
    private const int PageSize = 8;
    private const int MaxVisibleDropdownOptions = 6;
    private const int MaxVisibleHireCandidates = 6;
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _tradeButton;
    private readonly ButtonControl _filterButton;
    private readonly ButtonControl _viewModeButton;
    private readonly ButtonControl _hireScoutButton;
    private readonly ButtonControl _releaseScoutButton;
    private readonly ButtonControl _assignRegionButton;
    private readonly ButtonControl _assignSelectedPlayerButton;
    private readonly ButtonControl _clearAssignmentButton;
    private readonly ButtonControl _scoutedPlayersTabButton;
    private readonly ButtonControl _targetListTabButton;
    private readonly ButtonControl _sortMostScoutedButton;
    private readonly ButtonControl _sortByPositionButton;
    private readonly ButtonControl _prospectPreviousButton;
    private readonly ButtonControl _prospectNextButton;
    private readonly ButtonControl _toggleTargetButton;
    private readonly ButtonControl _openScoutReportButton;
    private readonly ButtonControl _cancelScoutHireButton;
    private readonly ButtonControl _closeScoutReportPopupButton;
    private readonly ButtonControl _countryFocusButton;
    private readonly ButtonControl _positionFocusButton;
    private readonly ButtonControl _traitFocusButton;
    private readonly ButtonControl _marketPreviousButton;
    private readonly ButtonControl _marketNextButton;
    private readonly ButtonControl _offerPreviousButton;
    private readonly ButtonControl _offerNextButton;
    private readonly Rectangle _backButtonBounds = new(24, 34, 120, 36);
    private MouseState _previousMouseState = default;
    private Point _viewport = new(1280, 720);
    private bool _ignoreClicksUntilRelease = true;
    private int _marketPageIndex;
    private int _offerPageIndex;
    private int _prospectPageIndex;
    private int _scoutDropdownScrollIndex;
    private int _hireScoutPopupScrollIndex;
    private bool _showFilterDropdown;
    private bool _showScoutHirePopup;
    private bool _showScoutReportPopup;
    private ScoutingFilterMode _filterMode = ScoutingFilterMode.All;
    private ProspectSortMode _prospectSortMode = ProspectSortMode.MostScouted;
    private string _prospectPositionFilter = "All";
    private ScoutingViewMode _viewMode = ScoutingViewMode.TradeMarket;
    private ScoutDropdownType _openScoutDropdown = ScoutDropdownType.None;
    private string _selectedCoachRole = "Scouting Director";
    private Guid? _selectedPlayerId;
    private Guid? _selectedOfferPlayerId;
    private int _selectedScoutSlotIndex;
    private bool _showTargetList;
    private string? _selectedProspectKey;
    private string _statusMessage = "Use the scouting menu to switch between trade targets and high school / international coverage.";

    public ScoutingScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
        _tradeButton = new ButtonControl
        {
            Label = "Swap Players",
            OnClick = CompleteTrade
        };
        _filterButton = new ButtonControl
        {
            Label = "Filter: All",
            OnClick = () => _showFilterDropdown = !_showFilterDropdown
        };
        _viewModeButton = new ButtonControl
        {
            Label = "View: Trade Market",
            OnClick = ToggleViewMode
        };
        _hireScoutButton = new ButtonControl
        {
            Label = "Hire Scout",
            OnClick = HireOrRotateSelectedScout
        };
        _releaseScoutButton = new ButtonControl
        {
            Label = "Release Scout",
            OnClick = ReleaseSelectedScout
        };
        _assignRegionButton = new ButtonControl
        {
            Label = "Assign Region Search",
            OnClick = AssignSelectedScoutToRegion
        };
        _assignSelectedPlayerButton = new ButtonControl
        {
            Label = "Assign To Player",
            OnClick = AssignSelectedScoutToSelectedPlayer
        };
        _clearAssignmentButton = new ButtonControl
        {
            Label = "Clear Assignment",
            OnClick = ClearSelectedScoutAssignment
        };
        _scoutedPlayersTabButton = new ButtonControl
        {
            Label = "Scouted Players",
            OnClick = () => _showTargetList = false
        };
        _targetListTabButton = new ButtonControl
        {
            Label = "Target List",
            OnClick = () => _showTargetList = true
        };
        _sortMostScoutedButton = new ButtonControl
        {
            Label = "Most Scouted",
            OnClick = () => SetProspectSortMode(ProspectSortMode.MostScouted)
        };
        _sortByPositionButton = new ButtonControl
        {
            Label = "Position: All",
            OnClick = () => ToggleScoutDropdown(ScoutDropdownType.Sort)
        };
        _prospectPreviousButton = new ButtonControl
        {
            Label = "Prev",
            OnClick = () => ScrollProspectList(-1)
        };
        _prospectNextButton = new ButtonControl
        {
            Label = "Next",
            OnClick = () => ScrollProspectList(1)
        };
        _toggleTargetButton = new ButtonControl
        {
            Label = "Toggle Target",
            OnClick = ToggleSelectedProspectTargetStatus
        };
        _openScoutReportButton = new ButtonControl
        {
            Label = "Open Full Report",
            OnClick = () => _showScoutReportPopup = true
        };
        _cancelScoutHireButton = new ButtonControl
        {
            Label = "Cancel",
            OnClick = () => _showScoutHirePopup = false
        };
        _closeScoutReportPopupButton = new ButtonControl
        {
            Label = "Close",
            OnClick = () => _showScoutReportPopup = false
        };
        _countryFocusButton = new ButtonControl
        {
            Label = "Country",
            OnClick = () => ToggleScoutDropdown(ScoutDropdownType.Country)
        };
        _positionFocusButton = new ButtonControl
        {
            Label = "Position Focus",
            OnClick = () => ToggleScoutDropdown(ScoutDropdownType.Position)
        };
        _traitFocusButton = new ButtonControl
        {
            Label = "Trait Focus",
            OnClick = () => ToggleScoutDropdown(ScoutDropdownType.Trait)
        };
        _marketPreviousButton = new ButtonControl
        {
            Label = "Prev",
            OnClick = () => _marketPageIndex = Math.Max(0, _marketPageIndex - 1)
        };
        _marketNextButton = new ButtonControl
        {
            Label = "Next",
            OnClick = () => _marketPageIndex++
        };
        _offerPreviousButton = new ButtonControl
        {
            Label = "Prev",
            OnClick = () => _offerPageIndex = Math.Max(0, _offerPageIndex - 1)
        };
        _offerNextButton = new ButtonControl
        {
            Label = "Next",
            OnClick = () => _offerPageIndex++
        };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _showFilterDropdown = false;
        _showScoutHirePopup = false;
        _showScoutReportPopup = false;
        _openScoutDropdown = ScoutDropdownType.None;
        _showTargetList = false;
        _prospectPageIndex = 0;
        _prospectSortMode = ProspectSortMode.MostScouted;
        _prospectPositionFilter = "All";
        _viewMode = ScoutingViewMode.AmateurInternational;
        _statusMessage = "Set your scout assignments and review high school / international reports here.";
        RefreshAmateurSelections();
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

        var mouseWheelDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (mouseWheelDelta != 0)
        {
            var scrollDirection = mouseWheelDelta < 0 ? 1 : -1;
            var wheelPosition = currentMouseState.Position;

            if (_showScoutHirePopup)
            {
                ScrollScoutHirePopup(scrollDirection);
            }
            else if (_openScoutDropdown != ScoutDropdownType.None)
            {
                ScrollScoutDropdown(scrollDirection);
            }
            else if (!_showScoutReportPopup && _viewMode == ScoutingViewMode.AmateurInternational && GetProspectListPanelBounds().Contains(wheelPosition))
            {
                ScrollProspectList(scrollDirection);
            }
        }

        if (_previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed)
        {
            var mousePosition = currentMouseState.Position;

            if (_showScoutHirePopup)
            {
                HandleScoutHirePopupClick(mousePosition);
                _previousMouseState = currentMouseState;
                return;
            }

            if (_showScoutReportPopup)
            {
                HandleScoutReportPopupClick(mousePosition);
                _previousMouseState = currentMouseState;
                return;
            }

            if (_backButtonBounds.Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (_openScoutDropdown != ScoutDropdownType.None)
            {
                TrySelectScoutDropdownOption(mousePosition);
            }
            else if (GetHireScoutButtonBounds().Contains(mousePosition))
            {
                _hireScoutButton.Click();
            }
            else if (GetReleaseScoutButtonBounds().Contains(mousePosition))
            {
                _releaseScoutButton.Click();
            }
            else if (GetCountryFocusButtonBounds().Contains(mousePosition))
            {
                _countryFocusButton.Click();
            }
            else if (GetPositionFocusButtonBounds().Contains(mousePosition))
            {
                _positionFocusButton.Click();
            }
            else if (GetTraitFocusButtonBounds().Contains(mousePosition))
            {
                _traitFocusButton.Click();
            }
            else if (GetAssignRegionButtonBounds().Contains(mousePosition))
            {
                _assignRegionButton.Click();
            }
            else if (GetAssignSelectedPlayerButtonBounds().Contains(mousePosition))
            {
                _assignSelectedPlayerButton.Click();
            }
            else if (GetClearAssignmentButtonBounds().Contains(mousePosition))
            {
                _clearAssignmentButton.Click();
            }
            else if (GetScoutedPlayersTabBounds().Contains(mousePosition))
            {
                _scoutedPlayersTabButton.Click();
            }
            else if (GetTargetListTabBounds().Contains(mousePosition))
            {
                _targetListTabButton.Click();
            }
            else if (GetProspectSortMostScoutedBounds().Contains(mousePosition))
            {
                _sortMostScoutedButton.Click();
            }
            else if (GetProspectSortPositionBounds().Contains(mousePosition))
            {
                _sortByPositionButton.Click();
            }
            else if (GetProspectPreviousBounds().Contains(mousePosition))
            {
                _prospectPreviousButton.Click();
            }
            else if (GetProspectNextBounds().Contains(mousePosition))
            {
                _prospectNextButton.Click();
            }
            else if (GetToggleTargetButtonBounds().Contains(mousePosition))
            {
                _toggleTargetButton.Click();
            }
            else if (GetOpenScoutReportButtonBounds().Contains(mousePosition))
            {
                _openScoutReportButton.Click();
            }
            else if (TrySelectAssistantScout(mousePosition))
            {
            }
            else if (TrySelectProspect(mousePosition))
            {
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        uiRenderer.DrawText("Scouting", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, Math.Max(320, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(
            "Your head scout can oversee up to three area scouts for U.S. high school and international coverage. Click a scout slot, then set the country, position, and trait focus you want them chasing.",
            new Rectangle(168, 112, Math.Max(720, _viewport.X - 236), 48),
            Color.White,
            uiRenderer.UiSmallFont,
            3);

        DrawAmateurScouting(uiRenderer);

        var mousePosition = Mouse.GetState().Position;
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, _backButtonBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);

        if (_showScoutHirePopup)
        {
            DrawScoutHirePopup(uiRenderer, mousePosition);
        }
        else if (_showScoutReportPopup)
        {
            DrawScoutReportPopup(uiRenderer, mousePosition);
        }
    }

    private void DrawCoaches(UiRenderer uiRenderer, IReadOnlyList<CoachProfileView> coaches)
    {
        var coachHeaderBounds = new Rectangle(GetCoachRowBounds(0).X, 174, GetCoachRowBounds(0).Width, 24);
        uiRenderer.DrawTextInBounds("COACHES", coachHeaderBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);

        for (var i = 0; i < coaches.Count; i++)
        {
            var coach = coaches[i];
            var bounds = GetCoachRowBounds(i);
            var isHovered = !_showFilterDropdown && bounds.Contains(Mouse.GetState().Position);
            var isSelected = string.Equals(_selectedCoachRole, coach.Role, StringComparison.OrdinalIgnoreCase);
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton($"{coach.Role}: {Truncate(coach.Name, 14)}", bounds, color, Color.White);
        }
    }

    private void DrawMarketPlayers(UiRenderer uiRenderer, IReadOnlyList<ScoutingPlayerCard> marketPlayers)
    {
        var marketHeaderBounds = new Rectangle(GetMarketRowBounds(0).X, 174, GetMarketRowBounds(0).Width, 24);
        uiRenderer.DrawTextInBounds("PLAYERS AROUND THE LEAGUE", marketHeaderBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        var visiblePlayers = marketPlayers.Skip(_marketPageIndex * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetMarketRowBounds(i);
            var isHovered = !_showFilterDropdown && bounds.Contains(Mouse.GetState().Position);
            var isSelected = _selectedPlayerId == player.PlayerId;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            var ownTag = player.IsOnSelectedTeam ? " (Yours)" : string.Empty;
            var label = $"{player.TeamAbbreviation} {Truncate(player.PlayerName, 15)} {player.PrimaryPosition} Age {player.Age}{ownTag}";
            uiRenderer.DrawButton(label, bounds, color, Color.White);
        }

        DrawListPaging(uiRenderer, GetMarketPreviousBounds(), GetMarketNextBounds(), _marketPageIndex, marketPlayers.Count);
    }

    private void DrawOfferPlayers(UiRenderer uiRenderer, IReadOnlyList<ScoutingPlayerCard> offerPlayers)
    {
        var offerHeaderBounds = new Rectangle(GetOfferRowBounds(0).X, 174, GetOfferRowBounds(0).Width, 24);
        uiRenderer.DrawTextInBounds("PLAYER YOU OFFER", offerHeaderBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        var visiblePlayers = offerPlayers.Skip(_offerPageIndex * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetOfferRowBounds(i);
            var isHovered = !_showFilterDropdown && bounds.Contains(Mouse.GetState().Position);
            var isSelected = _selectedOfferPlayerId == player.PlayerId;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            var label = $"{Truncate(player.PlayerName, 18)} {player.PrimaryPosition}/{player.SecondaryPosition} Age {player.Age}";
            uiRenderer.DrawButton(label, bounds, color, Color.White);
        }

        DrawListPaging(uiRenderer, GetOfferPreviousBounds(), GetOfferNextBounds(), _offerPageIndex, offerPlayers.Count);
    }

    private void DrawReportArea(UiRenderer uiRenderer, IReadOnlyList<CoachProfileView> coaches, IReadOnlyList<ScoutingPlayerCard> marketPlayers)
    {
        var selectedCoach = coaches.FirstOrDefault(coach => string.Equals(coach.Role, _selectedCoachRole, StringComparison.OrdinalIgnoreCase))
            ?? coaches.FirstOrDefault();
        var selectedPlayer = marketPlayers.FirstOrDefault(player => player.PlayerId == _selectedPlayerId)
            ?? marketPlayers.FirstOrDefault();

        var reportPanelBounds = GetReportPanelBounds();
        var movesPanelBounds = GetMovesPanelBounds(reportPanelBounds);

        uiRenderer.DrawTextInBounds("SCOUT REPORT", new Rectangle(reportPanelBounds.X, reportPanelBounds.Y - 30, 300, 24), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawButton(string.Empty, reportPanelBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, movesPanelBounds, new Color(38, 48, 56), Color.White);

        if (selectedCoach == null || selectedPlayer == null)
        {
            uiRenderer.DrawWrappedTextInBounds("Select a coach and a player to see a report.", new Rectangle(reportPanelBounds.X + 12, reportPanelBounds.Y + 12, reportPanelBounds.Width - 24, reportPanelBounds.Height - 24), Color.White, uiRenderer.ScoreboardFont, 2);
            return;
        }

        var report = _franchiseSession.GetCoachScoutingReport(selectedPlayer.PlayerId, selectedCoach.Role);
        uiRenderer.DrawTextInBounds($"{report.CoachName} - {report.CoachRole}", new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 8, reportPanelBounds.Width - 20, 16), Color.Goldenrod, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Specialty: {selectedCoach.Specialty} | Voice: {selectedCoach.Voice}", new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 24, reportPanelBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);

        var reportText = string.Join(" ", new[]
        {
            BuildLastSeasonSummary(selectedPlayer),
            report.Summary,
            report.Strengths,
            report.Concern,
            report.TransferRecommendation,
            $"Status: {_statusMessage}"
        });

        uiRenderer.DrawWrappedTextInBounds(reportText, new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 44, reportPanelBounds.Width - 20, reportPanelBounds.Height - 52), Color.White, uiRenderer.UiSmallFont, 5);

        uiRenderer.DrawTextInBounds("Recent Moves", new Rectangle(movesPanelBounds.X + 10, movesPanelBounds.Y + 8, movesPanelBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);
        var recentMoves = _franchiseSession.GetRecentTransferSummaries(2);
        var movesText = recentMoves.Count == 0
            ? "No recent swaps yet."
            : string.Join(" ", recentMoves);
        uiRenderer.DrawWrappedTextInBounds(movesText, new Rectangle(movesPanelBounds.X + 10, movesPanelBounds.Y + 28, movesPanelBounds.Width - 20, movesPanelBounds.Height - 36), Color.White, uiRenderer.UiSmallFont, 5);
    }

    private void DrawListPaging(UiRenderer uiRenderer, Rectangle previousBounds, Rectangle nextBounds, int currentPage, int totalItems)
    {
        uiRenderer.DrawButton(_marketPreviousButton.Label, previousBounds, !_showFilterDropdown && previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_marketNextButton.Label, nextBounds, !_showFilterDropdown && nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);

        var pageLabel = $"Page {currentPage + 1} / {GetMaxPage(totalItems) + 1}";
        var labelBounds = new Rectangle(previousBounds.Right + 8, previousBounds.Y, Math.Max(84, nextBounds.X - previousBounds.Right - 16), previousBounds.Height);
        uiRenderer.DrawButton(string.Empty, labelBounds, new Color(70, 70, 70), Color.White);
        uiRenderer.DrawTextInBounds(pageLabel, labelBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
    }

    private bool TrySelectCoach(Point mousePosition)
    {
        var coaches = _franchiseSession.GetCoachingStaff();
        for (var i = 0; i < coaches.Count; i++)
        {
            if (!GetCoachRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedCoachRole = coaches[i].Role;
            _statusMessage = $"Talking with {coaches[i].Name}.";
            return true;
        }

        return false;
    }

    private bool TrySelectMarketPlayer(Point mousePosition)
    {
        var visiblePlayers = ApplyFilter(_franchiseSession.GetScoutingBoardPlayers()).Skip(_marketPageIndex * PageSize).Take(PageSize).ToList();
        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            if (!GetMarketRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedPlayerId = visiblePlayers[i].PlayerId;
            _statusMessage = $"{visiblePlayers[i].PlayerName} is now on the board for discussion.";
            return true;
        }

        return false;
    }

    private bool TrySelectOfferPlayer(Point mousePosition)
    {
        var visiblePlayers = _franchiseSession.GetTradeChipPlayers().Skip(_offerPageIndex * PageSize).Take(PageSize).ToList();
        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            if (!GetOfferRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedOfferPlayerId = visiblePlayers[i].PlayerId;
            _statusMessage = $"You are offering {visiblePlayers[i].PlayerName} in the swap.";
            return true;
        }

        return false;
    }

    private void CompleteTrade()
    {
        if (!_selectedPlayerId.HasValue || !_selectedOfferPlayerId.HasValue)
        {
            _statusMessage = "Pick both a target and an outgoing player first.";
            return;
        }

        _franchiseSession.TryTradeForPlayer(_selectedPlayerId.Value, _selectedOfferPlayerId.Value, out _statusMessage);
        RefreshSelections();
    }

    private void ToggleViewMode()
    {
        _viewMode = _viewMode == ScoutingViewMode.TradeMarket
            ? ScoutingViewMode.AmateurInternational
            : ScoutingViewMode.TradeMarket;
        _showFilterDropdown = false;
        _statusMessage = _viewMode == ScoutingViewMode.TradeMarket
            ? "Back on the league market board. Talk to your coaches and line up trades."
            : "Amateur and international scouting is active. Set each scout's country, position, and trait focus.";
        RefreshAmateurSelections();
    }

    private void HireOrRotateSelectedScout()
    {
        _openScoutDropdown = ScoutDropdownType.None;
        var selectedScout = _franchiseSession.GetScoutDepartment()
            .FirstOrDefault(entry => !entry.IsHeadScout && entry.SlotIndex == _selectedScoutSlotIndex);

        if (selectedScout == null || selectedScout.IsVacant)
        {
            _showScoutHirePopup = true;
            _hireScoutPopupScrollIndex = 0;
            _statusMessage = "Choose one of the available scouts to fill this open slot.";
            return;
        }

        _showScoutHirePopup = false;
        _statusMessage = string.Equals(selectedScout.AssignmentMode, "Unassigned", StringComparison.OrdinalIgnoreCase)
            ? "Use the buttons below to assign this scout to a region or a player."
            : "Use the buttons below to reassign this scout to a new region or player.";
    }

    private void HandleScoutHirePopupClick(Point mousePosition)
    {
        var candidates = GetVisibleScoutHireCandidates();
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!GetScoutHireOptionBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _showScoutHirePopup = false;
            _franchiseSession.HireAssistantScout(_selectedScoutSlotIndex, _hireScoutPopupScrollIndex + i, out _statusMessage);
            RefreshAmateurSelections();
            return;
        }

        if (GetScoutHireCancelBounds().Contains(mousePosition) || !GetScoutHirePopupBounds().Contains(mousePosition))
        {
            _showScoutHirePopup = false;
        }
    }

    private void HandleScoutReportPopupClick(Point mousePosition)
    {
        if (GetScoutReportPopupCloseBounds().Contains(mousePosition) || !GetScoutReportPopupBounds().Contains(mousePosition))
        {
            _showScoutReportPopup = false;
        }
    }

    private void AssignSelectedScoutToRegion()
    {
        _openScoutDropdown = ScoutDropdownType.None;
        _statusMessage = _franchiseSession.AssignScoutToRegionSearch(_selectedScoutSlotIndex);
        RefreshAmateurSelections();
    }

    private void AssignSelectedScoutToSelectedPlayer()
    {
        _openScoutDropdown = ScoutDropdownType.None;
        if (string.IsNullOrWhiteSpace(_selectedProspectKey))
        {
            _statusMessage = "Select a scouted player first, then assign the scout to follow him.";
            return;
        }

        _statusMessage = _franchiseSession.AssignScoutToScoutedPlayer(_selectedScoutSlotIndex, _selectedProspectKey);
        RefreshAmateurSelections();
    }

    private void ClearSelectedScoutAssignment()
    {
        _openScoutDropdown = ScoutDropdownType.None;
        _statusMessage = _franchiseSession.ClearScoutAssignment(_selectedScoutSlotIndex);
        RefreshAmateurSelections();
    }

    private void ToggleSelectedProspectTargetStatus()
    {
        if (string.IsNullOrWhiteSpace(_selectedProspectKey))
        {
            _statusMessage = "Select a scouted player first.";
            return;
        }

        var selectedProspect = _franchiseSession
            .GetScoutedPlayers()
            .FirstOrDefault(prospect => string.Equals(prospect.ProspectKey, _selectedProspectKey, StringComparison.OrdinalIgnoreCase));

        _statusMessage = selectedProspect?.IsOnTargetList == true
            ? _franchiseSession.RemoveScoutedPlayerFromTargetList(_selectedProspectKey)
            : _franchiseSession.AddScoutedPlayerToTargetList(_selectedProspectKey);
        RefreshAmateurSelections();
    }

    private void ToggleScoutDropdown(ScoutDropdownType dropdownType)
    {
        _openScoutDropdown = _openScoutDropdown == dropdownType ? ScoutDropdownType.None : dropdownType;
        _scoutDropdownScrollIndex = 0;
    }

    private void ReleaseSelectedScout()
    {
        _openScoutDropdown = ScoutDropdownType.None;
        _franchiseSession.ReleaseAssistantScout(_selectedScoutSlotIndex, out _statusMessage);
        RefreshAmateurSelections();
    }

    private bool TrySelectScoutDropdownOption(Point mousePosition)
    {
        var options = GetVisibleScoutDropdownOptions();
        for (var i = 0; i < options.Count; i++)
        {
            if (!GetScoutDropdownOptionBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _statusMessage = _openScoutDropdown switch
            {
                ScoutDropdownType.Country => _franchiseSession.SetAssistantScoutCountry(_selectedScoutSlotIndex, options[i]),
                ScoutDropdownType.Position => _franchiseSession.SetAssistantScoutPositionFocus(_selectedScoutSlotIndex, options[i]),
                ScoutDropdownType.Trait => _franchiseSession.SetAssistantScoutTraitFocus(_selectedScoutSlotIndex, options[i]),
                ScoutDropdownType.Sort => ApplyProspectPositionSelection(options[i]),
                _ => _statusMessage
            };

            _openScoutDropdown = ScoutDropdownType.None;
            _scoutDropdownScrollIndex = 0;
            RefreshAmateurSelections();
            return true;
        }

        _openScoutDropdown = ScoutDropdownType.None;
        _scoutDropdownScrollIndex = 0;
        return false;
    }

    private void DrawScoutDropdown(UiRenderer uiRenderer)
    {
        var options = GetVisibleScoutDropdownOptions();
        var selectedValue = GetOpenScoutDropdownSelectedValue();
        var panelBounds = GetScoutDropdownPanelBounds();
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(28, 36, 44), Color.White);

        for (var i = 0; i < options.Count; i++)
        {
            var bounds = GetScoutDropdownOptionBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = string.Equals(options[i], selectedValue, StringComparison.OrdinalIgnoreCase);
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton(options[i], bounds, color, Color.White, uiRenderer.UiSmallFont);
        }
    }

    private void DrawScoutHirePopup(UiRenderer uiRenderer, Point mousePosition)
    {
        var panelBounds = GetScoutHirePopupBounds();
        var candidates = GetVisibleScoutHireCandidates();

        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(28, 32, 40), Color.White);
        uiRenderer.DrawTextInBounds("Hire Scout", new Rectangle(panelBounds.X + 12, panelBounds.Y + 10, panelBounds.Width - 24, 24), Color.Gold, uiRenderer.UiMediumFont, centerHorizontally: true);
        uiRenderer.DrawWrappedTextInBounds("Pick from the available scout pool for this open slot.", new Rectangle(panelBounds.X + 18, panelBounds.Y + 42, panelBounds.Width - 36, 34), Color.White, uiRenderer.UiSmallFont, 2);

        if (candidates.Count == 0)
        {
            uiRenderer.DrawWrappedTextInBounds("No scout candidates are available right now.", new Rectangle(panelBounds.X + 18, panelBounds.Y + 84, panelBounds.Width - 36, 28), Color.White, uiRenderer.UiSmallFont, 2);
        }
        else
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var bounds = GetScoutHireOptionBounds(i);
                var isHovered = bounds.Contains(mousePosition);
                var label = $"{candidate.Name} | {candidate.Specialty} | {candidate.Voice}";
                uiRenderer.DrawButton(Truncate(label, 56), bounds, isHovered ? Color.DarkOliveGreen : Color.SlateGray, Color.White, uiRenderer.UiSmallFont);
            }
        }

        var cancelBounds = GetScoutHireCancelBounds();
        uiRenderer.DrawButton(_cancelScoutHireButton.Label, cancelBounds, cancelBounds.Contains(mousePosition) ? Color.DarkSlateGray : Color.SlateGray, Color.White);
    }

    private void DrawScoutReportPopup(UiRenderer uiRenderer, Point mousePosition)
    {
        var panelBounds = GetScoutReportPopupBounds();
        var closeBounds = GetScoutReportPopupCloseBounds();
        var department = _franchiseSession.GetScoutDepartment().Where(entry => !entry.IsHeadScout).OrderBy(entry => entry.SlotIndex).ToList();
        var selectedScout = department.FirstOrDefault(scout => scout.SlotIndex == _selectedScoutSlotIndex) ?? department.FirstOrDefault();
        var selectedProspect = GetVisibleProspects().FirstOrDefault(prospect => string.Equals(prospect.ProspectKey, _selectedProspectKey, StringComparison.OrdinalIgnoreCase));

        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(28, 32, 40), Color.White);
        uiRenderer.DrawTextInBounds("Full Scout Report", new Rectangle(panelBounds.X + 12, panelBounds.Y + 10, panelBounds.Width - 24, 24), Color.Gold, uiRenderer.UiMediumFont, centerHorizontally: true);
        uiRenderer.DrawWrappedTextInBounds(BuildExpandedScoutReport(selectedScout, selectedProspect), new Rectangle(panelBounds.X + 18, panelBounds.Y + 42, panelBounds.Width - 36, panelBounds.Height - 94), Color.White, uiRenderer.UiSmallFont, 14);
        uiRenderer.DrawButton(_closeScoutReportPopupButton.Label, closeBounds, closeBounds.Contains(mousePosition) ? Color.DarkSlateGray : Color.SlateGray, Color.White);
    }

    private IReadOnlyList<string> GetOpenScoutDropdownOptions()
    {
        return _openScoutDropdown switch
        {
            ScoutDropdownType.Country => _franchiseSession.GetScoutCountryOptions(),
            ScoutDropdownType.Position => _franchiseSession.GetScoutPositionOptions(),
            ScoutDropdownType.Trait => _franchiseSession.GetScoutTraitOptions(),
            ScoutDropdownType.Sort => ["All", "C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "OF", "SP", "RP"],
            _ => []
        };
    }

    private string GetOpenScoutDropdownSelectedValue()
    {
        if (_openScoutDropdown == ScoutDropdownType.Sort)
        {
            return _prospectPositionFilter;
        }

        var selectedScout = _franchiseSession.GetScoutDepartment()
            .Where(entry => !entry.IsHeadScout)
            .FirstOrDefault(scout => scout.SlotIndex == _selectedScoutSlotIndex);

        if (selectedScout == null)
        {
            return string.Empty;
        }

        return _openScoutDropdown switch
        {
            ScoutDropdownType.Country => selectedScout.Country,
            ScoutDropdownType.Position => selectedScout.PositionFocus,
            ScoutDropdownType.Trait => selectedScout.TraitFocus,
            _ => string.Empty
        };
    }

    private string GetScoutDropdownLabel(string prefix, string value, ScoutDropdownType dropdownType)
    {
        var suffix = _openScoutDropdown == dropdownType ? " ^" : " v";
        return $"{prefix}: {value}{suffix}";
    }

    private void DrawAmateurScouting(UiRenderer uiRenderer)
    {
        var department = _franchiseSession.GetScoutDepartment();
        var headScout = department.FirstOrDefault(entry => entry.IsHeadScout);
        var assistantScouts = department.Where(entry => !entry.IsHeadScout).OrderBy(entry => entry.SlotIndex).ToList();
        var prospects = GetVisibleProspects().ToList();
        var visibleProspects = GetPagedProspects(prospects).ToList();
        RefreshAmateurSelections(assistantScouts, prospects);

        var mousePosition = Mouse.GetState().Position;
        var suppressHover = _openScoutDropdown != ScoutDropdownType.None || _showScoutHirePopup || _showScoutReportPopup;
        var selectedScout = assistantScouts.FirstOrDefault(scout => scout.SlotIndex == _selectedScoutSlotIndex) ?? assistantScouts.FirstOrDefault();
        var selectedProspect = prospects.FirstOrDefault(prospect => string.Equals(prospect.ProspectKey, _selectedProspectKey, StringComparison.OrdinalIgnoreCase))
            ?? prospects.FirstOrDefault();
        var shortReportText = BuildShortScoutReport(selectedScout, selectedProspect);

        var departmentPanelBounds = GetScoutDepartmentPanelBounds();
        var assignmentPanelBounds = GetScoutAssignmentPanelBounds();
        var prospectListPanelBounds = GetProspectListPanelBounds();
        var prospectReportPanelBounds = GetProspectReportPanelBounds();

        uiRenderer.DrawButton(string.Empty, departmentPanelBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawTextInBounds("SCOUTING DEPARTMENT", new Rectangle(departmentPanelBounds.X + 10, departmentPanelBounds.Y + 8, departmentPanelBounds.Width - 20, 18), Color.Gold, uiRenderer.UiSmallFont);
        if (headScout != null)
        {
            uiRenderer.DrawTextInBounds($"Head Scout: {headScout.Name}", new Rectangle(departmentPanelBounds.X + 12, departmentPanelBounds.Y + 32, departmentPanelBounds.Width - 24, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{headScout.Specialty} | {headScout.Voice}", new Rectangle(departmentPanelBounds.X + 12, departmentPanelBounds.Y + 52, departmentPanelBounds.Width - 24, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds("Hire up to three scouts under him. They must be assigned before any players can be found.", new Rectangle(departmentPanelBounds.X + 12, departmentPanelBounds.Y + 74, departmentPanelBounds.Width - 24, 34), Color.White, uiRenderer.UiSmallFont, 2);
        }

        for (var i = 0; i < assistantScouts.Count; i++)
        {
            var scout = assistantScouts[i];
            var bounds = GetAssistantScoutRowBounds(i);
            var isHovered = !suppressHover && bounds.Contains(mousePosition);
            var isSelected = scout.SlotIndex == _selectedScoutSlotIndex;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            var assignmentLabel = GetDepartmentAssignmentLabel(scout);
            var label = scout.IsVacant
                ? $"{scout.Role}: Open Slot"
                : $"{scout.Role}: {Truncate(scout.Name, 14)} | {Truncate(assignmentLabel, 18)}";
            uiRenderer.DrawButton(label, bounds, color, Color.White);
        }

        uiRenderer.DrawButton(string.Empty, assignmentPanelBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawTextInBounds("SELECTED SCOUT", new Rectangle(assignmentPanelBounds.X + 10, assignmentPanelBounds.Y + 8, assignmentPanelBounds.Width - 20, 18), Color.Gold, uiRenderer.UiSmallFont);
        if (selectedScout != null)
        {
            uiRenderer.DrawTextInBounds($"{selectedScout.Role}: {selectedScout.Name}", new Rectangle(assignmentPanelBounds.X + 12, assignmentPanelBounds.Y + 30, assignmentPanelBounds.Width - 24, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"Specialty: {selectedScout.Specialty} | Voice: {selectedScout.Voice}", new Rectangle(assignmentPanelBounds.X + 12, assignmentPanelBounds.Y + 48, assignmentPanelBounds.Width - 24, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"Status: {selectedScout.AssignmentMode}", new Rectangle(assignmentPanelBounds.X + 12, assignmentPanelBounds.Y + 66, assignmentPanelBounds.Width - 24, 16), Color.Goldenrod, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds($"Target: {selectedScout.AssignmentTarget}", new Rectangle(assignmentPanelBounds.X + 12, assignmentPanelBounds.Y + 82, assignmentPanelBounds.Width - 24, 30), Color.White, uiRenderer.UiSmallFont, 2);

            _hireScoutButton.Label = GetScoutPrimaryActionLabel(selectedScout);
            uiRenderer.DrawButton(_hireScoutButton.Label, GetHireScoutButtonBounds(), !suppressHover && GetHireScoutButtonBounds().Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
            uiRenderer.DrawButton(_releaseScoutButton.Label, GetReleaseScoutButtonBounds(), !suppressHover && GetReleaseScoutButtonBounds().Contains(mousePosition) ? Color.DarkRed : Color.Firebrick, Color.White);
            uiRenderer.DrawButton(GetScoutDropdownLabel("Country", selectedScout.Country, ScoutDropdownType.Country), GetCountryFocusButtonBounds(), !suppressHover && GetCountryFocusButtonBounds().Contains(mousePosition) || _openScoutDropdown == ScoutDropdownType.Country ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
            uiRenderer.DrawButton(GetScoutDropdownLabel("Position", selectedScout.PositionFocus, ScoutDropdownType.Position), GetPositionFocusButtonBounds(), !suppressHover && GetPositionFocusButtonBounds().Contains(mousePosition) || _openScoutDropdown == ScoutDropdownType.Position ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
            uiRenderer.DrawButton(GetScoutDropdownLabel("Trait", selectedScout.TraitFocus, ScoutDropdownType.Trait), GetTraitFocusButtonBounds(), !suppressHover && GetTraitFocusButtonBounds().Contains(mousePosition) || _openScoutDropdown == ScoutDropdownType.Trait ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
            uiRenderer.DrawButton(_assignRegionButton.Label, GetAssignRegionButtonBounds(), !suppressHover && GetAssignRegionButtonBounds().Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
            uiRenderer.DrawButton(_assignSelectedPlayerButton.Label, GetAssignSelectedPlayerButtonBounds(), !suppressHover && GetAssignSelectedPlayerButtonBounds().Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
            uiRenderer.DrawButton(_clearAssignmentButton.Label, GetClearAssignmentButtonBounds(), !suppressHover && GetClearAssignmentButtonBounds().Contains(mousePosition) ? Color.DarkRed : Color.Firebrick, Color.White);

        }

        uiRenderer.DrawButton(string.Empty, prospectListPanelBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, prospectReportPanelBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(_scoutedPlayersTabButton.Label, GetScoutedPlayersTabBounds(), !_showTargetList ? Color.DarkOliveGreen : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_targetListTabButton.Label, GetTargetListTabBounds(), _showTargetList ? Color.DarkOliveGreen : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_sortMostScoutedButton.Label, GetProspectSortMostScoutedBounds(), _prospectSortMode == ProspectSortMode.MostScouted ? Color.DarkOliveGreen : Color.SlateGray, Color.White, uiRenderer.UiSmallFont);
        _sortByPositionButton.Label = GetScoutDropdownLabel("Position", _prospectPositionFilter, ScoutDropdownType.Sort);
        uiRenderer.DrawButton(_sortByPositionButton.Label, GetProspectSortPositionBounds(), ((!suppressHover && GetProspectSortPositionBounds().Contains(mousePosition)) || _openScoutDropdown == ScoutDropdownType.Sort || _prospectSortMode == ProspectSortMode.Position) ? Color.DarkSlateBlue : Color.SlateGray, Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawButton(_prospectPreviousButton.Label, GetProspectPreviousBounds(), !suppressHover && GetProspectPreviousBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawButton(_prospectNextButton.Label, GetProspectNextBounds(), !suppressHover && GetProspectNextBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Page {_prospectPageIndex + 1}/{Math.Max(1, GetProspectMaxPage(prospects.Count) + 1)}", GetProspectPageLabelBounds(), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds(_showTargetList ? "TARGET LIST" : "SCOUTED PLAYERS", new Rectangle(prospectListPanelBounds.X + 10, prospectListPanelBounds.Y + 76, prospectListPanelBounds.Width - 20, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds("SCOUT REPORT", new Rectangle(prospectReportPanelBounds.X + 10, prospectReportPanelBounds.Y + 8, prospectReportPanelBounds.Width - 20, 18), Color.Gold, uiRenderer.UiSmallFont);

        if (prospects.Count == 0)
        {
            var emptyText = _showTargetList
                ? "No players are on the target list yet. Move scouted players there when they catch your eye."
                : "No players have been discovered yet. Hire a scout and assign him to a region so reports can start coming in over the next few days.";
            uiRenderer.DrawWrappedTextInBounds(emptyText, new Rectangle(prospectListPanelBounds.X + 12, prospectListPanelBounds.Y + 104, prospectListPanelBounds.Width - 24, 60), Color.White, uiRenderer.UiSmallFont, 3);
            uiRenderer.DrawButton(_openScoutReportButton.Label, GetOpenScoutReportButtonBounds(), !suppressHover && GetOpenScoutReportButtonBounds().Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds(shortReportText, new Rectangle(prospectReportPanelBounds.X + 12, prospectReportPanelBounds.Y + 66, prospectReportPanelBounds.Width - 24, prospectReportPanelBounds.Height - 78), Color.White, uiRenderer.UiSmallFont, 5);
            if (_openScoutDropdown != ScoutDropdownType.None)
            {
                DrawScoutDropdown(uiRenderer);
            }
            return;
        }

        for (var i = 0; i < visibleProspects.Count; i++)
        {
            var prospect = visibleProspects[i];
            var bounds = GetProspectRowBounds(i);
            var isHovered = !suppressHover && bounds.Contains(mousePosition);
            var isSelected = string.Equals(prospect.ProspectKey, _selectedProspectKey, StringComparison.OrdinalIgnoreCase);
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            var label = $"{Truncate(prospect.PlayerName, 16)} | {prospect.PrimaryPosition} | {prospect.Country} | {prospect.ScoutingProgress}%";
            uiRenderer.DrawButton(label, bounds, color, Color.White, uiRenderer.UiSmallFont);
        }

        if (selectedProspect != null)
        {
            _toggleTargetButton.Label = selectedProspect.IsOnTargetList ? "Remove Target" : "Add Target";
            uiRenderer.DrawButton(_toggleTargetButton.Label, GetToggleTargetButtonBounds(), !suppressHover && GetToggleTargetButtonBounds().Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawButton(_openScoutReportButton.Label, GetOpenScoutReportButtonBounds(), !suppressHover && GetOpenScoutReportButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{selectedProspect.PlayerName} - {selectedProspect.PrimaryPosition}", new Rectangle(prospectReportPanelBounds.X + 12, prospectReportPanelBounds.Y + 62, prospectReportPanelBounds.Width - 24, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{selectedProspect.Source} | {selectedProspect.Country} | Age {selectedProspect.Age}", new Rectangle(prospectReportPanelBounds.X + 12, prospectReportPanelBounds.Y + 82, prospectReportPanelBounds.Width - 24, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"Found by: {selectedProspect.ScoutName} | Progress: {selectedProspect.ScoutingProgress}%", new Rectangle(prospectReportPanelBounds.X + 12, prospectReportPanelBounds.Y + 102, prospectReportPanelBounds.Width - 24, 18), Color.Goldenrod, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"Assigned scout: {selectedProspect.AssignedScoutName}", new Rectangle(prospectReportPanelBounds.X + 12, prospectReportPanelBounds.Y + 122, prospectReportPanelBounds.Width - 24, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds(shortReportText, new Rectangle(prospectReportPanelBounds.X + 12, prospectReportPanelBounds.Y + 148, prospectReportPanelBounds.Width - 24, prospectReportPanelBounds.Height - 160), Color.White, uiRenderer.UiSmallFont, 5);
        }

        if (_openScoutDropdown != ScoutDropdownType.None)
        {
            DrawScoutDropdown(uiRenderer);
        }
    }

    private bool TrySelectAssistantScout(Point mousePosition)
    {
        var assistantScouts = _franchiseSession.GetScoutDepartment().Where(entry => !entry.IsHeadScout).OrderBy(entry => entry.SlotIndex).ToList();
        for (var i = 0; i < assistantScouts.Count; i++)
        {
            if (!GetAssistantScoutRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _openScoutDropdown = ScoutDropdownType.None;
            _selectedScoutSlotIndex = assistantScouts[i].SlotIndex;
            _statusMessage = assistantScouts[i].IsVacant
                ? $"Scout {assistantScouts[i].SlotIndex + 1} is open. Hire a scout, then assign him to a region or player before reports start coming in."
                : $"{assistantScouts[i].Name}: {assistantScouts[i].AssignmentMode} - {assistantScouts[i].AssignmentTarget}.";
            return true;
        }

        return false;
    }

    private bool TrySelectProspect(Point mousePosition)
    {
        var prospects = GetPagedProspects().ToList();
        for (var i = 0; i < prospects.Count; i++)
        {
            if (!GetProspectRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedProspectKey = prospects[i].ProspectKey;
            _statusMessage = $"{prospects[i].PlayerName} is now highlighted on the amateur scouting board.";
            return true;
        }

        return false;
    }

    private void RefreshSelections(IReadOnlyList<ScoutingPlayerCard>? marketPlayers = null, IReadOnlyList<ScoutingPlayerCard>? offerPlayers = null, IReadOnlyList<CoachProfileView>? coaches = null)
    {
        coaches ??= _franchiseSession.GetCoachingStaff();
        marketPlayers ??= ApplyFilter(_franchiseSession.GetScoutingBoardPlayers());
        offerPlayers ??= _franchiseSession.GetTradeChipPlayers();

        if (coaches.Count > 0 && coaches.All(coach => !string.Equals(coach.Role, _selectedCoachRole, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedCoachRole = coaches[0].Role;
        }

        if (!_selectedPlayerId.HasValue || marketPlayers.All(player => player.PlayerId != _selectedPlayerId.Value))
        {
            _selectedPlayerId = marketPlayers.FirstOrDefault()?.PlayerId;
        }

        if (!_selectedOfferPlayerId.HasValue || offerPlayers.All(player => player.PlayerId != _selectedOfferPlayerId.Value))
        {
            _selectedOfferPlayerId = offerPlayers.FirstOrDefault()?.PlayerId;
        }

        ClampPages(marketPlayers.Count, offerPlayers.Count);
    }

    private string BuildShortScoutReport(ScoutAssignmentView? selectedScout, AmateurProspectView? selectedProspect)
    {
        if (selectedProspect != null)
        {
            return $"{selectedProspect.PlayerName} is {selectedProspect.ScoutingProgress}% scouted. {selectedProspect.Projection}. {selectedProspect.Summary}";
        }

        if (selectedScout == null || selectedScout.IsVacant)
        {
            return "Hire a scout, then assign him to a region or a player to start filling out this report.";
        }

        return string.Equals(selectedScout.AssignmentMode, "Unassigned", StringComparison.OrdinalIgnoreCase)
            ? $"{selectedScout.Name} is hired but still waiting for an assignment."
            : $"{selectedScout.Name} is currently working on {selectedScout.AssignmentTarget}.";
    }

    private string BuildExpandedScoutReport(ScoutAssignmentView? selectedScout, AmateurProspectView? selectedProspect)
    {
        if (selectedProspect != null)
        {
            var assignedScout = string.IsNullOrWhiteSpace(selectedProspect.AssignedScoutName) ? "No scout currently assigned" : selectedProspect.AssignedScoutName;
            return $"Prospect: {selectedProspect.PlayerName} ({selectedProspect.PrimaryPosition}, Age {selectedProspect.Age})\n\nSource: {selectedProspect.Source} in {selectedProspect.Country}.\nProgress: {selectedProspect.ScoutingProgress}% complete.\nFound by: {selectedProspect.ScoutName}.\nActive follow-up: {assignedScout}.\n\nProjection: {selectedProspect.Projection}.\n\nSummary: {selectedProspect.Summary}\n\nBonus note: {selectedProspect.EstimatedBonus}.";
        }

        if (selectedScout == null || selectedScout.IsVacant)
        {
            return "This slot is open. Hire a scout from the candidate popup, then assign him to a region search or to a specific player. New leads take time to appear, and any discovered players remain on your board even if that scout is later released.";
        }

        var timingNote = string.Equals(selectedScout.AssignmentMode, "Region Search", StringComparison.OrdinalIgnoreCase)
            ? $"Next lead is expected in about {selectedScout.DaysUntilNextDiscovery} day(s)."
            : string.Equals(selectedScout.AssignmentMode, "Player Follow", StringComparison.OrdinalIgnoreCase)
                ? "This scout is deepening an existing player file a little more each day."
                : "This scout is currently idle and waiting for a new assignment.";

        return $"Scout: {selectedScout.Name}\nSpecialty: {selectedScout.Specialty}\nVoice: {selectedScout.Voice}\nStatus: {selectedScout.AssignmentMode}\nTarget: {selectedScout.AssignmentTarget}\n\n{timingNote}\n\nAssign a region to discover new prospects over time, or assign a scout directly to a player to keep pushing that file toward 100%.";
    }

    private void ScrollProspectList(int direction)
    {
        var prospects = GetVisibleProspects();
        _prospectPageIndex = Math.Clamp(_prospectPageIndex + direction, 0, GetProspectMaxPage(prospects.Count));
    }

    private void ScrollScoutDropdown(int direction)
    {
        var maxScroll = Math.Max(0, GetOpenScoutDropdownOptions().Count - MaxVisibleDropdownOptions);
        _scoutDropdownScrollIndex = Math.Clamp(_scoutDropdownScrollIndex + direction, 0, maxScroll);
    }

    private void ScrollScoutHirePopup(int direction)
    {
        var maxScroll = Math.Max(0, _franchiseSession.GetAvailableAssistantScoutCandidates(_selectedScoutSlotIndex).Count - MaxVisibleHireCandidates);
        _hireScoutPopupScrollIndex = Math.Clamp(_hireScoutPopupScrollIndex + direction, 0, maxScroll);
    }

    private void SetProspectSortMode(ProspectSortMode sortMode)
    {
        _prospectSortMode = sortMode;
        _prospectPageIndex = 0;
        RefreshAmateurSelections();
    }

    private string ApplyProspectPositionSelection(string option)
    {
        _prospectPositionFilter = string.IsNullOrWhiteSpace(option) ? "All" : option;
        _prospectSortMode = ProspectSortMode.Position;
        _prospectPageIndex = 0;
        RefreshAmateurSelections();

        return string.Equals(_prospectPositionFilter, "All", StringComparison.OrdinalIgnoreCase)
            ? "Prospect board sorted by position."
            : $"Prospect board filtered to {_prospectPositionFilter} and sorted by scouting progress.";
    }

    private IReadOnlyList<CoachProfileView> GetVisibleScoutHireCandidates()
    {
        var candidates = _franchiseSession.GetAvailableAssistantScoutCandidates(_selectedScoutSlotIndex);
        _hireScoutPopupScrollIndex = Math.Clamp(_hireScoutPopupScrollIndex, 0, Math.Max(0, candidates.Count - MaxVisibleHireCandidates));
        return candidates.Skip(_hireScoutPopupScrollIndex).Take(MaxVisibleHireCandidates).ToList();
    }

    private IReadOnlyList<string> GetVisibleScoutDropdownOptions()
    {
        var options = GetOpenScoutDropdownOptions();
        _scoutDropdownScrollIndex = Math.Clamp(_scoutDropdownScrollIndex, 0, Math.Max(0, options.Count - MaxVisibleDropdownOptions));
        return options.Skip(_scoutDropdownScrollIndex).Take(MaxVisibleDropdownOptions).ToList();
    }

    private IReadOnlyList<AmateurProspectView> GetVisibleProspects()
    {
        var prospects = _franchiseSession.GetScoutedPlayers(_showTargetList);
        return _prospectSortMode switch
        {
            ProspectSortMode.Position => prospects
                .Where(prospect => PositionMatchesFilter(prospect.PrimaryPosition, _prospectPositionFilter))
                .OrderBy(prospect => string.Equals(_prospectPositionFilter, "All", StringComparison.OrdinalIgnoreCase) ? prospect.PrimaryPosition : string.Empty)
                .ThenByDescending(prospect => prospect.ScoutingProgress)
                .ThenBy(prospect => prospect.PlayerName)
                .ToList(),
            _ => prospects
                .OrderByDescending(prospect => prospect.ScoutingProgress)
                .ThenBy(prospect => prospect.PrimaryPosition)
                .ThenBy(prospect => prospect.PlayerName)
                .ToList()
        };
    }

    private IReadOnlyList<AmateurProspectView> GetPagedProspects(IReadOnlyList<AmateurProspectView>? prospects = null)
    {
        prospects ??= GetVisibleProspects();
        var pageSize = GetProspectPageSize();
        _prospectPageIndex = Math.Clamp(_prospectPageIndex, 0, GetProspectMaxPage(prospects.Count));
        return prospects.Skip(_prospectPageIndex * pageSize).Take(pageSize).ToList();
    }

    private void RefreshAmateurSelections(IReadOnlyList<ScoutAssignmentView>? assistantScouts = null, IReadOnlyList<AmateurProspectView>? prospects = null)
    {
        assistantScouts ??= _franchiseSession.GetScoutDepartment().Where(entry => !entry.IsHeadScout).OrderBy(entry => entry.SlotIndex).ToList();
        prospects ??= GetVisibleProspects();

        if (assistantScouts.Count > 0 && assistantScouts.All(scout => scout.SlotIndex != _selectedScoutSlotIndex))
        {
            _selectedScoutSlotIndex = assistantScouts[0].SlotIndex;
        }

        _prospectPageIndex = Math.Clamp(_prospectPageIndex, 0, GetProspectMaxPage(prospects.Count));

        if (string.IsNullOrWhiteSpace(_selectedProspectKey) || prospects.All(prospect => !string.Equals(prospect.ProspectKey, _selectedProspectKey, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedProspectKey = prospects.FirstOrDefault()?.ProspectKey;
        }
    }

    private void ClampPages(int marketCount, int offerCount)
    {
        _marketPageIndex = Math.Clamp(_marketPageIndex, 0, GetMaxPage(marketCount));
        _offerPageIndex = Math.Clamp(_offerPageIndex, 0, GetMaxPage(offerCount));
    }

    private static int GetMaxPage(int totalCount)
    {
        return Math.Max(0, (int)Math.Ceiling(totalCount / (double)PageSize) - 1);
    }

    private int GetProspectPageSize()
    {
        var panel = GetProspectListPanelBounds();
        var availableHeight = Math.Max(26, panel.Height - 112);
        return Math.Max(1, ((availableHeight - 26) / 30) + 1);
    }

    private int GetProspectMaxPage(int totalCount)
    {
        var pageSize = GetProspectPageSize();
        return Math.Max(0, (int)Math.Ceiling(totalCount / (double)pageSize) - 1);
    }

    private Rectangle GetViewModeButtonBounds() => new(_viewport.X - 300, 38, 240, 44);

    private Rectangle GetFilterButtonBounds() => new(_viewport.X - 520, 40, 200, 44);

    private string GetScoutPrimaryActionLabel(ScoutAssignmentView? selectedScout)
    {
        if (selectedScout == null || selectedScout.IsVacant)
        {
            return "Hire Scout";
        }

        return string.Equals(selectedScout.AssignmentMode, "Unassigned", StringComparison.OrdinalIgnoreCase)
            ? "Assign Scout"
            : "Reassign Scout";
    }

    private bool PositionMatchesFilter(string position, string filter)
    {
        if (string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(filter, "OF", StringComparison.OrdinalIgnoreCase))
        {
            return position is "LF" or "CF" or "RF" or "OF";
        }

        return string.Equals(position, filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDepartmentAssignmentLabel(ScoutAssignmentView scout)
    {
        if (scout.IsVacant || string.Equals(scout.AssignmentMode, "Unassigned", StringComparison.OrdinalIgnoreCase))
        {
            return "No assignment";
        }

        if (!string.IsNullOrWhiteSpace(scout.AssignmentTarget))
        {
            return scout.AssignmentTarget;
        }

        return string.Equals(scout.AssignmentMode, "Player Follow", StringComparison.OrdinalIgnoreCase)
            ? "Scouting player"
            : "Scouting region";
    }

    private Rectangle GetTradeButtonBounds() => new(_viewport.X - 300, 90, 240, 44);

    private int GetProspectListPanelWidth() => Math.Clamp(_viewport.X / 2, 420, 560);

    private int GetProspectRightColumnX() => 40 + GetProspectListPanelWidth() + 24;

    private int GetProspectRightColumnWidth() => Math.Max(320, _viewport.X - GetProspectRightColumnX() - 40);

    private Rectangle GetScoutDepartmentPanelBounds()
    {
        var totalWidth = GetProspectRightColumnWidth();
        var width = Math.Max(180, (totalWidth - 24) / 2);
        return new Rectangle(GetProspectRightColumnX(), 186, width, 220);
    }

    private Rectangle GetScoutAssignmentPanelBounds()
    {
        var departmentPanel = GetScoutDepartmentPanelBounds();
        var x = departmentPanel.Right + 24;
        var width = Math.Max(180, GetProspectRightColumnWidth() - departmentPanel.Width - 24);
        return new Rectangle(x, 186, width, 324);
    }

    private Rectangle GetAssistantScoutRowBounds(int index)
    {
        var panel = GetScoutDepartmentPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 116 + (index * 34), panel.Width - 24, 28);
    }

    private Rectangle GetHireScoutButtonBounds()
    {
        var panel = GetScoutAssignmentPanelBounds();
        var buttonWidth = Math.Max(120, (panel.Width - 36) / 2);
        return new Rectangle(panel.X + 12, panel.Y + 104, buttonWidth, 26);
    }

    private Rectangle GetReleaseScoutButtonBounds()
    {
        var panel = GetScoutAssignmentPanelBounds();
        var buttonWidth = Math.Max(120, (panel.Width - 36) / 2);
        return new Rectangle(panel.X + panel.Width - buttonWidth - 12, panel.Y + 104, buttonWidth, 26);
    }

    private Rectangle GetCountryFocusButtonBounds()
    {
        var panel = GetScoutAssignmentPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 136, panel.Width - 24, 24);
    }

    private Rectangle GetPositionFocusButtonBounds()
    {
        var panel = GetScoutAssignmentPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 166, panel.Width - 24, 24);
    }

    private Rectangle GetTraitFocusButtonBounds()
    {
        var panel = GetScoutAssignmentPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 196, panel.Width - 24, 24);
    }

    private Rectangle GetAssignRegionButtonBounds()
    {
        var panel = GetScoutAssignmentPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 226, panel.Width - 24, 26);
    }

    private Rectangle GetAssignSelectedPlayerButtonBounds()
    {
        var panel = GetScoutAssignmentPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 258, panel.Width - 24, 26);
    }

    private Rectangle GetClearAssignmentButtonBounds()
    {
        var panel = GetScoutAssignmentPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 290, panel.Width - 24, 26);
    }

    private Rectangle GetProspectListPanelBounds()
    {
        var top = 186;
        return new Rectangle(40, top, GetProspectListPanelWidth(), Math.Max(200, _viewport.Y - top - 20));
    }

    private Rectangle GetProspectReportPanelBounds()
    {
        var top = Math.Max(GetScoutDepartmentPanelBounds().Bottom, GetScoutAssignmentPanelBounds().Bottom) + 20;
        return new Rectangle(GetProspectRightColumnX(), top, GetProspectRightColumnWidth(), Math.Max(200, _viewport.Y - top - 20));
    }

    private Rectangle GetScoutedPlayersTabBounds()
    {
        var panel = GetProspectListPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 8, Math.Max(150, (panel.Width / 2) - 18), 28);
    }

    private Rectangle GetTargetListTabBounds()
    {
        var panel = GetProspectListPanelBounds();
        return new Rectangle(panel.X + panel.Width - Math.Max(150, (panel.Width / 2) - 18) - 12, panel.Y + 8, Math.Max(150, (panel.Width / 2) - 18), 28);
    }

    private Rectangle GetProspectSortMostScoutedBounds()
    {
        var panel = GetProspectListPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 42, 132, 24);
    }

    private Rectangle GetProspectSortPositionBounds()
    {
        var panel = GetProspectListPanelBounds();
        return new Rectangle(panel.X + 150, panel.Y + 42, 182, 24);
    }

    private Rectangle GetProspectPreviousBounds()
    {
        var panel = GetProspectListPanelBounds();
        return new Rectangle(panel.Right - 154, panel.Y + 42, 68, 24);
    }

    private Rectangle GetProspectNextBounds()
    {
        var panel = GetProspectListPanelBounds();
        return new Rectangle(panel.Right - 74, panel.Y + 42, 62, 24);
    }

    private Rectangle GetProspectPageLabelBounds()
    {
        var panel = GetProspectListPanelBounds();
        return new Rectangle(panel.Right - 248, panel.Y + 42, 88, 24);
    }

    private Rectangle GetProspectRowBounds(int index)
    {
        var panel = GetProspectListPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 100 + (index * 30), panel.Width - 24, 26);
    }

    private Rectangle GetToggleTargetButtonBounds()
    {
        var panel = GetProspectReportPanelBounds();
        var buttonWidth = Math.Max(120, (panel.Width - 36) / 2);
        return new Rectangle(panel.X + 12, panel.Y + 28, buttonWidth, 28);
    }

    private Rectangle GetOpenScoutReportButtonBounds()
    {
        var panel = GetProspectReportPanelBounds();
        var buttonWidth = Math.Max(120, (panel.Width - 36) / 2);
        return new Rectangle(panel.Right - buttonWidth - 12, panel.Y + 28, buttonWidth, 28);
    }

    private Rectangle GetScoutHirePopupBounds()
    {
        var candidates = _franchiseSession.GetAvailableAssistantScoutCandidates(_selectedScoutSlotIndex);
        var width = Math.Clamp(_viewport.X / 3, 420, 560);
        var visibleCount = Math.Max(1, Math.Min(candidates.Count, MaxVisibleHireCandidates));
        var height = Math.Clamp(120 + (visibleCount * 36), 180, _viewport.Y - 120);
        return new Rectangle((_viewport.X - width) / 2, Math.Max(96, (_viewport.Y - height) / 2), width, height);
    }

    private Rectangle GetScoutHireOptionBounds(int index)
    {
        var panel = GetScoutHirePopupBounds();
        return new Rectangle(panel.X + 16, panel.Y + 78 + (index * 36), panel.Width - 32, 30);
    }

    private Rectangle GetScoutHireCancelBounds()
    {
        var panel = GetScoutHirePopupBounds();
        return new Rectangle(panel.X + 16, panel.Bottom - 40, panel.Width - 32, 24);
    }

    private Rectangle GetScoutReportPopupBounds()
    {
        var width = Math.Clamp((int)(_viewport.X * 0.56f), 460, 760);
        var height = Math.Clamp((int)(_viewport.Y * 0.48f), 260, 420);
        return new Rectangle((_viewport.X - width) / 2, (_viewport.Y - height) / 2, width, height);
    }

    private Rectangle GetScoutReportPopupCloseBounds()
    {
        var panel = GetScoutReportPopupBounds();
        return new Rectangle(panel.X + 16, panel.Bottom - 38, panel.Width - 32, 24);
    }

    private Rectangle GetScoutDropdownAnchorBounds()
    {
        return _openScoutDropdown switch
        {
            ScoutDropdownType.Country => GetCountryFocusButtonBounds(),
            ScoutDropdownType.Position => GetPositionFocusButtonBounds(),
            ScoutDropdownType.Trait => GetTraitFocusButtonBounds(),
            ScoutDropdownType.Sort => GetProspectSortPositionBounds(),
            _ => Rectangle.Empty
        };
    }

    private Rectangle GetScoutDropdownOptionBounds(int index)
    {
        var anchor = GetScoutDropdownAnchorBounds();
        return new Rectangle(anchor.X + 2, anchor.Bottom + 6 + (index * 28), Math.Max(1, anchor.Width - 4), 24);
    }

    private Rectangle GetScoutDropdownPanelBounds()
    {
        var anchor = GetScoutDropdownAnchorBounds();
        var optionCount = GetOpenScoutDropdownOptions().Count;
        var visibleCount = Math.Max(1, Math.Min(optionCount, MaxVisibleDropdownOptions));
        return new Rectangle(anchor.X, anchor.Bottom + 4, anchor.Width, Math.Max(28, visibleCount * 28 + 4));
    }

    private void DrawFilterDropdown(UiRenderer uiRenderer)
    {
        var options = GetFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var bounds = GetFilterOptionBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = option == _filterMode;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton(GetFilterLabel(option), bounds, color, Color.White, uiRenderer.ScoreboardFont);
        }
    }

    private bool TrySelectFilterOption(Point mousePosition)
    {
        var options = GetFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            if (!GetFilterOptionBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _filterMode = options[i];
            _marketPageIndex = 0;
            _showFilterDropdown = false;
            return true;
        }

        _showFilterDropdown = false;
        return false;
    }

    private Rectangle GetFilterOptionBounds(int index)
    {
        var buttonBounds = GetFilterButtonBounds();
        return new Rectangle(buttonBounds.X, buttonBounds.Bottom + 4 + (index * 32), buttonBounds.Width, 28);
    }

    private static IReadOnlyList<ScoutingFilterMode> GetFilterOptions()
    {
        return
        [
            ScoutingFilterMode.All,
            ScoutingFilterMode.Hitters,
            ScoutingFilterMode.Power,
            ScoutingFilterMode.Speed,
            ScoutingFilterMode.Pitching,
            ScoutingFilterMode.LastOps,
            ScoutingFilterMode.LastEra
        ];
    }

    private List<ScoutingPlayerCard> ApplyFilter(IReadOnlyList<ScoutingPlayerCard> players)
    {
        return _filterMode switch
        {
            ScoutingFilterMode.Hitters => players.Where(player => player.PrimaryPosition is not "SP" and not "RP").OrderBy(player => player.TeamName).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.Power => players.Where(player => player.PrimaryPosition is not "SP" and not "RP").OrderByDescending(GetPowerValue).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.Speed => players.Where(player => player.PrimaryPosition is not "SP" and not "RP").OrderByDescending(GetSpeedValue).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.Pitching => players.Where(player => player.PrimaryPosition is "SP" or "RP").OrderByDescending(GetPitchingValue).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.LastOps => players.Where(player => player.PrimaryPosition is not "SP" and not "RP").OrderByDescending(GetLastOpsValue).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.LastEra => players.Where(player => player.PrimaryPosition is "SP" or "RP").OrderBy(GetLastEraValue).ThenBy(player => player.PlayerName).ToList(),
            _ => players.OrderBy(player => player.IsOnSelectedTeam).ThenBy(player => player.TeamName).ThenBy(player => player.PlayerName).ToList()
        };
    }

    private int GetPowerValue(ScoutingPlayerCard player)
    {
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).EffectivePowerRating;
    }

    private int GetSpeedValue(ScoutingPlayerCard player)
    {
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).EffectiveSpeedRating;
    }

    private int GetPitchingValue(ScoutingPlayerCard player)
    {
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).EffectivePitchingRating;
    }

    private double GetLastOpsValue(ScoutingPlayerCard player)
    {
        var stats = _franchiseSession.GetLastSeasonStats(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var obp = stats.PlateAppearances == 0 ? 0d : (stats.Hits + stats.Walks) / (double)stats.PlateAppearances;
        var singles = Math.Max(0, stats.Hits - stats.Doubles - stats.Triples - stats.HomeRuns);
        var totalBases = singles + (stats.Doubles * 2) + (stats.Triples * 3) + (stats.HomeRuns * 4);
        var slg = stats.AtBats == 0 ? 0d : totalBases / (double)stats.AtBats;
        return obp + slg;
    }

    private double GetLastEraValue(ScoutingPlayerCard player)
    {
        var stats = _franchiseSession.GetLastSeasonStats(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        return stats.InningsPitchedOuts == 0 ? double.MaxValue : (stats.EarnedRuns * 9d) / (stats.InningsPitchedOuts / 3d);
    }

    private string BuildLastSeasonSummary(ScoutingPlayerCard player)
    {
        var stats = _franchiseSession.GetLastSeasonStats(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        return player.PrimaryPosition is "SP" or "RP"
            ? $"Last season: {stats.EarnedRunAverageDisplay} ERA, {stats.WinLossDisplay}, {stats.StrikeoutsPitched} K in {stats.GamesPitched} G."
            : $"Last season: {stats.BattingAverageDisplay} AVG, {stats.HomeRuns} HR, {stats.RunsBattedIn} RBI, {stats.OpsDisplay} OPS in {stats.GamesPlayed} G.";
    }

    private static ScoutingFilterMode GetNextFilter(ScoutingFilterMode filterMode)
    {
        return filterMode switch
        {
            ScoutingFilterMode.All => ScoutingFilterMode.Hitters,
            ScoutingFilterMode.Hitters => ScoutingFilterMode.Power,
            ScoutingFilterMode.Power => ScoutingFilterMode.Speed,
            ScoutingFilterMode.Speed => ScoutingFilterMode.Pitching,
            ScoutingFilterMode.Pitching => ScoutingFilterMode.LastOps,
            ScoutingFilterMode.LastOps => ScoutingFilterMode.LastEra,
            _ => ScoutingFilterMode.All
        };
    }

    private static string GetFilterLabel(ScoutingFilterMode filterMode)
    {
        return filterMode switch
        {
            ScoutingFilterMode.All => "All Targets",
            ScoutingFilterMode.Hitters => "Hitters",
            ScoutingFilterMode.Power => "Power Bats",
            ScoutingFilterMode.Speed => "Speed",
            ScoutingFilterMode.Pitching => "Pitchers",
            ScoutingFilterMode.LastOps => "Last OPS",
            _ => "Last ERA"
        };
    }

    private static IEnumerable<string> WrapText(string text, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (candidate.Length <= maxCharacters)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }

            currentLine = word;
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            yield return currentLine;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private Rectangle GetCoachRowBounds(int index)
    {
        var margin = 40;
        var top = 210;
        var gap = 24;
        var availableWidth = Math.Max(960, _viewport.X - (margin * 2) - (gap * 2));
        var coachWidth = Math.Clamp((int)(availableWidth * 0.28f), 260, 360);
        return new Rectangle(margin, top + (index * 46), coachWidth, 38);
    }

    private Rectangle GetMarketRowBounds(int index)
    {
        var coachBounds = GetCoachRowBounds(0);
        var gap = 24;
        var x = coachBounds.Right + gap;
        var availableWidth = Math.Max(960, _viewport.X - 80 - (gap * 2));
        var coachWidth = coachBounds.Width;
        var marketWidth = Math.Clamp((int)(availableWidth * 0.32f), 280, 380);
        return new Rectangle(x, 210 + (index * 34), marketWidth, 30);
    }

    private Rectangle GetOfferRowBounds(int index)
    {
        var marketBounds = GetMarketRowBounds(0);
        var gap = 24;
        var x = marketBounds.Right + gap;
        return new Rectangle(x, 210 + (index * 34), Math.Max(300, _viewport.X - x - 40), 30);
    }

    private Rectangle GetMarketPreviousBounds() => new(GetMarketRowBounds(0).X, 490, 84, 34);

    private Rectangle GetMarketNextBounds() => new(GetMarketRowBounds(0).Right - 84, 490, 84, 34);

    private Rectangle GetOfferPreviousBounds() => new(GetOfferRowBounds(0).X, 490, 84, 34);

    private Rectangle GetOfferNextBounds() => new(GetOfferRowBounds(0).Right - 84, 490, 84, 34);

    private Rectangle GetReportPanelBounds()
    {
        var bottomY = Math.Max(548, _viewport.Y - 160);
        var width = Math.Max(520, (int)(_viewport.X * 0.62f));
        return new Rectangle(40, bottomY, Math.Min(width, _viewport.X - 420), 140);
    }

    private Rectangle GetMovesPanelBounds(Rectangle reportPanelBounds)
    {
        var x = reportPanelBounds.Right + 30;
        return new Rectangle(x, reportPanelBounds.Y, Math.Max(250, _viewport.X - x - 40), reportPanelBounds.Height);
    }

    private enum ScoutingFilterMode
    {
        All,
        Hitters,
        Power,
        Speed,
        Pitching,
        LastOps,
        LastEra
    }

    private enum ScoutDropdownType
    {
        None,
        Country,
        Position,
        Trait,
        Sort
    }

    private enum ProspectSortMode
    {
        MostScouted,
        Position
    }

    private enum ScoutingViewMode
    {
        TradeMarket,
        AmateurInternational
    }
}
