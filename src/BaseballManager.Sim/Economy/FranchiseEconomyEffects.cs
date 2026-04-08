namespace BaseballManager.Sim.Economy;

public static class FranchiseEconomyEffects
{
    public static int GetScoutingVariance(decimal scoutingBudget)
    {
        return scoutingBudget switch
        {
            >= 900_000m => 1,
            >= 650_000m => 2,
            >= 400_000m => 4,
            >= 250_000m => 6,
            _ => 8
        };
    }

    public static double GetDevelopmentMultiplier(decimal developmentBudget, int facilitiesLevel)
    {
        var budgetFactor = developmentBudget switch
        {
            >= 1_000_000m => 1.30d,
            >= 700_000m => 1.18d,
            >= 450_000m => 1.08d,
            >= 250_000m => 1.00d,
            _ => 0.90d
        };

        var facilitiesFactor = 1d + ((Math.Clamp(facilitiesLevel, 1, 5) - 2) * 0.04d);
        return budgetFactor * facilitiesFactor;
    }

    public static double GetMedicalRecoveryMultiplier(decimal medicalBudget, int facilitiesLevel)
    {
        var budgetFactor = medicalBudget switch
        {
            >= 900_000m => 1.28d,
            >= 650_000m => 1.18d,
            >= 400_000m => 1.08d,
            >= 225_000m => 1.00d,
            _ => 0.90d
        };

        var facilitiesFactor = 1d + ((Math.Clamp(facilitiesLevel, 1, 5) - 2) * 0.03d);
        return budgetFactor * facilitiesFactor;
    }

    public static int GetFanInterestDelta(bool wonGame, int runDifferential)
    {
        var swing = runDifferential switch
        {
            >= 5 => 2,
            <= -5 => -2,
            _ => 1
        };

        return wonGame ? swing : -swing;
    }
}
