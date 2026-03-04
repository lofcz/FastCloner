using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FastCloner.Benchmark.CI.Reporting;

internal sealed record BenchmarkMeasurement(
    string Benchmark,
    string Library,
    string Method,
    string Category,
    double MeanNanoseconds,
    long AllocatedBytes);

internal sealed record BenchmarkStats(double MeanNanoseconds, long AllocatedBytes);

internal sealed record BenchmarkPair(string Benchmark, BenchmarkStats DeepCloner, BenchmarkStats FastCloner);

internal sealed record NormalizedBenchmarkDocument(
    DateTime GeneratedAtUtc,
    string SourceCsv,
    IReadOnlyList<BenchmarkMeasurement> Measurements,
    IReadOnlyList<BenchmarkPair> Pairs);

internal static class BenchmarkResultParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static IReadOnlyList<BenchmarkMeasurement> ParseCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
            throw new FileNotFoundException($"Benchmark CSV not found: {csvPath}");

        string[] lines = File.ReadAllLines(csvPath);
        if (lines.Length <= 1)
            return Array.Empty<BenchmarkMeasurement>();

        string[] headers = ParseCsvLine(lines[0]);
        int methodIndex = IndexOf(headers, "Method");
        int meanIndex = IndexOf(headers, "Mean");
        int allocatedIndex = IndexOf(headers, "Allocated");
        int categoryIndex = IndexOf(headers, "Categories");
        if (categoryIndex < 0)
            categoryIndex = IndexOf(headers, "Category");

        if (methodIndex < 0 || meanIndex < 0 || allocatedIndex < 0)
            throw new InvalidDataException("Benchmark CSV must contain Method, Mean and Allocated columns.");

        List<BenchmarkMeasurement> raw = new();
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] values = ParseCsvLine(line);
            string method = Read(values, methodIndex);
            if (!TryGetLibraryAndBenchmark(method, out string library, out string benchmark))
                continue;

            string category = categoryIndex >= 0 ? Read(values, categoryIndex) : string.Empty;
            double meanNs = ParseTimeToNanoseconds(Read(values, meanIndex));
            long allocatedBytes = ParseAllocatedBytes(Read(values, allocatedIndex));
            if (double.IsNaN(meanNs) || double.IsInfinity(meanNs))
                continue;

            raw.Add(new BenchmarkMeasurement(
                Benchmark: benchmark,
                Library: library,
                Method: method,
                Category: category,
                MeanNanoseconds: meanNs,
                AllocatedBytes: allocatedBytes));
        }

        return Consolidate(raw);
    }

    public static IReadOnlyList<BenchmarkPair> BuildPairs(IReadOnlyList<BenchmarkMeasurement> measurements)
    {
        Dictionary<string, BenchmarkStats> deepClonerByBenchmark = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BenchmarkStats> fastClonerByBenchmark = new(StringComparer.OrdinalIgnoreCase);

        foreach (BenchmarkMeasurement measurement in measurements)
        {
            if (measurement.Library.Equals("DeepCloner", StringComparison.OrdinalIgnoreCase))
            {
                deepClonerByBenchmark[measurement.Benchmark] =
                    new BenchmarkStats(measurement.MeanNanoseconds, measurement.AllocatedBytes);
                continue;
            }

            if (measurement.Library.Equals("FastCloner", StringComparison.OrdinalIgnoreCase))
            {
                fastClonerByBenchmark[measurement.Benchmark] =
                    new BenchmarkStats(measurement.MeanNanoseconds, measurement.AllocatedBytes);
            }
        }

        List<BenchmarkPair> pairs = new();
        foreach (KeyValuePair<string, BenchmarkStats> kvp in deepClonerByBenchmark)
        {
            if (!fastClonerByBenchmark.TryGetValue(kvp.Key, out BenchmarkStats? fastStats))
                continue;

            pairs.Add(new BenchmarkPair(
                Benchmark: kvp.Key,
                DeepCloner: kvp.Value,
                FastCloner: fastStats));
        }

        pairs.Sort(static (left, right) =>
            left.DeepCloner.MeanNanoseconds.CompareTo(right.DeepCloner.MeanNanoseconds));
        return pairs;
    }

    public static void SaveNormalized(string path, NormalizedBenchmarkDocument document)
    {
        EnsureParentDirectory(path);
        string json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static NormalizedBenchmarkDocument? TryLoadNormalized(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<NormalizedBenchmarkDocument>(json, JsonOptions);
    }

    private static IReadOnlyList<BenchmarkMeasurement> Consolidate(IReadOnlyList<BenchmarkMeasurement> raw)
    {
        Dictionary<string, MeasurementBucket> buckets = new(StringComparer.OrdinalIgnoreCase);
        foreach (BenchmarkMeasurement measurement in raw)
        {
            string key = $"{measurement.Benchmark}|{measurement.Library}";
            if (!buckets.TryGetValue(key, out MeasurementBucket? bucket) || bucket is null)
            {
                buckets[key] = new MeasurementBucket(
                    measurement.Benchmark,
                    measurement.Library,
                    measurement.Method,
                    measurement.Category,
                    measurement.MeanNanoseconds,
                    measurement.AllocatedBytes,
                    1);
                continue;
            }

            buckets[key] = bucket with
            {
                MeanNanosecondsSum = bucket.MeanNanosecondsSum + measurement.MeanNanoseconds,
                AllocatedBytesSum = bucket.AllocatedBytesSum + measurement.AllocatedBytes,
                Count = bucket.Count + 1
            };
        }

        List<BenchmarkMeasurement> consolidated = new();
        foreach (MeasurementBucket bucket in buckets.Values)
        {
            consolidated.Add(new BenchmarkMeasurement(
                Benchmark: bucket.Benchmark,
                Library: bucket.Library,
                Method: bucket.Method,
                Category: bucket.Category,
                MeanNanoseconds: bucket.MeanNanosecondsSum / bucket.Count,
                AllocatedBytes: bucket.AllocatedBytesSum / bucket.Count));
        }

        consolidated.Sort(static (left, right) => string.CompareOrdinal(left.Method, right.Method));
        return consolidated;
    }

    private static bool TryGetLibraryAndBenchmark(string method, out string library, out string benchmark)
    {
        const string deepPrefix = "DeepCloner_";
        const string fastPrefix = "FastCloner_";

        if (method.StartsWith(deepPrefix, StringComparison.OrdinalIgnoreCase))
        {
            library = "DeepCloner";
            benchmark = method[deepPrefix.Length..];
            return !string.IsNullOrWhiteSpace(benchmark);
        }

        if (method.StartsWith(fastPrefix, StringComparison.OrdinalIgnoreCase))
        {
            library = "FastCloner";
            benchmark = method[fastPrefix.Length..];
            return !string.IsNullOrWhiteSpace(benchmark);
        }

        library = string.Empty;
        benchmark = string.Empty;
        return false;
    }

    private static double ParseTimeToNanoseconds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return double.NaN;

        string cleaned = value.Trim();
        if (string.Equals(cleaned, "NA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cleaned, "-", StringComparison.OrdinalIgnoreCase))
        {
            return double.NaN;
        }

        string[] parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return double.NaN;

        if (!double.TryParse(
                parts[0].Replace(",", string.Empty),
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out double amount))
        {
            return double.NaN;
        }

        string unit = parts.Length > 1 ? parts[1] : "ns";
        return unit switch
        {
            "ns" => amount,
            "us" => amount * 1000d,
            "ms" => amount * 1_000_000d,
            "s" => amount * 1_000_000_000d,
            "μs" => amount * 1000d,
            "µs" => amount * 1000d,
            _ => amount
        };
    }

    private static long ParseAllocatedBytes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        string cleaned = value.Trim();
        if (string.Equals(cleaned, "NA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cleaned, "-", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string[] parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return 0;

        if (!double.TryParse(
                parts[0].Replace(",", string.Empty),
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out double amount))
        {
            return 0;
        }

        string unit = parts.Length > 1 ? parts[1] : "B";
        double multiplier = unit.ToUpperInvariant() switch
        {
            "B" => 1d,
            "KB" => 1024d,
            "MB" => 1024d * 1024d,
            "GB" => 1024d * 1024d * 1024d,
            _ => 1d
        };

        return Convert.ToInt64(Math.Round(amount * multiplier, MidpointRounding.AwayFromZero));
    }

    private static int IndexOf(string[] headers, string name)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string Read(string[] values, int index)
    {
        return index >= 0 && index < values.Length ? values[index].Trim() : string.Empty;
    }

    private static string[] ParseCsvLine(string line)
    {
        List<string> values = new();
        StringBuilder current = new();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private static void EnsureParentDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        Directory.CreateDirectory(directory);
    }

    private sealed record MeasurementBucket(
        string Benchmark,
        string Library,
        string Method,
        string Category,
        double MeanNanosecondsSum,
        long AllocatedBytesSum,
        int Count);
}
