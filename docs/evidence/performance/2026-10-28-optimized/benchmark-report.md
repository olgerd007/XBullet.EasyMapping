```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
AMD Ryzen 9 7900X 4.70GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  Job-HDUCDG : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

IterationCount=8  IterationTime=250ms  LaunchCount=1
WarmupCount=3

```
| Method             | Scenario        | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |---------------- |-------------:|------------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
| **Manual**             | **NullCollections** |     **8.826 ns** |   **0.6383 ns** |   **0.3338 ns** |  **1.00** |    **0.05** | **0.0033** |      **-** |      **56 B** |        **1.00** |
| AutoMapper         | NullCollections |    59.184 ns |   4.5258 ns |   2.3671 ns |  6.71 |    0.35 | 0.0071 |      - |     120 B |        2.14 |
| AutoMapperNullable | NullCollections |    64.740 ns |  13.1723 ns |   6.8894 ns |  7.34 |    0.79 | 0.0031 |      - |      56 B |        1.00 |
| Mapperly           | NullCollections |    11.711 ns |   1.6327 ns |   0.8539 ns |  1.33 |    0.10 | 0.0033 |      - |      56 B |        1.00 |
| MapperlyCopying    | NullCollections |    15.435 ns |   1.6856 ns |   0.8816 ns |  1.75 |    0.11 | 0.0033 |      - |      56 B |        1.00 |
| Facet              | NullCollections |    14.482 ns |   1.4006 ns |   0.7325 ns |  1.64 |    0.10 | 0.0052 |      - |      88 B |        1.57 |
| EasyMapping        | NullCollections |    11.336 ns |   0.6295 ns |   0.3293 ns |  1.29 |    0.06 | 0.0033 |      - |      56 B |        1.00 |
| EasyMappingBefore  | NullCollections |     8.724 ns |   1.3091 ns |   0.6847 ns |  0.99 |    0.08 | 0.0033 |      - |      56 B |        1.00 |
|                    |                 |              |             |             |       |         |        |        |           |             |
| **Manual**             | **Small**           |    **79.712 ns** |   **7.1871 ns** |   **3.7590 ns** |  **1.00** |    **0.06** | **0.0275** |      **-** |     **464 B** |        **1.00** |
| AutoMapper         | Small           |   135.910 ns |  11.7732 ns |   6.1576 ns |  1.71 |    0.10 | 0.0228 |      - |     392 B |        0.84 |
| AutoMapperNullable | Small           |   133.007 ns |  11.1519 ns |   4.9515 ns |  1.67 |    0.09 | 0.0231 |      - |     392 B |        0.84 |
| Mapperly           | Small           |    38.746 ns |   1.0638 ns |   0.4723 ns |  0.49 |    0.02 | 0.0181 |      - |     304 B |        0.66 |
| MapperlyCopying    | Small           |    57.397 ns |  10.1398 ns |   5.3033 ns |  0.72 |    0.07 | 0.0233 |      - |     392 B |        0.84 |
| Facet              | Small           |    60.975 ns |   4.0811 ns |   1.8121 ns |  0.77 |    0.04 | 0.0281 |      - |     472 B |        1.02 |
| EasyMapping        | Small           |    51.201 ns |   2.2868 ns |   1.0154 ns |  0.64 |    0.03 | 0.0232 |      - |     392 B |        0.84 |
| EasyMappingBefore  | Small           |    86.797 ns |   5.7517 ns |   2.5538 ns |  1.09 |    0.06 | 0.0318 |      - |     536 B |        1.16 |
|                    |                 |              |             |             |       |         |        |        |           |             |
| **Manual**             | **Large**           | **1,289.304 ns** | **130.7148 ns** |  **68.3664 ns** |  **1.00** |    **0.07** | **0.3797** | **0.0056** |    **6416 B** |        **1.00** |
| AutoMapper         | Large           | 2,581.314 ns | 525.4542 ns | 274.8227 ns |  2.01 |    0.23 | 0.5115 |      - |    8568 B |        1.34 |
| AutoMapperNullable | Large           | 2,922.917 ns | 185.0809 ns |  96.8009 ns |  2.27 |    0.14 | 0.5049 |      - |    8568 B |        1.34 |
| Mapperly           | Large           | 1,084.382 ns |  74.2551 ns |  38.8369 ns |  0.84 |    0.05 | 0.3121 | 0.0044 |    5264 B |        0.82 |
| MapperlyCopying    | Large           | 1,225.603 ns | 146.5775 ns |  76.6629 ns |  0.95 |    0.07 | 0.3748 | 0.0051 |    6344 B |        0.99 |
| Facet              | Large           | 1,296.181 ns |  82.8636 ns |  43.3393 ns |  1.01 |    0.06 | 0.3191 | 0.0060 |    5432 B |        0.85 |
| EasyMapping        | Large           | 1,238.461 ns | 229.8742 ns | 120.2287 ns |  0.96 |    0.10 | 0.3756 | 0.0085 |    6344 B |        0.99 |
| EasyMappingBefore  | Large           | 1,920.008 ns | 259.2752 ns | 135.6059 ns |  1.49 |    0.13 | 0.3878 |      - |    6488 B |        1.01 |
