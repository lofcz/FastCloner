# FastCloner.Benchmark.CI

This benchmark project runs against new changes and compares running performance between:

- `DeepCloner` (Force.DeepCloner) - benchmark baseline
- `FastCloner` (reflection deep clone API)

The suite covers a few categories (small/medium/large graphs, dynamic payloads, arrays, lists, dictionaries) with deterministic seeded data.

## Run Locally

From repository root:

```powershell
dotnet run -c Release --project src/FastCloner.Benchmark.CI/FastCloner.Benchmark.CI.csproj -- --filter *DeepCloneBenchmarks*
```

List available benchmarks:

```powershell
dotnet run -c Release --project src/FastCloner.Benchmark.CI/FastCloner.Benchmark.CI.csproj -- --list tree
```

## Generate Normalized Reports From Raw CSV

After a benchmark run, BenchmarkDotNet writes CSV files under `BenchmarkDotNet.Artifacts/results`.

```powershell
dotnet run -c Release --project src/FastCloner.Benchmark.CI/FastCloner.Benchmark.CI.csproj -- --report `
  --csv BenchmarkDotNet.Artifacts/results/FastCloner.Benchmark.CI.DeepCloneBenchmarks-report.csv `
  --normalized-json benchmark-results/current-normalized.json `
  --current-report benchmark-results/current-report.md `
  --summary-report benchmark-results/summary.md `
  --comment-report benchmark-results/pr-comment.md
```

Optional baseline diff (FastCloner current vs prior baseline):

```powershell
dotnet run -c Release --project src/FastCloner.Benchmark.CI/FastCloner.Benchmark.CI.csproj -- --report `
  --csv BenchmarkDotNet.Artifacts/results/FastCloner.Benchmark.CI.DeepCloneBenchmarks-report.csv `
  --baseline-json baseline/current-normalized.json `
  --normalized-json benchmark-results/current-normalized.json `
  --current-report benchmark-results/current-report.md `
  --summary-report benchmark-results/summary.md `
  --comment-report benchmark-results/pr-comment.md `
  --diff-json benchmark-results/baseline-diff.json
```

## CI Integration

Workflow: `.github/workflows/benchmark.yml`

- Push/PR to `next`: runs on `ubuntu-latest` by default.
- Manual run (`workflow_dispatch`): OS checkboxes allow running on Windows, Ubuntu, and macOS.
- PR runs post an upserted comment with:
  - current `FastCloner` vs `DeepCloner` table
  - regression/improvement diff against the latest successful baseline from `master`
- Baseline resolution order for PR runs:
  - use the latest successful uploaded baseline artifact for the target OS from `master`
  - if no artifact is available, fall back to a slow path that clones `master`, runs the benchmark suite there, and generates a fresh normalized baseline before comparing

