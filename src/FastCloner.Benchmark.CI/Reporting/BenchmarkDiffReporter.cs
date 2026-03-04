using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FastCloner.Benchmark.CI.Reporting;

internal static class BenchmarkDiffReporter
{
    private const string ReportFlag = "--report";

    public static bool TryRun(string[] args, out int exitCode)
    {
        if (!args.Any(arg => string.Equals(arg, ReportFlag, StringComparison.OrdinalIgnoreCase)))
        {
            exitCode = 0;
            return false;
        }

        try
        {
            ReporterOptions options = ReporterOptions.Parse(args);
            RunReport(options);
            exitCode = 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Benchmark report generation failed: {ex.Message}");
            exitCode = 1;
        }

        return true;
    }

    private static void RunReport(ReporterOptions options)
    {
        IReadOnlyList<BenchmarkMeasurement> measurements = BenchmarkResultParser.ParseCsv(options.CsvPath);
        IReadOnlyList<BenchmarkPair> pairs = BenchmarkResultParser.BuildPairs(measurements);
        if (pairs.Count == 0)
            throw new InvalidDataException("No DeepCloner/FastCloner benchmark pairs were found in the CSV.");

        NormalizedBenchmarkDocument currentDocument = new(
            GeneratedAtUtc: DateTime.UtcNow,
            SourceCsv: options.CsvPath,
            Measurements: measurements,
            Pairs: pairs);

        BenchmarkResultParser.SaveNormalized(options.NormalizedJsonPath, currentDocument);

        string currentTable = BuildCurrentComparisonTable(pairs, options.SameThreshold);
        WriteFile(options.CurrentReportPath, currentTable);

        NormalizedBenchmarkDocument? baselineDocument = BenchmarkResultParser.TryLoadNormalized(options.BaselineJsonPath);
        BaselineDiffReport? diffReport = baselineDocument is null
            ? null
            : CreateDiffReport(currentDocument, baselineDocument, options.TimeThreshold, options.AllocThreshold, options.SameThreshold);

        if (!string.IsNullOrWhiteSpace(options.DiffJsonPath) && diffReport is not null)
        {
            string diffJson = JsonSerializer.Serialize(diffReport, new JsonSerializerOptions { WriteIndented = true });
            WriteFile(options.DiffJsonPath!, diffJson);
        }

        string commentMarkdown = BuildCommentMarkdown(options, currentTable, baselineDocument, diffReport);
        string summaryMarkdown = BuildSummaryMarkdown(options, baselineDocument, diffReport);

        WriteFile(options.CommentReportPath, commentMarkdown);
        WriteFile(options.SummaryReportPath, summaryMarkdown);

        Console.WriteLine($"Generated normalized results: {options.NormalizedJsonPath}");
        Console.WriteLine($"Generated current report: {options.CurrentReportPath}");
        Console.WriteLine($"Generated PR comment report: {options.CommentReportPath}");
        Console.WriteLine($"Generated workflow summary report: {options.SummaryReportPath}");
    }

    private static BaselineDiffReport CreateDiffReport(
        NormalizedBenchmarkDocument current,
        NormalizedBenchmarkDocument baseline,
        double timeThreshold,
        double allocThreshold,
        double sameThreshold)
    {
        Dictionary<string, BenchmarkPair> baselinePairs = baseline.Pairs
            .ToDictionary(pair => pair.Benchmark, StringComparer.OrdinalIgnoreCase);

        List<BaselineDiffItem> items = new();
        foreach (BenchmarkPair currentPair in current.Pairs)
        {
            if (!baselinePairs.TryGetValue(currentPair.Benchmark, out BenchmarkPair? baselinePair))
            {
                items.Add(new BaselineDiffItem(
                    Benchmark: currentPair.Benchmark,
                    BaselineMeanNanoseconds: null,
                    CurrentMeanNanoseconds: currentPair.FastCloner.MeanNanoseconds,
                    TimeDeltaRatio: null,
                    BaselineAllocatedBytes: null,
                    CurrentAllocatedBytes: currentPair.FastCloner.AllocatedBytes,
                    AllocDeltaRatio: null,
                    Status: DiffStatus.NewBenchmark));
                continue;
            }

            double? timeDelta = SafeDelta(currentPair.FastCloner.MeanNanoseconds, baselinePair.FastCloner.MeanNanoseconds);
            double? allocDelta = SafeDelta(currentPair.FastCloner.AllocatedBytes, baselinePair.FastCloner.AllocatedBytes);

            DiffStatus status = ClassifyDiff(timeDelta, allocDelta, timeThreshold, allocThreshold, sameThreshold);
            items.Add(new BaselineDiffItem(
                Benchmark: currentPair.Benchmark,
                BaselineMeanNanoseconds: baselinePair.FastCloner.MeanNanoseconds,
                CurrentMeanNanoseconds: currentPair.FastCloner.MeanNanoseconds,
                TimeDeltaRatio: timeDelta,
                BaselineAllocatedBytes: baselinePair.FastCloner.AllocatedBytes,
                CurrentAllocatedBytes: currentPair.FastCloner.AllocatedBytes,
                AllocDeltaRatio: allocDelta,
                Status: status));
        }

        items.Sort(static (left, right) => string.CompareOrdinal(left.Benchmark, right.Benchmark));
        return new BaselineDiffReport(
            GeneratedAtUtc: DateTime.UtcNow,
            BaselineGeneratedAtUtc: baseline.GeneratedAtUtc,
            TimeRegressionThreshold: timeThreshold,
            AllocRegressionThreshold: allocThreshold,
            SameThreshold: sameThreshold,
            Items: items);
    }

