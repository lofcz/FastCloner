namespace FastCloner.Tests;

public static class Extensions
{
    public static IReadOnlySet<T> AsReadOnly<T>(this HashSet<T> set)
    {
        return new SpecialCaseTests.ReadOnlySet<T>(set);
    }
}
