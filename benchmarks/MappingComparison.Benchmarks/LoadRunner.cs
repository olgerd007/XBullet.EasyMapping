using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MappingComparison.Contracts;

namespace MappingComparison.Benchmarks;

public static class LoadRunner
{
    private const int BatchSize = 256;

    public static async Task RunAsync(LoadOptions options, CancellationToken cancellationToken)
    {
        RejectDebugBuild();
        var mappers = MapperCatalog.Create();
        var scenarios = new[] { PersonScenario.Small, PersonScenario.Large };
        var datasets = scenarios.ToDictionary(scenario => scenario, PersonWorkload.CreateDataset);
        Console.WriteLine("Validating all inputs and warming each mapper for one second...");
        foreach (var mapper in mappers)
        {
            foreach (var source in datasets.Values.SelectMany(dataset => dataset))
            {
                MapperCatalog.Validate(mapper, source);
            }

            _ = await MeasureAsync(mapper, PersonScenario.Small, datasets[PersonScenario.Small],
                workers: 1, repetition: 0, seconds: 1, cancellationToken);
        }

        var results = new List<LoadResult>();
        var random = new Random(42);
        var cases = (from scenario in scenarios
                     from workers in options.Workers
                     from mapper in mappers
                     select (scenario, workers, mapper)).ToArray();
        for (var repetition = 1; repetition <= options.Repetitions; repetition++)
        {
            random.Shuffle(cases);
            foreach (var (scenario, workers, mapper) in cases)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await MeasureAsync(mapper, scenario, datasets[scenario], workers,
                    repetition, options.Seconds, cancellationToken);
                results.Add(result);
                Console.WriteLine($"{mapper.Name,-20} {scenario,-6} workers={workers} run={repetition}: "
                    + $"{result.OperationsPerSecond,12:N0} maps/s; {result.AllocatedBytesPerOperation:N1} B/map");
            }
        }

        var report = new
        {
            CapturedAtUtc = TimeProvider.System.GetUtcNow(),
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            LogicalProcessors = Environment.ProcessorCount,
            ServerGC = GCSettings.IsServerGC,
            StopwatchFrequency = Stopwatch.Frequency,
            DatasetSize = PersonWorkload.DatasetSize,
            ShuffleSeed = 42,
            Options = options,
            Results = results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        await File.WriteAllTextAsync(options.Output, JsonSerializer.Serialize(report, jsonOptions), cancellationToken);
        Console.WriteLine($"Saved {results.Count} measurements to {Path.GetFullPath(options.Output)}");
    }

    [Conditional("DEBUG")]
    private static void RejectDebugBuild() =>
        throw new InvalidOperationException("Load testing requires a Release build.");

    public static async Task<LoadResult> MeasureAsync(
        MapperCase mapper,
        PersonScenario scenario,
        PersonInput[] dataset,
        int workers,
        int repetition,
        int seconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentOutOfRangeException.ThrowIfZero(dataset.Length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);
        cancellationToken.ThrowIfCancellationRequested();

        using var ready = new CountdownEvent(workers);
        using var start = new ManualResetEventSlim();
        var deadline = 0L;
        // Workers must start even after cancellation so every participant signals the barrier.
        var tasks = Enumerable.Range(0, workers).Select(worker => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var operations = 0L;
            var checksum = 0L;
            var cursor = worker % dataset.Length;
            while (Stopwatch.GetTimestamp() < deadline && !cancellationToken.IsCancellationRequested)
            {
                for (var index = 0; index < BatchSize; index++)
                {
                    var source = dataset[cursor];
                    var id = MapperCatalog.GetId(mapper.Map(source));
                    if (id != source.Id)
                    {
                        throw new InvalidOperationException($"{mapper.Name}: incorrect ID under load.");
                    }

                    checksum += id;
                    cursor = (cursor + 1) % dataset.Length;
                }

                operations += BatchSize;
            }

            return (Operations: operations, Checksum: checksum,
                AllocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
                FinishedAt: Stopwatch.GetTimestamp());
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        // All dedicated workers are ready before the shared measurement window starts.
        ready.Wait();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var collectionsBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var startedAt = Stopwatch.GetTimestamp();
        deadline = startedAt + seconds * Stopwatch.Frequency;
        start.Set();
        var samples = await Task.WhenAll(tasks);
        cancellationToken.ThrowIfCancellationRequested();
        var elapsed = Stopwatch.GetElapsedTime(startedAt, samples.Max(sample => sample.FinishedAt)).TotalSeconds;
        var operations = samples.Sum(sample => sample.Operations);
        return new LoadResult(mapper.Name, scenario, workers, repetition, operations, elapsed,
            operations / elapsed, samples.Sum(sample => sample.AllocatedBytes) / (double)operations,
            GC.CollectionCount(0) - collectionsBefore[0],
            GC.CollectionCount(1) - collectionsBefore[1],
            GC.CollectionCount(2) - collectionsBefore[2],
            samples.Sum(sample => sample.Checksum));
    }
}
