namespace BaseballManager.Core.Drafts;

public sealed class DraftState
{
    private readonly List<string> _draftOrder;
    private readonly List<DraftProspect> _availableProspects;
    private readonly List<DraftPick> _draftedPicks;

    private DraftState(
        IReadOnlyList<string> draftOrder,
        IReadOnlyList<DraftProspect> availableProspects,
        IReadOnlyList<DraftPick> draftedPicks,
        int totalRounds,
        bool isSnakeDraft,
        int currentRound,
        int currentPickNumber)
    {
        if (draftOrder.Count == 0)
        {
            throw new ArgumentException("Draft order must contain at least one team.", nameof(draftOrder));
        }

        if (totalRounds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRounds), "Draft must contain at least one round.");
        }

        _draftOrder = draftOrder
            .Where(teamName => !string.IsNullOrWhiteSpace(teamName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _availableProspects = availableProspects.ToList();
        _draftedPicks = draftedPicks.ToList();
        TotalRounds = totalRounds;
        IsSnakeDraft = isSnakeDraft;
        CurrentRound = currentRound;
        CurrentPickNumber = currentPickNumber;

        ValidateProspectPool();
        ValidatePicks();
        NormalizeClock();
    }

    public int TotalRounds { get; }

    public bool IsSnakeDraft { get; }

    public int CurrentRound { get; private set; }

    public int CurrentPickNumber { get; private set; }

    public IReadOnlyList<string> DraftOrder => _draftOrder;

    public IReadOnlyList<DraftProspect> AvailableProspects => _availableProspects;

    public IReadOnlyList<DraftPick> DraftedPicks => _draftedPicks;

    public bool IsComplete => CurrentRound > TotalRounds || _availableProspects.Count == 0 || _draftedPicks.Count >= MaximumPickCount;

    public string CurrentTeamName => IsComplete
        ? string.Empty
        : GetDraftOrderForRound(CurrentRound)[CurrentPickNumber - 1];

    public int MaximumPickCount => _draftOrder.Count * TotalRounds;

    public static DraftState Create(IReadOnlyList<string> draftOrder, IReadOnlyList<DraftProspect> availableProspects, int totalRounds, bool isSnakeDraft = false)
    {
        return new DraftState(draftOrder, availableProspects, [], totalRounds, isSnakeDraft, currentRound: 1, currentPickNumber: 1);
    }

    public static DraftState Restore(
        IReadOnlyList<string> draftOrder,
        IReadOnlyList<DraftProspect> availableProspects,
        IReadOnlyList<DraftPick> draftedPicks,
        int totalRounds,
        bool isSnakeDraft,
        int currentRound,
        int currentPickNumber)
    {
        return new DraftState(draftOrder, availableProspects, draftedPicks, totalRounds, isSnakeDraft, currentRound, currentPickNumber);
    }

    public IReadOnlyList<string> GetDraftOrderForRound(int roundNumber)
    {
        if (roundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber));
        }

        if (!IsSnakeDraft || roundNumber % 2 == 1)
        {
            return _draftOrder;
        }

        return _draftOrder.AsEnumerable().Reverse().ToList();
    }

    public DraftProspect? FindProspect(Guid playerId)
    {
        return _availableProspects.FirstOrDefault(prospect => prospect.PlayerId == playerId);
    }

    public bool IsTeamOnClock(string teamName)
    {
        return !IsComplete && string.Equals(CurrentTeamName, teamName, StringComparison.OrdinalIgnoreCase);
    }

    public DraftPick MakePick(string teamName, Guid playerId, bool isUserPick)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("The draft has already been completed.");
        }

        if (!IsTeamOnClock(teamName))
        {
            throw new InvalidOperationException($"{teamName} is not currently on the clock.");
        }

        var selectedProspect = FindProspect(playerId);
        if (selectedProspect == null)
        {
            throw new InvalidOperationException("That prospect is no longer available.");
        }

        if (_draftedPicks.Any(pick => pick.PlayerId == playerId))
        {
            throw new InvalidOperationException("That prospect has already been drafted.");
        }

        var draftPick = new DraftPick(
            CurrentRound,
            CurrentPickNumber,
            _draftedPicks.Count + 1,
            CurrentTeamName,
            selectedProspect.PlayerId,
            selectedProspect.PlayerName,
            selectedProspect.PrimaryPosition,
            selectedProspect.OverallRating,
            isUserPick);

        _availableProspects.Remove(selectedProspect);
        _draftedPicks.Add(draftPick);
        AdvanceClock();
        return draftPick;
    }

    private void AdvanceClock()
    {
        if (_draftedPicks.Count >= MaximumPickCount || _availableProspects.Count == 0)
        {
            CurrentRound = TotalRounds + 1;
            CurrentPickNumber = 0;
            return;
        }

        CurrentPickNumber++;
        if (CurrentPickNumber <= _draftOrder.Count)
        {
            return;
        }

        CurrentRound++;
        CurrentPickNumber = 1;
        if (CurrentRound > TotalRounds)
        {
            CurrentPickNumber = 0;
        }
    }

    private void ValidateProspectPool()
    {
        var duplicateProspectId = _availableProspects
            .GroupBy(prospect => prospect.PlayerId)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateProspectId != null && duplicateProspectId != Guid.Empty)
        {
            throw new InvalidOperationException("Draft prospect pool contains duplicate players.");
        }
    }

    private void ValidatePicks()
    {
        var duplicatePickId = _draftedPicks
            .GroupBy(pick => pick.PlayerId)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicatePickId != null && duplicatePickId != Guid.Empty)
        {
            throw new InvalidOperationException("Draft pick history contains duplicate players.");
        }

        if (_draftedPicks.Any(pick => _availableProspects.Any(prospect => prospect.PlayerId == pick.PlayerId)))
        {
            throw new InvalidOperationException("Drafted players cannot remain in the available pool.");
        }
    }

    private void NormalizeClock()
    {
        if (_draftedPicks.Count >= MaximumPickCount || _availableProspects.Count == 0)
        {
            CurrentRound = TotalRounds + 1;
            CurrentPickNumber = 0;
            return;
        }

        if (CurrentRound < 1)
        {
            CurrentRound = 1;
        }

        if (CurrentPickNumber < 1)
        {
            CurrentPickNumber = 1;
        }

        if (CurrentRound > TotalRounds)
        {
            CurrentRound = TotalRounds + 1;
            CurrentPickNumber = 0;
            return;
        }

        if (CurrentPickNumber > _draftOrder.Count)
        {
            CurrentPickNumber = _draftOrder.Count;
        }
    }
}
