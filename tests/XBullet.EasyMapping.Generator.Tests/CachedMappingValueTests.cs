using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using XBullet.EasyMapping.Generator;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class CachedMappingValueTests
{
    [Fact]
    public void Equal_cached_plans_and_snapshots_have_equal_object_values_and_hash_codes()
    {
        var diagnostic = Diagnostic();
        var copy = Diagnostic();
        Assert.True(diagnostic.Equals((object)copy));
        Assert.False(diagnostic.Equals((object?)null));
        Assert.False(diagnostic.Equals("other"));
        Assert.Equal(diagnostic.GetHashCode(), copy.GetHashCode());

        var snapshot = new ModelApprovalSnapshot("OrderMapper", "order\r\n", diagnostic, "Orders.cs");
        var snapshotCopy = new ModelApprovalSnapshot("OrderMapper", "order\n\n", copy, "Orders.cs");
        Assert.True(snapshot.Equals((object)snapshotCopy));
        Assert.False(snapshot.Equals((object?)null));
        Assert.False(snapshot.Equals("other"));
        Assert.Equal(snapshot.GetHashCode(), snapshotCopy.GetHashCode());

        var plan = new GeneratedMapper("OrderMapper.g.cs", "order", [diagnostic], snapshot);
        var planCopy = new GeneratedMapper("OrderMapper.g.cs", "order", [copy], snapshotCopy);
        Assert.True(plan.Equals((object)planCopy));
        Assert.False(plan.Equals((object?)null));
        Assert.False(plan.Equals("other"));
        Assert.Equal(plan.GetHashCode(), planCopy.GetHashCode());
    }

    [Theory]
    [InlineData("descriptor")]
    [InlineData("file")]
    [InlineData("span")]
    [InlineData("lines")]
    [InlineData("arguments")]
    public void Diagnostic_changes_invalidate_cached_values(string change)
    {
        var original = Diagnostic();
        var changed = new MappingDiagnostic(
            change == "descriptor" ? MappingDiagnostics.Mapper : MappingDiagnostics.Configuration,
            Location.Create(change == "file" ? "Customers.cs" : "Orders.cs",
                change == "span" ? new TextSpan(1, 1) : new TextSpan(0, 1),
                change == "lines" ? new LinePositionSpan(new LinePosition(1, 0), new LinePosition(1, 1)) : Lines()),
            change == "arguments" ? "changed" : "order");

        Assert.False(original.Equals(changed));
    }

    [Fact]
    public void Cached_diagnostic_falls_back_to_a_file_location_when_its_tree_disappears_or_shrinks()
    {
        var diagnostic = Diagnostic();
        var empty = CSharpCompilation.Create("Orders");
        var missing = diagnostic.ToDiagnostic(empty, CancellationToken.None);
        Assert.Null(missing.Location.SourceTree);
        Assert.Equal("Orders.cs", missing.Location.GetLineSpan().Path);

        var shorter = empty.AddSyntaxTrees(CSharpSyntaxTree.ParseText("", path: "Orders.cs"));
        var outOfRange = diagnostic.ToDiagnostic(shorter, CancellationToken.None);
        Assert.Null(outOfRange.Location.SourceTree);
        Assert.Equal("Orders.cs", outOfRange.Location.GetLineSpan().Path);

        var matching = empty.AddSyntaxTrees(CSharpSyntaxTree.ParseText("class Order {}", path: "Orders.cs"));
        var current = diagnostic.ToDiagnostic(matching, CancellationToken.None);
        Assert.Same(Assert.Single(matching.SyntaxTrees), current.Location.SourceTree);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => diagnostic.ToDiagnostic(matching, canceled.Token));
    }

    private static MappingDiagnostic Diagnostic() => new(MappingDiagnostics.Configuration,
        Location.Create("Orders.cs", new TextSpan(0, 1), Lines()), "order");

    private static LinePositionSpan Lines() => new(new LinePosition(0, 0), new LinePosition(0, 1));
}
