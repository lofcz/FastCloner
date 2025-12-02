namespace FastCloner.Tests;

/// <summary>
/// Base class for test fixtures that should run with multiple MaxRecursionDepth values.
/// Inherit from this class and add [TestFixture(1)] and [TestFixture(1000)] to your test class.
/// </summary>
public abstract class BaseTestFixture
{
    public const int Low = 1;
    public const int High = 1_000;
    
    protected int MaxRecursionDepth { get; private set; }

    protected BaseTestFixture(int maxRecursionDepth)
    {
        MaxRecursionDepth = maxRecursionDepth;
    }

    [OneTimeSetUp]
    public void BaseOneTimeSetUp()
    {
        FastCloner.MaxRecursionDepth = MaxRecursionDepth;
    }

    [OneTimeTearDown]
    public void BaseOneTimeTearDown()
    {
        // Reset to default
        FastCloner.MaxRecursionDepth = 1000;
    }
}

