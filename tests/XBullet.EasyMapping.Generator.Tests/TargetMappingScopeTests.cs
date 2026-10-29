using Microsoft.CodeAnalysis;
using XBullet.EasyMapping.Generator;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class TargetMappingScopeTests
{
    [Fact]
    public void Empty_and_ignore_only_scopes_choose_a_stable_diagnostic_path()
    {
        var reports = new List<string>();
        void Report(DiagnosticDescriptor descriptor, string[] arguments) => reports.AddRange(arguments);
        var empty = TargetMappingScope.Create([], [], Report, CancellationToken.None);
        Assert.Empty(empty.FirstPath());
        var ignored = TargetMappingScope.Create([], ["Customer.Address.City"], Report, CancellationToken.None);
        Assert.Equal("Customer.Address.City", ignored.FirstPath());
        Assert.Equal("Address.City", ignored.Mappings["Customer"].NestedTarget!.FirstPath());
        Assert.Empty(reports);
    }

    [Fact]
    public void Scope_construction_honors_cancellation()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => TargetMappingScope.Create([], ["Customer.Name"],
            (_, _) => Assert.Fail("A canceled scope must not report diagnostics"), canceled.Token));
    }
}
