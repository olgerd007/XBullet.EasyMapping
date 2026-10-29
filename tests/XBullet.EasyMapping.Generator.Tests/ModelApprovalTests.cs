using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ModelApprovalTests
{
    private const string Mapper = """
        using XBullet.EasyMapping;
        [Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)]
        public static partial class M { public static partial Output Map(Input source); }
        """;
    private const string Models = "public record Input(int Id); public record Output(int Id);";

    [Theory]
    [InlineData("int", "long")]
    [InlineData("int", "uint")]
    [InlineData("byte", "sbyte")]
    [InlineData("short", "ushort")]
    [InlineData("long", "ulong")]
    public void Enum_underlying_type_changes_in_nested_models_require_approval(string before, string after)
    {
        var source = Mapper + $$"""
            public enum Status : {{before}} { Ready = 1 }
            public readonly record struct Envelope(Status Status);
            public record Input(Envelope Value);
            public record Output(Envelope Value);
            """;
        var baseline = Approval(source);
        var (driver, compilation) = GeneratorTestCompilation.Generate(
            source.Replace("Status : " + before, "Status : " + after, StringComparison.Ordinal), baseline);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP011", error.Id);
        Assert.Contains("has changed", error.GetMessage());
        Assert.Contains("  underlying " + before + "\n", baseline.GetText()!.ToString());
        Assert.Contains("  underlying " + after + "\n", Candidate(driver));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
    }

    [Theory]
    [InlineData("byte")]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    [InlineData("ulong")]
    public void Legacy_enum_approvals_require_one_review_before_the_new_shape_is_accepted(string underlyingType)
    {
        var source = Mapper + $"public enum Status : {underlyingType} {{ Ready = 1 }} public record Input(Status Id); public record Output(Status Id);";
        var current = Approval(source);
        var underlyingLine = "  underlying " + underlyingType + "\n";
        Assert.Contains(underlyingLine, current.GetText()!.ToString());
        var legacy = new MemoryApproval(current.Path, current.GetText()!.ToString().Replace(underlyingLine, "", StringComparison.Ordinal));

        var (driver, _) = GeneratorTestCompilation.Generate(source, legacy);

        Assert.Equal("EMAP011", Assert.Single(driver.GetRunResult().Diagnostics).Id);
        Assert.Equal(current.GetText()!.ToString(), Candidate(driver));
        var (approvedDriver, compilation) = GeneratorTestCompilation.Generate(source, current);
        Assert.Empty(approvedDriver.GetRunResult().Diagnostics);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
    }

    [Fact]
    public void Missing_approval_fails_the_build_but_emits_a_reviewable_candidate()
    {
        var (driver, output) = GeneratorTestCompilation.Generate(Mapper + Models);
        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP011", error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("is missing", error.GetMessage());
        Assert.Contains(".mapper.received.txt", error.GetMessage());
        Assert.Contains("rename", error.GetMessage());
        Assert.Equal("TestInput.cs", error.Location.GetLineSpan().Path);
        Assert.Contains("model global::Input", Candidate(driver));
        Assert.Contains("model global::Output", Candidate(driver));
        Assert.DoesNotContain(output.GetDiagnostics(), diagnostic => diagnostic.Id == "CS8795");
    }

    [Fact]
    public void Approved_shapes_allow_compilation_without_runtime_snapshot_code()
    {
        var source = Mapper + Models;
        var approval = Approval(source);
        var (driver, output) = GeneratorTestCompilation.Generate(source, approval);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        using var stream = new MemoryStream();
        Assert.True(output.Emit(stream).Success);
        var snapshotSource = Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources.Where(
            generated => generated.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal)));
        Assert.All(snapshotSource.SourceText.Lines.Where(line => line.ToString().Length > 0), line => Assert.StartsWith("//", line.ToString()));
    }

    [Fact]
    public void Approval_location_uses_the_attribute_file_and_does_not_change_the_baseline()
    {
        var (driver, output) = GeneratorTestCompilation.Generate(Mapper + Models);
        var baseline = Candidate(driver);
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var options = new CSharpParseOptions(LanguageVersion.CSharp12);
        var otherPart = CSharpSyntaxTree.ParseText("public static partial class M {}", options, path: "OtherPart.cs");
        const string mapperPath = "Mappings with spaces/PersonMappings.cs";
        var mapperTree = CSharpSyntaxTree.ParseText(Mapper + Models, options, path: mapperPath);
        input = input.RemoveAllSyntaxTrees().AddSyntaxTrees(otherPart, mapperTree);
        driver = driver.RunGenerators(input);

        var candidate = Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources.Where(
            source => source.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal))).SourceText.ToString();
        var metadata = Assert.Single(candidate.Split('\n'), line => line.StartsWith("// MapperSourcePath: ", StringComparison.Ordinal));
        var path = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(metadata["// MapperSourcePath: ".Length..]));
        Assert.Equal(mapperPath, path);
        Assert.Equal(baseline, Candidate(driver));
        Assert.DoesNotContain(mapperPath, Candidate(driver));
    }

    [Theory]
    [InlineData("matching_add")]
    [InlineData("matching_remove")]
    [InlineData("types")]
    [InlineData("nullability")]
    [InlineData("constructor_default")]
    [InlineData("required")]
    [InlineData("enum_value")]
    [InlineData("attribute")]
    [InlineData("nested")]
    [InlineData("accessors")]
    public void Structurally_valid_mapping_changes_still_require_approval(string scenario)
    {
        var models = scenario switch
        {
            "matching_remove" => "public record Input(int Id, string Name); public record Output(int Id, string Name);",
            "nullability" => "public record Input(string Name); public record Output(string Name);",
            "constructor_default" => "public record Input(int Id); public class Output { public Output(int id, int fallback = 1) { Id = id; } public int Id {get;} }",
            "required" => "public record Input(int Id); public class Output { public required int Id {get;init;} }",
            "enum_value" => "public enum Status { A = 0, B = 1 } public record Input(Status Id); public record Output(Status Id);",
            "attribute" => """
                public sealed class LabelAttribute : System.Attribute { public LabelAttribute(string value) {} }
                public record Input(string Name); public record Output([property: Label("old")] string Name);
                """,
            "nested" => "public record ChildInput(int Id); public record ChildDto(int Id); public record Input(ChildInput Child); public record Output(ChildDto Child);",
            "accessors" => "public record Input(int Id); public class Output { public int Id {get;set;} }",
            _ => Models
        };
        var mapper = scenario == "nested"
            ? Mapper.Replace("public static partial Output Map(Input source);", "public static partial Output Map(Input source); private static partial ChildDto MapChild(ChildInput child);", StringComparison.Ordinal)
            : Mapper;
        var before = mapper + models;
        var changedModels = scenario switch
        {
            "matching_add" => models.Replace("(int Id)", "(int Id, string Name)", StringComparison.Ordinal),
            "matching_remove" => models.Replace("(int Id, string Name)", "(int Id)", StringComparison.Ordinal),
            "types" => models.Replace("int Id", "long Id", StringComparison.Ordinal),
            "nullability" => models.Replace("string Name", "string? Name", StringComparison.Ordinal),
            "constructor_default" => models.Replace("fallback = 1", "fallback = 2", StringComparison.Ordinal),
            "required" => models.Replace("required int", "int", StringComparison.Ordinal),
            "enum_value" => models.Replace("B = 1", "B = 10", StringComparison.Ordinal),
            "attribute" => models.Replace("Label(\"old\")", "Label(\"new\")", StringComparison.Ordinal),
            "nested" => models.Replace("ChildInput(int Id)", "ChildInput(int Id, string Name)", StringComparison.Ordinal)
                .Replace("ChildDto(int Id)", "ChildDto(int Id, string Name)", StringComparison.Ordinal),
            "accessors" => models.Replace("get;set;", "get;init;", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var approved = Approval(before);
        var (driver, output) = GeneratorTestCompilation.Generate(mapper + changedModels, approved);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP011", error.Id);
        Assert.Contains("has changed", error.GetMessage());
        // The rejection comes from shape approval, not an incompatible or invalid mapping.
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning && diagnostic.Id != "EMAP011"));
        Assert.NotEqual(approved.GetText()!.ToString(), Candidate(driver));
    }

    [Fact]
    public void Ignored_members_are_still_part_of_the_approved_model_shape()
    {
        var source = Mapper.Replace("public static partial Output Map", "[MapIgnoreSource(\"Note\")] public static partial Output Map", StringComparison.Ordinal)
            + "public record Input(int Id, string Note); public record Output(int Id);";
        var approval = Approval(source);
        var changed = source.Replace("string Note", "string? Note", StringComparison.Ordinal);
        var (driver, _) = GeneratorTestCompilation.Generate(changed, approval);
        Assert.Equal("EMAP011", Assert.Single(driver.GetRunResult().Diagnostics).Id);
    }

    [Fact]
    public void Crlf_and_final_newline_variations_do_not_require_approval()
    {
        var source = Mapper + Models;
        var baseline = Approval(source);
        var crlf = new MemoryApproval(baseline.Path, baseline.GetText()!.ToString().Replace("\n", "\r\n", StringComparison.Ordinal).TrimEnd());
        var (driver, _) = GeneratorTestCompilation.Generate(source, crlf);
        Assert.Empty(driver.GetRunResult().Diagnostics);
    }

    [Fact]
    public void Approval_content_is_independent_of_property_declaration_order()
    {
        const string properties = "public int Id {get;set;} public string Name {get;set;} = \"\";";
        var source = Mapper + "public class Input { " + properties + " } public class Output { " + properties + " }";
        var changed = source.Replace(properties, "public string Name {get;set;} = \"\"; public int Id {get;set;}", StringComparison.Ordinal);
        Assert.Equal(Approval(source).GetText()!.ToString(), Approval(changed).GetText()!.ToString());
    }

    [Fact]
    public void Duplicate_or_unreadable_approvals_cannot_silently_pass()
    {
        var source = Mapper + Models;
        var baseline = Approval(source);
        var duplicate = new MemoryApproval("other/M.mapper.approved.txt", baseline.GetText()!.ToString());
        var duplicated = GeneratorTestCompilation.Generate(source, baseline, duplicate).Driver.GetRunResult();
        Assert.Contains("duplicate", Assert.Single(duplicated.Diagnostics).GetMessage());
        var unreadable = GeneratorTestCompilation.Generate(source, new MemoryApproval(baseline.Path, null)).Driver.GetRunResult();
        Assert.Contains("cannot be read", Assert.Single(unreadable.Diagnostics).GetMessage());
    }

    [Fact]
    public void An_approval_for_another_mapper_is_not_accepted()
    {
        var source = Mapper + Models;
        var approval = Approval(source);
        var wrong = new MemoryApproval("OtherMapper.mapper.approved.txt", approval.GetText()!.ToString());
        var (driver, _) = GeneratorTestCompilation.Generate(source, wrong);
        Assert.Contains("is missing", Assert.Single(driver.GetRunResult().Diagnostics).GetMessage());
    }

    [Fact]
    public void Additional_file_approval_changes_revalidate_without_changing_source()
    {
        var source = Mapper + Models;
        var (driver, output) = GeneratorTestCompilation.Generate(source);
        var approval = new MemoryApproval("M.mapper.approved.txt", Candidate(driver));
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        driver = driver.AddAdditionalTexts([approval]).RunGenerators(input);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        var changed = new MemoryApproval(approval.Path, approval.GetText() + "unapproved edit\n");
        driver = driver.ReplaceAdditionalText(approval, changed).RunGenerators(input);
        Assert.Equal("EMAP011", Assert.Single(driver.GetRunResult().Diagnostics).Id);
    }

    [Fact]
    public void A_model_edit_in_a_separate_file_cannot_reuse_an_old_approval()
    {
        var source = Mapper + Models;
        var approval = Approval(source);
        var (driver, output) = GeneratorTestCompilation.Generate(source, approval);
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var options = new CSharpParseOptions(LanguageVersion.CSharp12);
        var mapperTree = CSharpSyntaxTree.ParseText(Mapper, options, path: "Mapper.cs");
        var modelTree = CSharpSyntaxTree.ParseText(Models, options, path: "Models.cs");
        input = input.ReplaceSyntaxTree(Assert.Single(input.SyntaxTrees), mapperTree).AddSyntaxTrees(modelTree);
        driver = driver.RunGenerators(input);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        var changed = CSharpSyntaxTree.ParseText(Models.Replace("(int Id)", "(int Id, string Name)", StringComparison.Ordinal), options, path: "Models.cs");
        driver = driver.RunGenerators(input.ReplaceSyntaxTree(modelTree, changed));
        Assert.Equal("EMAP011", Assert.Single(driver.GetRunResult().Diagnostics).Id);
    }

    [Fact]
    public void Opting_out_preserves_existing_generation_and_ignores_approval_files()
    {
        var source = Mapper.Replace(", RequireModelApproval = true", "", StringComparison.Ordinal) + Models;
        var (driver, _) = GeneratorTestCompilation.Generate(source, new MemoryApproval("M.mapper.approved.txt", "stale"));
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Legacy_and_annotated_generic_models_can_share_an_approval()
    {
        var source = """
            using XBullet.EasyMapping;
            #nullable disable
            public class Box<T> { public T Value {get;set;} }
            public record Input(Box<string> Value);
            #nullable enable
            public record Output(Box<string> Value);
            [Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)]
            public static partial class M {
                public static partial Output Map(Input source);
                private static Box<string> Copy(Box<string> source) => new() { Value = source.Value };
            }
            """;
        var approval = Approval(source);
        var (driver, _) = GeneratorTestCompilation.Generate(source, approval);
        Assert.Empty(driver.GetRunResult().Diagnostics);
    }

    [Fact]
    public void Ignored_recursive_generic_models_have_finite_deterministic_snapshots()
    {
        var source = Mapper.Replace("public static partial Output Map", "[MapIgnoreSource(\"Next\")] public static partial Output Map", StringComparison.Ordinal) + """
            public class Node<T> { public int Id {get;set;} public Node<Node<T>>? Next {get;set;} }
            public class Input : Node<int> {}
            public record Output(int Id);
            """;
        var approval = Approval(source);
        Assert.True(approval.GetText()!.Length < 10_000);
        var (driver, _) = GeneratorTestCompilation.Generate(source, approval);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Equal(approval.GetText()!.ToString(), Candidate(driver));
    }

    [Fact]
    public void Handwritten_method_body_changes_are_outside_model_shape_approval()
    {
        var source = Mapper.Replace("public static partial Output Map(Input source);", "public static partial Output Map(Input source); private static string Convert(int value) => \"before\";", StringComparison.Ordinal)
            + "public record Input(int Id); public record Output(string Id);";
        var approval = Approval(source);
        var changed = source.Replace("=> \"before\"", "=> \"after\"", StringComparison.Ordinal);
        var (driver, _) = GeneratorTestCompilation.Generate(changed, approval);
        Assert.Empty(driver.GetRunResult().Diagnostics);
    }

    [Theory]
    [InlineData("nested_member")]
    [InlineData("nested_numeric_type")]
    [InlineData("leaf_nullability")]
    [InlineData("generic_collection_nullability")]
    [InlineData("generic_constructor_default")]
    [InlineData("nested_enum_value")]
    [InlineData("ignored_leaf_member")]
    public void Changes_deep_inside_generic_collection_graphs_still_require_approval(string scenario)
    {
        var models = """
            public enum State : byte { A = 0, B = 1 }
            public class Page<T> where T : class {
                public Page(int limit = 10) {}
                public System.Collections.Generic.IReadOnlyList<T?>? Items { get; init; } = default!;
            }
            public record AddressInput(string City);
            public record AddressDto(string City);
            public record ChildInput(int Id, State State, AddressInput? Address);
            public record ChildDto(int Id, State State, AddressDto? Address);
            public record Input(Page<ChildInput> Value);
            public record Output(Page<ChildDto> Value);
            """;
        var mapper = """
            using XBullet.EasyMapping;
            [Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)]
            public static partial class M {
                public static partial Output Map(Input source);
                private static partial Page<ChildDto> MapPage(Page<ChildInput> source);
                private static partial ChildDto? MapChild(ChildInput? source);
                private static partial AddressDto? MapAddress(AddressInput? source);
            }
            """;
        if (scenario == "ignored_leaf_member")
        {
            models = models.Replace("AddressInput(string City)", "AddressInput(string City, int Internal)", StringComparison.Ordinal);
            mapper = mapper.Replace("private static partial AddressDto?", "[MapIgnoreSource(\"Internal\")] private static partial AddressDto?", StringComparison.Ordinal);
        }
        var changedModels = scenario switch
        {
            "nested_member" => models.Replace("AddressInput? Address)", "AddressInput? Address, string? Note)", StringComparison.Ordinal)
                .Replace("AddressDto? Address)", "AddressDto? Address, string? Note)", StringComparison.Ordinal),
            "nested_numeric_type" => models.Replace("int Id", "long Id", StringComparison.Ordinal),
            "leaf_nullability" => models.Replace("string City", "string? City", StringComparison.Ordinal),
            "generic_collection_nullability" => models.Replace("IReadOnlyList<T?>?", "IReadOnlyList<T?>", StringComparison.Ordinal),
            "generic_constructor_default" => models.Replace("limit = 10", "limit = 20", StringComparison.Ordinal),
            "nested_enum_value" => models.Replace("B = 1", "B = 10", StringComparison.Ordinal),
            "ignored_leaf_member" => models.Replace("int Internal", "long Internal", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var baseline = Approval(mapper + models);
        var (driver, compilation) = GeneratorTestCompilation.Generate(mapper + changedModels, baseline);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP011", error.Id);
        Assert.Contains("has changed", error.GetMessage(), StringComparison.Ordinal);
        Assert.NotEqual(baseline.GetText()!.ToString(), Candidate(driver));
        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic =>
            diagnostic.Severity >= DiagnosticSeverity.Warning && diagnostic.Id != "EMAP011");
    }

    private static MemoryApproval Approval(string source)
    {
        var (driver, _) = GeneratorTestCompilation.Generate(source);
        Assert.Equal("EMAP011", Assert.Single(driver.GetRunResult().Diagnostics).Id);
        return new MemoryApproval("M.mapper.approved.txt", Candidate(driver));
    }

    private static string Candidate(GeneratorDriver driver)
    {
        var generated = Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources.Where(
            source => source.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal)));
        var lines = generated.SourceText.ToString().Split('\n');
        var start = Array.IndexOf(lines, "// <MapperModelApproval>");
        var end = Array.IndexOf(lines, "// </MapperModelApproval>");
        Assert.True(start >= 0 && end > start);
        return string.Join("\n", lines[(start + 1)..end].Select(line => line[3..])) + "\n";
    }

    private sealed class MemoryApproval(string path, string? content) : AdditionalText
    {
        public override string Path => path;
        public override SourceText? GetText(CancellationToken cancellationToken = default) => content is null ? null : SourceText.From(content);
    }
}
