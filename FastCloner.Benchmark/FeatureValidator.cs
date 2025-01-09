namespace FastCloner.Benchmark;

public class FeatureValidator
{
    public static void ValidateDictionary()
    {
        new FeatureDictionary().ValidateHashCodesAfterCloning();
    }
}