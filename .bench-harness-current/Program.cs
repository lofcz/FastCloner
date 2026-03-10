using System.Diagnostics;
using FastCloner;

var results = new List<(string Name, double Ms, double NsPerOp)>();
Measure("SmallObject x100000", CreateSmallObject(), 2000, 100000, static x => FastCloner.FastCloner.DeepClone((SmallObject)x)!);
Measure("StringArray1000 x20000", CreateStringArray(1000), 500, 20000, static x => FastCloner.FastCloner.DeepClone((string[])x)!);
Measure("Dictionary50 x5000", CreateDictionary(50), 200, 5000, static x => FastCloner.FastCloner.DeepClone((Dictionary<string, SmallObject>)x)!);
foreach (var (name, ms, nsPerOp) in results)
    Console.WriteLine($"{name}|{ms:F2}|{nsPerOp:F1}");

void Measure(string name, object value, int warmup, int iterations, Func<object, object> clone)
{
    for (var i = 0; i < warmup; i++) _ = clone(value);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var sw = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++) _ = clone(value);
    sw.Stop();
    results.Add((name, sw.Elapsed.TotalMilliseconds, sw.Elapsed.TotalMilliseconds * 1_000_000d / iterations));
}

static SmallObject CreateSmallObject() => new() { Id = 123, Name = "small-object-name", CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, Score = 42.5 };
static string[] CreateStringArray(int count) { var arr = new string[count]; for (var i = 0; i < count; i++) arr[i] = $"value-{i}"; return arr; }
static Dictionary<string, SmallObject> CreateDictionary(int count) { var dict = new Dictionary<string, SmallObject>(count); for (var i = 0; i < count; i++) dict[$"key-{i}"] = new SmallObject { Id = i, Name = $"item-{i}", CreatedAt = new DateTime(2025, 1, 1).AddMinutes(i), IsActive = (i & 1) == 0, Score = i * 1.25 }; return dict; }

public sealed class SmallObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public double Score { get; set; }
}
