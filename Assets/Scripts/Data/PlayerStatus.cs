public class PlayerStatus : PlayerState
{
    public int RunRandomSeed { get; private set; }
    public int RunRandomStep { get; private set; }

    public PlayerStatus(int maxHealth = 50, int gold = 0) : base(maxHealth, gold)
    {
        ResetRunRandom();
    }

    public void SetRunRandomState(int seed, int step)
    {
        RunRandomSeed = seed != 0 ? seed : RunRandom.CreateSeed();
        RunRandomStep = step < 0 ? 0 : step;
    }

    public void ResetRunRandom()
    {
        SetRunRandomState(RunRandom.CreateSeed(), 0);
    }

    public int NextRunRandomInt(int minInclusive, int maxExclusive)
    {
        int value = RunRandom.Range(RunRandomSeed, RunRandomStep, minInclusive, maxExclusive);
        RunRandomStep++;
        return value;
    }

    public static PlayerStatus CreateDefaultStatus()
    {
        string configId = string.IsNullOrEmpty(SelectedStartConfigId) ? "balanced" : SelectedStartConfigId;
        return CreateFromConfigStatus(configId);
    }

    public static PlayerStatus CreateFromConfigStatus(string configId)
    {
        PlayerState state = CreateFromConfig(configId);
        return state as PlayerStatus;
    }
}
