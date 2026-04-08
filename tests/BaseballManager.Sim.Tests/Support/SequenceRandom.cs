namespace BaseballManager.Sim.Tests.Support;

internal sealed class SequenceRandom : Random
{
    private readonly Queue<int> _nextValues;
    private readonly Queue<double> _nextDoubleValues;

    public SequenceRandom(IEnumerable<int>? nextValues = null, IEnumerable<double>? nextDoubleValues = null)
    {
        _nextValues = new Queue<int>(nextValues ?? Array.Empty<int>());
        _nextDoubleValues = new Queue<double>(nextDoubleValues ?? Array.Empty<double>());
    }

    public override int Next(int maxValue)
    {
        if (maxValue <= 1)
        {
            return 0;
        }

        if (_nextValues.Count == 0)
        {
            return 0;
        }

        return Math.Clamp(_nextValues.Dequeue(), 0, maxValue - 1);
    }

    public override int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
        {
            return minValue;
        }

        return minValue + Next(maxValue - minValue);
    }

    public override double NextDouble()
    {
        if (_nextDoubleValues.Count == 0)
        {
            return 0d;
        }

        return Math.Clamp(_nextDoubleValues.Dequeue(), 0d, 0.9999999999999999d);
    }

    protected override double Sample() => NextDouble();
}
