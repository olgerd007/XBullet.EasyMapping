using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class LegacyNestedSourcePathTests
{
    [Theory]
    [InlineData("Child")]
    [InlineData("IChild")]
    public void Legacy_class_and_interface_paths_preserve_null_for_creation_and_updates(string receiver)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            #nullable disable
            public interface IChild { int Value { get; } string Name { get; } }
            public record Child(int Value, string Name) : IChild;
            public record Input({{receiver}} Child);
            #nullable enable
            public class Output {
                public int? Value { get; set; } = 99;
                public string? Name { get; set; } = "initial";
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Child.Value", nameof(Output.Value))]
                [MapProperty("Child.Name", nameof(Output.Name))]
                public static partial Output Map(Input source);
                [MapProperty("Child.Value", nameof(Output.Value))]
                [MapProperty("Child.Name", nameof(Output.Name))]
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var missing = new Input(null);
                    var empty = M.Map(missing);
                    var updated = new Output();
                    M.Update(missing, updated);
                    var present = M.Map(new Input(new Child(7, "Ada")));
                    return empty.Value == null && empty.Name == null &&
                        updated.Value == null && updated.Name == null && present.Value == 7 && present.Name == "Ada";
                }
            }
            """);
    }

    [Fact]
    public void Deep_legacy_paths_guard_each_reference_and_evaluate_getters_once()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            #nullable disable
            public readonly record struct ValueData(int Value);
            public sealed class Child {
                public int Reads { get; private set; }
                public ValueData Data { get { Reads++; return new ValueData(7); } }
            }
            public sealed class Parent {
                private readonly Child _child;
                public Parent(Child child) { _child = child; }
                public int Reads { get; private set; }
                public Child Child { get { Reads++; return _child; } }
            }
            public sealed class Input {
                private readonly Parent _parent;
                public Input(Parent parent) { _parent = parent; }
                public int Reads { get; private set; }
                public Parent Parent { get { Reads++; return _parent; } }
            }
            #nullable enable
            public record Output(int? Value);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreSource(nameof(Input.Reads))]
                [MapProperty("Parent.Child.Data.Value", nameof(Output.Value))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var missing = new Input(null);
                    if (M.Map(missing).Value != null || missing.Reads != 1) return false;
                    var parent = new Parent(null);
                    var middle = new Input(parent);
                    if (M.Map(middle).Value != null || middle.Reads != 1 || parent.Reads != 1) return false;
                    var child = new Child();
                    var presentParent = new Parent(child);
                    var present = new Input(presentParent);
                    return M.Map(present).Value == 7 && present.Reads == 1 && presentParent.Reads == 1 && child.Reads == 1;
                }
            }
            """);
    }

    [Fact]
    public void Named_leaf_converter_receives_null_and_supplies_an_explicit_enum_default()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public enum SampleMode { Unknown, Enabled }
            #nullable disable
            public record Child(SampleMode Mode);
            public record Input(Child Child);
            #nullable enable
            public record Output(SampleMode Mode);
            [Mapper] public static partial class M {
                [MapProperty("Child.Mode", nameof(Output.Mode), Use = nameof(MapMode))]
                public static partial Output Map(Input source);
                private static SampleMode MapMode(SampleMode? value) => value ?? SampleMode.Unknown;
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(null)).Mode == SampleMode.Unknown &&
                    M.Map(new Input(new Child(SampleMode.Enabled))).Mode == SampleMode.Enabled;
            }
            """);
    }

    [Fact]
    public void Legacy_array_intermediates_and_collection_leaves_preserve_null_and_copy_containers()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            #nullable disable
            public record Child(List<int> Values);
            public record Input(Child Child, int[] Numbers);
            #nullable enable
            public record Output(long[]? Values, int? Count);
            [Mapper] public static partial class M {
                [MapProperty("Child.Values", nameof(Output.Values))]
                [MapProperty("Numbers.Length", nameof(Output.Count))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var missing = M.Map(new Input(null, null));
                    var values = new List<int> { 7, 8 };
                    var present = M.Map(new Input(new Child(values), new[] { 1, 2 }));
                    values.Clear();
                    return missing.Values == null && missing.Count == null && present.Count == 2 &&
                        present.Values is { Length: 2 } && present.Values[0] == 7L && present.Values[1] == 8L;
                }
            }
            """);
    }

    [Fact]
    public void Reference_assignment_option_composes_with_legacy_path_null_propagation()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public class Settings {}
            #nullable disable
            public record Child(Settings Value);
            public record Input(Child Child);
            #nullable enable
            public record Output(Settings? Value);
            [Mapper(AllowReferenceAssignment = true)] public static partial class M {
                [MapProperty("Child.Value", nameof(Output.Value))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var settings = new Settings();
                    return M.Map(new Input(null)).Value == null &&
                        object.ReferenceEquals(settings, M.Map(new Input(new Child(settings))).Value);
                }
            }
            """);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("Status")]
    [InlineData("string")]
    public void Legacy_paths_to_non_nullable_targets_require_explicit_null_handling(string leaf)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public enum Status { Pending, Approved }
            #nullable disable
            public record Child({{leaf}} Value);
            public record Input(Child Child);
            #nullable enable
            public record Output({{leaf}} Value);
            [Mapper] public static partial class M {
                [MapProperty("Child.Value", nameof(Output.Value))]
                public static partial Output Map(Input source);
            }
            """);

        var diagnostic = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP005", diagnostic.Id);
        Assert.Contains("nullable source", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Explicitly_non_nullable_intermediates_and_value_type_intermediates_are_not_lifted()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Child(int Value);
            #nullable disable
            public readonly record struct Data(int Value);
            public record LegacyInput(Data Data);
            #nullable enable
            public record Input(Child Child);
            public record Output(int Value);
            [Mapper] public static partial class M {
                [MapProperty("Child.Value", nameof(Output.Value))]
                public static partial Output Map(Input source);
                [MapProperty("Data.Value", nameof(Output.Value))]
                public static partial Output Map(LegacyInput source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(new Child(7))).Value == 7 &&
                    M.Map(new LegacyInput(new Data(8))).Value == 8;
            }
            """);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Referenced_receiver_annotations_control_guards_independently_of_consumer_context(
        bool contractsNullableEnabled, bool consumerNullableEnabled)
    {
        var (_, template) = GeneratorTestCompilation.Generate("");
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var contracts = ((CSharpCompilation)template).RemoveAllSyntaxTrees()
            .WithAssemblyName("Contracts_" + Guid.NewGuid().ToString("N"))
            .WithOptions(((CSharpCompilationOptions)template.Options).WithNullableContextOptions(
                contractsNullableEnabled ? NullableContextOptions.Enable : NullableContextOptions.Disable))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                namespace Contracts;
                public record Child(int Value);
                public record Input(Child Child);
                """, parseOptions));
        using var contractsStream = new MemoryStream();
        Assert.True(contracts.Emit(contractsStream).Success);
        var consumer = ((CSharpCompilation)template).RemoveAllSyntaxTrees()
            .WithOptions(((CSharpCompilationOptions)template.Options).WithNullableContextOptions(
                consumerNullableEnabled ? NullableContextOptions.Enable : NullableContextOptions.Disable))
            .AddReferences(MetadataReference.CreateFromImage(contractsStream.ToArray()))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                using XBullet.EasyMapping;
                public record Output(int? Value);
                [Mapper] public static partial class M {
                    [MapProperty("Child.Value", nameof(Output.Value))]
                    public static partial Output Map(Contracts.Input source);
                }
                """, parseOptions));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new MappingGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(consumer, out var output, out _);

        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        var generated = Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources).SourceText.ToString();
        Assert.Equal(!contractsNullableEnabled, generated.Contains("?.@Value", StringComparison.Ordinal));
        using var outputStream = new MemoryStream();
        Assert.True(output.Emit(outputStream).Success);
    }
}
