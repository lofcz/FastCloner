using Force.DeepCloner;

namespace FastCloner.Benchmark;

using System;
using System.Collections.Generic;

public class FeatureDictionary
{
    public void ValidateHashCodesAfterCloning()
    {
        // Arrange
        Dictionary<SimpleKey, string> originalDict = new Dictionary<SimpleKey, string>();
        SimpleKey key1 = new SimpleKey { Id = 1, Name = "Test1" };
        originalDict.Add(key1, "Value1");
        
        // Act - test všech implementací
        TestCloner("FastCloner", () => TryClone(() => global::FastCloner.FastCloner.DeepClone(originalDict)));
        TestCloner("DeepCopier", () => TryClone(() => global::DeepCopier.Copier.Copy(originalDict)));
        TestCloner("DeepCopy", () => TryClone(() => global::DeepCopy.DeepCopier.Copy(originalDict)));
        TestCloner("DeepCopyExpression", () => TryClone(() => global::DeepCopy.ObjectCloner.Clone(originalDict)));
        TestCloner("FastDeepCloner", () => TryClone(() => global::FastDeepCloner.DeepCloner.Clone(originalDict)));
        TestCloner("DeepCloner", () => TryClone(() => originalDict.DeepClone()));
    }

    private static void TestCloner(string clonerName, Func<(bool success, Dictionary<SimpleKey, string> clonedDict)> cloneFunction)
    {
        try
        {
            // Act
            (bool success, Dictionary<SimpleKey, string> clonedDict) = cloneFunction();

            if (!success)
            {
                Console.WriteLine($"{clonerName}: ❌");
                return;
            }

            // Arrange
            KeyValuePair<SimpleKey, string> searchKey = clonedDict.First();

            // Assert
            if (clonedDict.TryGetValue(searchKey.Key, out string? value) && value == "Value1")
            {
                Console.WriteLine($"{clonerName}: ✅");
            }
            else
            {
                Console.WriteLine($"{clonerName}: ❌");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{clonerName}: ❌ (Exception: {ex.Message})");
        }
    }

    private (bool success, Dictionary<SimpleKey, string>? clonedDict) TryClone(Func<Dictionary<SimpleKey, string>> cloneFunction)
    {
        try
        {
            Dictionary<SimpleKey, string> clonedDict = cloneFunction();
            return (true, clonedDict);
        }
        catch
        {
            return (false, null);
        }
    }
}