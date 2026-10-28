using MappingComparison.Benchmarks;
using MappingComparison.Manual;
using Xunit;

namespace MappingComparison.Tests;

public sealed class PerformanceHarnessTests
{
    [Theory]
    [InlineData(PersonScenario.NullCollections, 0)]
    [InlineData(PersonScenario.Small, 4)]
    [InlineData(PersonScenario.Large, 128)]
    public void Every_mapper_preserves_values_and_declared_ownership(PersonScenario scenario, int count)
    {
        var dataset = PersonWorkload.CreateDataset(scenario);
        Assert.Equal(PersonWorkload.DatasetSize, dataset.Length);
        Assert.Equal(dataset.Length, dataset.Select(source => source.Id).Distinct().Count());
        foreach (var source in dataset)
        {
            Assert.Equal(count, source.Tags?.Count ?? 0);
            Assert.Equal(count, source.PreviousAddresses?.Count ?? 0);
        }

        foreach (var mapper in MapperCatalog.Create())
        {
            foreach (var source in dataset)
            {
                MapperCatalog.Validate(mapper, source);
            }
        }
    }

    [Fact]
    public void Preflight_rejects_incorrect_values()
    {
        var mapper = new MapperCase("Invalid", false, false,
            source => source is null ? null : ManualMappings.ToDto(source)! with { Id = -1 });

        Assert.Throws<InvalidOperationException>(() => MapperCatalog.Validate(mapper, PersonWorkload.Create(PersonScenario.Small)));
    }

    [Fact]
    public void Preflight_rejects_unexpected_reference_sharing()
    {
        var mapper = new MapperCase("Invalid", false, false,
            source => source is null ? null : ManualMappings.ToDto(source)! with { Tags = source.Tags });

        Assert.Throws<InvalidOperationException>(() => MapperCatalog.Validate(mapper, PersonWorkload.Create(PersonScenario.Small)));
    }

    [Fact]
    public async Task Concurrent_measurement_counts_completed_work_and_consumes_results()
    {
        var mapper = new MapperCase("Manual", false, false, source => ManualMappings.ToDto(source));
        var source = PersonWorkload.Create(PersonScenario.Small);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var result = await LoadRunner.MeasureAsync(mapper, PersonScenario.Small, [source],
            workers: 2, repetition: 1, seconds: 1, cancellation.Token);

        Assert.True(result.Operations > 0);
        Assert.Equal(result.Operations * source.Id, result.Checksum);
        Assert.True(result.ElapsedSeconds >= 1);
        Assert.True(result.OperationsPerSecond > 0);
        Assert.True(result.AllocatedBytesPerOperation > 0);
    }

    [Fact]
    public async Task Cancelled_measurement_does_not_start_workers()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var mapper = new MapperCase("Manual", false, false, source => ManualMappings.ToDto(source));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LoadRunner.MeasureAsync(
            mapper, PersonScenario.Small, [PersonWorkload.Create(PersonScenario.Small)],
            workers: 2, repetition: 1, seconds: 1, cancellation.Token));
    }

    [Theory]
    [InlineData("--seconds", "0")]
    [InlineData("--seconds", "301")]
    [InlineData("--repetitions", "0")]
    [InlineData("--workers", "0")]
    [InlineData("--workers", "257")]
    [InlineData("--workers", "1,no")]
    [InlineData("--unknown", "1")]
    public void Invalid_load_options_are_rejected(string option, string value)
    {
        Assert.Throws<ArgumentException>(() => LoadOptions.Parse([option, value]));
    }

    [Fact]
    public void Load_options_require_values_and_remove_duplicate_workers()
    {
        Assert.Throws<ArgumentException>(() => LoadOptions.Parse(["--seconds"]));
        var options = LoadOptions.Parse(["--seconds", "2", "--repetitions", "3", "--workers", "1,4,1"]);
        Assert.Equal(2, options.Seconds);
        Assert.Equal(3, options.Repetitions);
        Assert.Equal([1, 4], options.Workers);
    }
}
