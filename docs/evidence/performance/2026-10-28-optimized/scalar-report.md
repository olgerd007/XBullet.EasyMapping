```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
AMD Ryzen 9 7900X 4.70GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  Job-XMVZGG : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

IterationCount=8  IterationTime=250ms  LaunchCount=3
WarmupCount=3

```
| Method            | Scenario        | Mean     | Error     | StdDev    | Gen0   | Allocated |
|------------------ |---------------- |---------:|----------:|----------:|-------:|----------:|
| EasyMapping       | NullCollections | 8.940 ns | 0.4339 ns | 0.5488 ns | 0.0033 |      56 B |
| EasyMappingBefore | NullCollections | 8.685 ns | 0.9905 ns | 1.2526 ns | 0.0033 |      56 B |
