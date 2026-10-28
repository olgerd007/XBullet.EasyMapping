namespace MappingComparison.Benchmarks;

public sealed record LoadResult(
    string Mapper,
    PersonScenario Scenario,
    int Workers,
    int Repetition,
    long Operations,
    double ElapsedSeconds,
    double OperationsPerSecond,
    double AllocatedBytesPerOperation,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long Checksum);
