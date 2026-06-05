public static class RunRandom
{
    public static int CreateSeed()
    {
        unchecked
        {
            int seed = System.Environment.TickCount;
            seed = seed * 397 ^ System.Guid.NewGuid().GetHashCode();
            return seed == 0 ? 1 : seed;
        }
    }

    public static int Range(int seed, int step, int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            return minInclusive;

        uint range = (uint)(maxExclusive - minInclusive);
        uint value = NextUInt(seed, step);
        return minInclusive + (int)(value % range);
    }

    private static uint NextUInt(int seed, int step)
    {
        unchecked
        {
            uint x = (uint)seed + 0x9E3779B9u * (uint)(step + 1);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }
    }
}
