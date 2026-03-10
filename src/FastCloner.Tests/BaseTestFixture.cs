namespace FastCloner.Tests;

/// <summary>
/// Base class for test classes that should run with multiple MaxRecursionDepth values.
/// Inherit from this class to run the derived test class with both low and high recursion-depth settings.
/// </summary>
[Arguments(Low)]
[Arguments(High)]
public abstract class BaseTestFixture
{
    public const int Low = 1;
    public const int High = 1_000;
    
    protected int MaxRecursionDepth { get; private set; }

    protected BaseTestFixture(int maxRecursionDepth)
    {
        MaxRecursionDepth = maxRecursionDepth;
    }

    [Before(Test)]
    public void BaseSetUp()
    {
        FastCloner.MaxRecursionDepth = MaxRecursionDepth;
    }

    [After(Test)]
    public void BaseTearDown()
    {
        // Reset to default
        FastCloner.MaxRecursionDepth = 1000;
    }
}
