namespace BaseballManager.Sim.Baserunning;

public sealed class BaserunnerState
{
    public Guid? FirstBaseRunnerId { get; set; }

    public Guid? SecondBaseRunnerId { get; set; }

    public Guid? ThirdBaseRunnerId { get; set; }

    public bool HasRunnerOnFirst => FirstBaseRunnerId.HasValue;

    public bool HasRunnerOnSecond => SecondBaseRunnerId.HasValue;

    public bool HasRunnerOnThird => ThirdBaseRunnerId.HasValue;

    public void Clear()
    {
        FirstBaseRunnerId = null;
        SecondBaseRunnerId = null;
        ThirdBaseRunnerId = null;
    }
}
