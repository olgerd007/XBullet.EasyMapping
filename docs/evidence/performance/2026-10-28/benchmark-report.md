```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
AMD Ryzen 9 7900X 4.70GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  Job-HDUCDG : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

IterationCount=8  IterationTime=250ms  LaunchCount=1
WarmupCount=3

```
| Method             | Scenario        | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |---------------- |-------------:|------------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| **Manual**             | **NullCollections** |     **9.040 ns** |   **1.1279 ns** |  **0.5008 ns** |  **1.00** |    **0.07** | **0.0033** |      **-** |      **56 B** |        **1.00** |
| AutoMapper         | NullCollections |    59.966 ns |   6.0702 ns |  2.6952 ns |  6.65 |    0.43 | 0.0070 |      - |     120 B |        2.14 |
| AutoMapperNullable | NullCollections |    54.702 ns |   3.8102 ns |  1.9928 ns |  6.07 |    0.37 | 0.0033 |      - |      56 B |        1.00 |
| Mapperly           | NullCollections |     8.535 ns |   0.5107 ns |  0.2671 ns |  0.95 |    0.05 | 0.0033 |      - |      56 B |        1.00 |
| MapperlyCopying    | NullCollections |    11.822 ns |   0.5841 ns |  0.3055 ns |  1.31 |    0.07 | 0.0033 |      - |      56 B |        1.00 |
| Facet              | NullCollections |    14.422 ns |   1.5910 ns |  0.8321 ns |  1.60 |    0.12 | 0.0052 |      - |      88 B |        1.57 |
| EasyMapping        | NullCollections |     8.523 ns |   2.7828 ns |  1.4555 ns |  0.95 |    0.16 | 0.0033 |      - |      56 B |        1.00 |
|                    |                 |              |             |            |       |         |        |        |           |             |
| **Manual**             | **Small**           |    **77.661 ns** |  **11.9716 ns** |  **6.2614 ns** |  **1.01** |    **0.11** | **0.0277** |      **-** |     **464 B** |        **1.00** |
| AutoMapper         | Small           |   137.107 ns |   9.9901 ns |  5.2250 ns |  1.78 |    0.15 | 0.0232 |      - |     392 B |        0.84 |
| AutoMapperNullable | Small           |   139.803 ns |  13.3552 ns |  6.9850 ns |  1.81 |    0.16 | 0.0233 |      - |     392 B |        0.84 |
| Mapperly           | Small           |    54.599 ns |   1.5169 ns |  0.7934 ns |  0.71 |    0.05 | 0.0182 |      - |     304 B |        0.66 |
| MapperlyCopying    | Small           |    75.975 ns |   3.0145 ns |  1.3385 ns |  0.98 |    0.08 | 0.0233 |      - |     392 B |        0.84 |
| Facet              | Small           |    79.737 ns |  14.2760 ns |  7.4666 ns |  1.03 |    0.12 | 0.0282 |      - |     472 B |        1.02 |
| EasyMapping        | Small           |   116.948 ns |  10.4679 ns |  5.4749 ns |  1.51 |    0.13 | 0.0317 |      - |     536 B |        1.16 |
|                    |                 |              |             |            |       |         |        |        |           |             |
| **Manual**             | **Large**           | **1,579.224 ns** | **112.8929 ns** | **59.0452 ns** |  **1.00** |    **0.05** | **0.3831** | **0.0061** |    **6416 B** |        **1.00** |
| AutoMapper         | Large           | 3,035.129 ns | 145.4014 ns | 76.0477 ns |  1.92 |    0.08 | 0.5069 |      - |    8568 B |        1.34 |
| AutoMapperNullable | Large           | 3,033.701 ns |  96.9685 ns | 50.7164 ns |  1.92 |    0.07 | 0.5007 |      - |    8568 B |        1.34 |
| Mapperly           | Large           | 1,219.021 ns | 111.4068 ns | 58.2679 ns |  0.77 |    0.04 | 0.3115 | 0.0045 |    5264 B |        0.82 |
| MapperlyCopying    | Large           | 1,238.844 ns | 120.1342 ns | 62.8325 ns |  0.79 |    0.05 | 0.3781 | 0.0047 |    6344 B |        0.99 |
| Facet              | Large           | 1,436.879 ns |  84.6551 ns | 44.2763 ns |  0.91 |    0.04 | 0.3231 | 0.0054 |    5432 B |        0.85 |
| EasyMapping        | Large           | 1,999.169 ns |  91.0124 ns | 47.6012 ns |  1.27 |    0.05 | 0.3848 |      - |    6488 B |        1.01 |
