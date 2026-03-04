namespace FastCloner.Profiling;

public static class Program
{
    public static void Main(string[] args)
    {
        if (ProfilingCore.HasFlag(args, "--dynamic-with-array"))
        {
            DynamicWithArrayProfiler.Run(args);
            return;
        }

        LargeExpandoProfiler.Run(args);
    }
}
