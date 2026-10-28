using BenchmarkDotNet.Running;
using MappingComparison.Benchmarks;

if (args is ["load", .. var loadArguments])
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    await LoadRunner.RunAsync(LoadOptions.Parse(loadArguments), cancellation.Token);
}
else
{
    var summaries = BenchmarkSwitcher.FromAssembly(typeof(PersonMappingBenchmarks).Assembly).Run(args).ToArray();
    if (summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)))
    {
        Environment.ExitCode = 1;
    }
}
