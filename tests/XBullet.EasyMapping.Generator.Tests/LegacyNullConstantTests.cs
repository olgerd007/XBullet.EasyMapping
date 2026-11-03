using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XBullet.EasyMapping.Generator;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class LegacyNullConstantTests
{
    [Theory]
    [InlineData("string", "\"initial\"")]
    [InlineData("object", "new object()")]
    [InlineData("Response", "new Response()")]
    [InlineData("Response[]", "new Response[] { new Response() }")]
    [InlineData("IResponse", "new Response()")]
    public void Legacy_reference_properties_accept_null_constants_in_creation_and_updates(string type, string initialValue)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            #nullable disable
            public interface IResponse {}
            public class Response : IResponse {}
            public class Output { public {{type}} Value { get; set; } = {{initialValue}}; }
            #nullable enable
            public record Input();
            [Mapper] public static partial class M {
                [MapConstant(nameof(Output.Value), null)]
                public static partial Output Map(Input source);
                [MapConstant(nameof(Output.Value), null)]
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var target = new Output();
                    if (target.Value is null) return false;
                    M.Update(new Input(), target);
                    return target.Value is null && M.Map(new Input()).Value is null;
                }
            }
            """);
    }

    [Fact]
    public void Legacy_constructor_parameters_accept_null_constants_in_nested_targets()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            #nullable disable
            public class Response {}
            public record Child(Response Value);
            #nullable enable
            public record Input();
            public record Output(Child Child);
            [Mapper] public static partial class M {
                [MapConstant("Child.Value", null)]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input()).Child.Value is null;
            }
            """);
    }

    [Fact]
    public void Legacy_array_elements_accept_null_constants_without_generated_nullable_warnings()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            #nullable disable
            public class Output { public string[] Values { get; set; } }
            #nullable enable
            public record Input();
            [Mapper] public static partial class M {
                [MapConstant(nameof(Output.Values), new string?[] { null, "kept" })]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var first = M.Map(new Input());
                    var second = M.Map(new Input());
                    return first.Values.Length == 2 && first.Values[0] is null &&
                        first.Values[1] == "kept" && !object.ReferenceEquals(first.Values, second.Values);
                }
            }
            """);
    }

    [Theory]
    [InlineData("enable", "string")]
    [InlineData("enable", "Response")]
    [InlineData("disable", "int")]
    [InlineData("disable", "System.Guid")]
    [InlineData("disable", "Status")]
    public void Null_constants_still_reject_non_nullable_targets(string nullableContext, string type)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            #nullable {{nullableContext}}
            public class Response {}
            public enum Status { Unknown }
            public class Output { public {{type}} Value { get; set; } = default!; }
            public record Input();
            [Mapper] public static partial class M {
                [MapConstant(nameof(Output.Value), null)]
                public static partial Output Map(Input source);
            }
            """);

        var diagnostic = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP006", diagnostic.Id);
        Assert.Contains("null requires a nullable target", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Referenced_model_annotations_control_null_constants_independently_of_the_consumer_setting(
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
                public class Response {}
                public class Output { public Response Value { get; set; } = new Response(); }
                """, parseOptions));
        using var contractsStream = new MemoryStream();
        var contractsEmit = contracts.Emit(contractsStream);
        Assert.True(contractsEmit.Success, string.Join(Environment.NewLine, contractsEmit.Diagnostics));
        var consumer = ((CSharpCompilation)template).RemoveAllSyntaxTrees()
            .WithOptions(((CSharpCompilationOptions)template.Options).WithNullableContextOptions(
                consumerNullableEnabled ? NullableContextOptions.Enable : NullableContextOptions.Disable))
            .AddReferences(MetadataReference.CreateFromImage(contractsStream.ToArray()))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                using XBullet.EasyMapping;
                public record Input();
                [Mapper] public static partial class M {
                    [MapConstant(nameof(Contracts.Output.Value), null)]
                    public static partial Contracts.Output Map(Input source);
                    [MapConstant(nameof(Contracts.Output.Value), null)]
                    public static partial void Update(Input source, Contracts.Output target);
                }
                """, parseOptions, path: "Consumer.cs"));
        var property = Assert.Single(consumer.GetTypeByMetadataName("Contracts.Output")!
            .GetMembers("Value").OfType<IPropertySymbol>());
        Assert.Equal(contractsNullableEnabled ? NullableAnnotation.NotAnnotated : NullableAnnotation.None,
            property.Type.NullableAnnotation);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new MappingGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(consumer, out var output, out _);

        if (contractsNullableEnabled)
        {
            var diagnostics = driver.GetRunResult().Diagnostics;
            Assert.Equal(2, diagnostics.Length);
            Assert.All(diagnostics, diagnostic => Assert.Equal("EMAP006", diagnostic.Id));
            Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
        }
        else
        {
            Assert.Empty(driver.GetRunResult().Diagnostics);
            Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
            using var outputStream = new MemoryStream();
            var outputEmit = output.Emit(outputStream);
            Assert.True(outputEmit.Success, string.Join(Environment.NewLine, outputEmit.Diagnostics));
        }
    }
}
