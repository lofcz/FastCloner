# Benchmark results

FastCloner aims to _work correctly_ in the first place. It's currently reflection based, with opt-in source generator for increased performance. The benchmarking is made harder by the fact that many competing libraries behave incorrectly for scenarios that would be interested to bechmark. For example, only a few libraries deep clone dictionaries correctly. Broadly speaking, FastCloner in reflection mode is _fast_ in the reflection category, lags behind IL generation (when it works) and source generators.

We alocate some extra memory, but this is mostly one-time price for various lookup tables.

### Simple Dictionary:

| Method               | Mean     | Error   | StdDev  | Ratio | RatioSD | Rank | Gen0    | Gen1    | Allocated | Alloc Ratio |
|----------------------|---------:|--------:|--------:|------:|--------:|-----:|--------:|--------:|----------:|------------:|
| DeepCloner**         | 111.8 us | 1.55 us | 1.45 us |  0.58 |    0.03 |    1 | 26.7334 |  8.7891 | 164.51 KB |        0.46 |
| FastDeepCloner       | 117.4 us | 1.36 us | 1.20 us |  0.61 |    0.03 |    2 | 16.3574 |  4.0283 | 100.79 KB |        0.28 |
| ⭐ FastCloner         | 191.9 us | 3.81 us | 9.13 us |  1.00 |    0.07 |    3 | 58.5938 |  1.9531 | 359.94 KB |        1.00 |
| DeepCopy**           | 211.9 us | 3.53 us | 3.30 us |  1.11 |    0.05 |    4 | 50.2930 | 24.9023 | 310.47 KB |        0.86 |
| DeepCopyExpression** | 241.5 us | 4.69 us | 7.02 us |  1.26 |    0.07 |    5 | 38.3301 |  9.5215 | 235.23 KB |        0.65 |
| DeepCopier (crashes) |       NA |      NA |      NA |     ? |       ? |    ? |      NA |      NA |        NA |           ? |

** clones items incorrectly, unless hash code is overriden.

### Minimal:

| Method             | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------|------------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DeepCopier         |    23.48 ns |  0.525 ns |  0.998 ns |  0.15 |    0.01 |    1 | 0.0115 |      72 B |        0.20 |
| DeepCopy           |    41.55 ns |  0.751 ns |  0.702 ns |  0.27 |    0.01 |    2 | 0.0114 |      72 B |        0.20 |
| DeepCloner         |   134.54 ns |  2.654 ns |  3.057 ns |  0.87 |    0.03 |    3 | 0.0370 |     232 B |        0.64 |
| ⭐ FastCloner       |   155.21 ns |  3.007 ns |  4.215 ns |  1.00 |    0.04 |    4 | 0.0572 |     360 B |        1.00 |
| DeepCopyExpression |   169.37 ns |  3.269 ns |  3.498 ns |  1.09 |    0.04 |    5 | 0.0393 |     248 B |        0.69 |
| FastDeepCloner     | 2,210.93 ns | 43.556 ns | 58.146 ns | 14.26 |    0.53 |    6 | 0.2060 |    1296 B |        3.60 |

### Array 2D Big

| Method               | Mean       | Error    | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------|-----------:|---------:|----------:|-----------:|------:|--------:|-----:|---------:|---------:|---------:|----------:|------------:|
| DeepCopyExpression   |   657.1 us | 15.29 us |  44.11 us |   652.0 us |  0.78 |    0.06 |    1 | 395.5078 | 394.5313 | 394.5313 |   3.81 MB |        1.00 |
| FastDeepCloner       |   663.9 us | 19.89 us |  57.38 us |   646.3 us |  0.79 |    0.07 |    1 | 399.4141 | 398.4375 | 398.4375 |   3.82 MB |        1.00 |
| ArrayCopy            |   784.2 us | 14.09 us |  18.81 us |   783.6 us |  0.93 |    0.04 |    2 | 281.2500 | 281.2500 | 281.2500 |   3.81 MB |        1.00 |
| DeepCloner           |   819.2 us | 13.67 us |  16.27 us |   820.5 us |  0.97 |    0.04 |    2 | 296.8750 | 296.8750 | 296.8750 |   3.81 MB |        1.00 |
| DeepCopy             |   832.6 us | 16.88 us |  48.15 us |   822.2 us |  0.99 |    0.07 |    2 | 273.4375 | 273.4375 | 273.4375 |   3.81 MB |        1.00 |
| ⭐ FastCloner         |   842.9 us | 16.79 us |  35.05 us |   833.3 us |  1.00 |    0.06 |    2 | 328.1250 | 328.1250 | 328.1250 |   3.82 MB |        1.00 |
| ManualCopy           | 2,574.0 us | 50.50 us | 115.02 us | 2,555.7 us |  3.06 |    0.18 |    3 | 390.6250 | 390.6250 | 390.6250 |   3.81 MB |        1.00 |
| DeepCopier (crashes) |         NA |       NA |        NA |         NA |     ? |       ? |    ? |       NA |       NA |       NA |        NA |           ? |