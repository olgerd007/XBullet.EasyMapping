using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ConverterInvocationTests
{
    [Theory]
    [InlineData("static", "scalar")]
    [InlineData("instance", "scalar")]
    [InlineData("instance-static", "scalar")]
    [InlineData("static", "list")]
    [InlineData("instance", "list")]
    [InlineData("instance-static", "list")]
    [InlineData("static", "enumerable")]
    [InlineData("instance", "enumerable")]
    [InlineData("instance-static", "enumerable")]
    public void Source_parameters_cannot_shadow_converters(string mode, string shape)
    {
        var isStaticMapper = mode == "static";
        var mapperModifier = isStaticMapper ? "static " : "";
        var converterModifier = mode == "instance" ? "" : "static ";
        var sourceType = shape switch
        {
            "list" => "List<int?>",
            "enumerable" => "IEnumerable<int?>",
            _ => "int?"
        };
        var targetType = shape == "scalar" ? "string" : "List<string>";
        var value = shape == "scalar" ? "(int?)null" : "new List<int?> { 7, null }";
        var receiver = isStaticMapper ? "M" : "new M()";
        var assertion = shape == "scalar" ? "result.Value == \"missing\"" : "result.Value.SequenceEqual(new[] { \"7\", \"missing\" })";

        GeneratorTestCompilation.VerifyRuntime($$"""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public record Input({{sourceType}} Value);
            public record Output({{targetType}} Value);
            [Mapper] public {{mapperModifier}}partial class M {
                public {{mapperModifier}}partial Output Map(Input Convert);
                private {{converterModifier}}string Convert(int? value) => value?.ToString() ?? "missing";
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = {{receiver}}.Map(new Input({{value}}));
                    return {{assertion}};
                }
            }
            """);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("instance")]
    [InlineData("instance-static")]
    public void Update_target_parameters_cannot_shadow_converters(string mode)
    {
        var isStaticMapper = mode == "static";
        var mapperModifier = isStaticMapper ? "static " : "";
        var converterModifier = mode == "instance" ? "" : "static ";
        var receiver = isStaticMapper ? "M" : "new M()";

        GeneratorTestCompilation.VerifyRuntime($$"""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public record Input(int Value, List<int> Values);
            public class Output {
                public string Value { get; set; } = "";
                public List<string> Values { get; set; } = new();
            }
            [Mapper] public {{mapperModifier}}partial class M {
                public {{mapperModifier}}partial void Update(Input source, Output Convert);
                private {{converterModifier}}string Convert(int value) => value.ToString();
            }
            public static class Scenario {
                public static bool Verify() {
                    var target = new Output();
                    {{receiver}}.Update(new Input(7, new List<int> { 8, 9 }), target);
                    return target.Value == "7" && target.Values.SequenceEqual(new[] { "8", "9" });
                }
            }
            """);
    }
}