    private static DiffStatus ClassifyDiff(
        double? timeDelta,
        double? allocDelta,
        double timeThreshold,
        double allocThreshold,
        double sameThreshold)
    {
        if (timeDelta is null || allocDelta is null)
            return DiffStatus.NewBenchmark;

        bool timeRegression = timeDelta.Value > timeThreshold;
        bool allocRegression = allocDelta.Value > allocThreshold;
        bool timeImprovement = timeDelta.Value < -timeThreshold;
        bool allocImprovement = allocDelta.Value < -allocThreshold;

        bool hasRegression = timeRegression || allocRegression;
        bool hasImprovement = timeImprovement || allocImprovement;
        if (hasRegression && hasImprovement)
            return DiffStatus.Mixed;
        if (hasRegression)
            return DiffStatus.Regression;
        if (hasImprovement)
            return DiffStatus.Improvement;

        bool essentiallySame =
            Math.Abs(timeDelta.Value) <= sameThreshold &&
            Math.Abs(allocDelta.Value) <= sameThreshold;
        return essentiallySame ? DiffStatus.Stable : DiffStatus.Stable;
    }

    private static string BuildCurrentComparisonTable(IReadOnlyList<BenchmarkPair> pairs, double sameThreshold)
    {
        StringBuilder sb = new();
        sb.AppendLine("| Benchmark | DeepCloner | FastCloner | Delta Time | DC Alloc | FC Alloc | Delta Alloc |");
        sb.AppendLine("|---|---:|---:|---|---:|---:|---|");

        foreach (BenchmarkPair pair in pairs)
        {
            double? timeDelta = SafeDelta(pair.FastCloner.MeanNanoseconds, pair.DeepCloner.MeanNanoseconds);
            double? allocDelta = SafeDelta(pair.FastCloner.AllocatedBytes, pair.DeepCloner.AllocatedBytes);

            sb.AppendLine(
                $"| {pair.Benchmark} | {FormatNanoseconds(pair.DeepCloner.MeanNanoseconds)} | {FormatNanoseconds(pair.FastCloner.MeanNanoseconds)} | {FormatCurrentDelta(timeDelta, sameThreshold, "faster", "slower")} | {FormatBytes(pair.DeepCloner.AllocatedBytes)} | {FormatBytes(pair.FastCloner.AllocatedBytes)} | {FormatCurrentDelta(allocDelta, sameThreshold, "less", "more")} |");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildCommentMarkdown(
        ReporterOptions options,
        string currentTable,
        NormalizedBenchmarkDocument? baseline,
        BaselineDiffReport? diff)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Deep Clone Benchmarks");
        sb.AppendLine();
        sb.AppendLine($"- OS: `{options.OsLabel}`");
        sb.AppendLine($"- Generated (UTC): `{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine();
        sb.AppendLine("### Current FastCloner vs DeepCloner");
        sb.AppendLine();
        sb.AppendLine(currentTable);
        sb.AppendLine();

        if (baseline is null || diff is null)
        {
            sb.AppendLine("### Baseline diff");
            sb.AppendLine();
            sb.AppendLine("No baseline artifact was found for this OS yet, so diff against `next` is unavailable.");
            sb.AppendLine("A successful push run on `next` will publish the baseline used by future PR runs.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine("### FastCloner vs latest `next` baseline");
        sb.AppendLine();
        sb.AppendLine($"- Baseline generated (UTC): `{baseline.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine($"- Regression thresholds: time > `{FormatPercent(options.TimeThreshold)}`, alloc > `{FormatPercent(options.AllocThreshold)}`");
        sb.AppendLine();
        sb.AppendLine("| Benchmark | FC Time (Baseline) | FC Time (Current) | Delta Time | FC Alloc (Baseline) | FC Alloc (Current) | Delta Alloc | Status |");
        sb.AppendLine("|---|---:|---:|---|---:|---:|---|---|");

        foreach (BaselineDiffItem item in diff.Items)
        {
            sb.AppendLine(
                $"| {item.Benchmark} | {FormatNanoseconds(item.BaselineMeanNanoseconds)} | {FormatNanoseconds(item.CurrentMeanNanoseconds)} | {FormatCurrentDelta(item.TimeDeltaRatio, options.SameThreshold, "faster", "slower")} | {FormatBytes(item.BaselineAllocatedBytes)} | {FormatBytes(item.CurrentAllocatedBytes)} | {FormatCurrentDelta(item.AllocDeltaRatio, options.SameThreshold, "less", "more")} | {FormatStatus(item.Status)} |");
        }

        AppendStatusSection(sb, "Regressions", diff.Items.Where(item => item.Status == DiffStatus.Regression).ToList(), options.SameThreshold);
        AppendStatusSection(sb, "Improvements", diff.Items.Where(item => item.Status == DiffStatus.Improvement).ToList(), options.SameThreshold);
        AppendStatusSection(sb, "Mixed changes", diff.Items.Where(item => item.Status == DiffStatus.Mixed).ToList(), options.SameThreshold);

        return sb.ToString().TrimEnd();
    }

    private static string BuildSummaryMarkdown(
        ReporterOptions options,
        NormalizedBenchmarkDocument? baseline,
        BaselineDiffReport? diff)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Deep Clone Benchmark Summary");
        sb.AppendLine();
        sb.AppendLine($"- OS: `{options.OsLabel}`");
        sb.AppendLine($"- Current report: `{options.CurrentReportPath}`");
        sb.AppendLine($"- Normalized JSON: `{options.NormalizedJsonPath}`");

        if (baseline is null || diff is null)
        {
            sb.AppendLine("- Baseline diff: unavailable (no baseline artifact found)");
            return sb.ToString().TrimEnd();
        }

        int regressionCount = diff.Items.Count(item => item.Status == DiffStatus.Regression);
        int improvementCount = diff.Items.Count(item => item.Status == DiffStatus.Improvement);
        int mixedCount = diff.Items.Count(item => item.Status == DiffStatus.Mixed);
        int stableCount = diff.Items.Count(item => item.Status == DiffStatus.Stable);
        int newCount = diff.Items.Count(item => item.Status == DiffStatus.NewBenchmark);

        sb.AppendLine($"- Baseline generated (UTC): `{baseline.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine($"- Regressions: **{regressionCount}**");
        sb.AppendLine($"- Improvements: **{improvementCount}**");
        sb.AppendLine($"- Mixed changes: **{mixedCount}**");
        sb.AppendLine($"- Stable: **{stableCount}**");
        sb.AppendLine($"- New benchmarks: **{newCount}**");
        return sb.ToString().TrimEnd();
    }

    private static void AppendStatusSection(
        StringBuilder sb,
        string title,
        IReadOnlyList<BaselineDiffItem> items,
        double sameThreshold)
    {
        sb.AppendLine();
        sb.AppendLine($"#### {title}");
        if (items.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("- none");
            return;
        }

        sb.AppendLine();
        foreach (BaselineDiffItem item in items)
        {
            string timeDelta = FormatCurrentDelta(item.TimeDeltaRatio, sameThreshold, "faster", "slower");
            string allocDelta = FormatCurrentDelta(item.AllocDeltaRatio, sameThreshold, "less", "more");
            sb.AppendLine($"- `{item.Benchmark}`: time {timeDelta}, alloc {allocDelta}");
        }
    }

    private static double? SafeDelta(double current, double baseline)
    {
        if (baseline <= 0)
            return null;

        return (current - baseline) / baseline;
    }

    private static double? SafeDelta(long current, long baseline)
    {
        if (baseline <= 0)
            return null;

        return (current - (double)baseline) / baseline;
    }

    private static string FormatCurrentDelta(double? ratio, double sameThreshold, string betterWord, string worseWord)
    {
        if (ratio is null || double.IsNaN(ratio.Value) || double.IsInfinity(ratio.Value))
            return "n/a";

        double absolute = Math.Abs(ratio.Value);
        if (absolute <= sameThreshold)
            return "~same";

        double percent = Math.Round(absolute * 100d, MidpointRounding.AwayFromZero);
        if (ratio.Value < 0)
            return $"-{percent.ToString("0", CultureInfo.InvariantCulture)}% {betterWord}";

        return $"+{percent.ToString("0", CultureInfo.InvariantCulture)}% {worseWord}";
    }

    private static string FormatNanoseconds(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return "n/a";

        return $"{value.Value.ToString("N2", CultureInfo.InvariantCulture)} ns";
    }

    private static string FormatBytes(long? value)
    {
        if (value is null)
            return "n/a";

        return $"{value.Value.ToString("N0", CultureInfo.InvariantCulture)} B";
    }

    private static string FormatPercent(double ratio)
    {
        return $"{(ratio * 100d).ToString("0.##", CultureInfo.InvariantCulture)}%";
    }

    private static string FormatStatus(DiffStatus status)
    {
        return status switch
        {
            DiffStatus.Regression => "regression",
            DiffStatus.Improvement => "improvement",
            DiffStatus.Mixed => "mixed",
            DiffStatus.NewBenchmark => "new",
            _ => "stable"
        };
    }

    private static void WriteFile(string path, string contents)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, contents.TrimEnd() + Environment.NewLine);
    }

    private sealed class ReporterOptions
    {
        public required string CsvPath { get; init; }
        public required string NormalizedJsonPath { get; init; }
        public required string CurrentReportPath { get; init; }
        public required string SummaryReportPath { get; init; }
        public required string CommentReportPath { get; init; }
        public string? BaselineJsonPath { get; init; }
        public string? DiffJsonPath { get; init; }
        public required string OsLabel { get; init; }
        public required double TimeThreshold { get; init; }
        public required double AllocThreshold { get; init; }
        public required double SameThreshold { get; init; }

        public static ReporterOptions Parse(string[] args)
        {
            Dictionary<string, string> values = ParseArguments(args);

            return new ReporterOptions
            {
                CsvPath = Require(values, "--csv"),
                NormalizedJsonPath = Get(values, "--normalized-json", "benchmark-results/current-normalized.json"),
                CurrentReportPath = Get(values, "--current-report", "benchmark-results/current-report.md"),
                SummaryReportPath = Get(values, "--summary-report", "benchmark-results/summary.md"),
                CommentReportPath = Get(values, "--comment-report", "benchmark-results/pr-comment.md"),
                BaselineJsonPath = GetNullable(values, "--baseline-json"),
                DiffJsonPath = GetNullable(values, "--diff-json"),
                OsLabel = Get(values, "--os", "unknown"),
                TimeThreshold = GetDouble(values, "--time-threshold", 0.05),
                AllocThreshold = GetDouble(values, "--alloc-threshold", 0.05),
                SameThreshold = GetDouble(values, "--same-threshold", 0.02)
            };
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string token = args[i];
                if (!token.StartsWith("--", StringComparison.Ordinal))
                    continue;

                int equalsIndex = token.IndexOf('=');
                if (equalsIndex > 0)
                {
                    string key = token[..equalsIndex];
                    string value = token[(equalsIndex + 1)..];
                    values[key] = value;
                    continue;
                }

                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    values[token] = args[i + 1];
                    i++;
                    continue;
                }

                values[token] = "true";
            }

            return values;
        }

        private static string Require(Dictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Missing required argument: {key}");

            return value;
        }

        private static string Get(Dictionary<string, string> values, string key, string defaultValue)
        {
            return values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : defaultValue;
        }

        private static string? GetNullable(Dictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
                return null;

            return value;
        }

        private static double GetDouble(Dictionary<string, string> values, string key, double defaultValue)
        {
            if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;

            throw new ArgumentException($"Argument '{key}' must be a number, got '{value}'.");
        }
    }
}

internal sealed record BaselineDiffReport(
    DateTime GeneratedAtUtc,
    DateTime BaselineGeneratedAtUtc,
    double TimeRegressionThreshold,
    double AllocRegressionThreshold,
    double SameThreshold,
    IReadOnlyList<BaselineDiffItem> Items);

internal sealed record BaselineDiffItem(
    string Benchmark,
    double? BaselineMeanNanoseconds,
    double CurrentMeanNanoseconds,
    double? TimeDeltaRatio,
    long? BaselineAllocatedBytes,
    long CurrentAllocatedBytes,
    double? AllocDeltaRatio,
    DiffStatus Status);

internal enum DiffStatus
{
    NewBenchmark,
    Stable,
    Improvement,
    Regression,
    Mixed
}
