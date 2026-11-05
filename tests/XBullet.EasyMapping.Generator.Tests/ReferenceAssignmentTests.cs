using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ReferenceAssignmentTests
{
    [Theory]
    [InlineData("Details")]
    [InlineData("IDetails")]
    [InlineData("object")]
    public void Opted_in_create_and_update_share_identical_reference_members(string type)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public interface IDetails { string Name { get; set; } }
            public class Details : IDetails { public string Name { get; set; } = "before"; }
            public record Input({{type}} Value);
            public class Output { public {{type}} Value { get; set; } = new Details(); }
            [Mapper(AllowReferenceAssignment = true)] public static partial class M {
                public static partial Output Map(Input source);
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var details = new Details();
                    var input = new Input(details);
                    var created = M.Map(input);
                    var updated = new Output();
                    M.Update(input, updated);
                    details.Name = "after";
                    return object.ReferenceEquals(details, created.Value) &&
                        object.ReferenceEquals(details, updated.Value) &&
                        ((IDetails)created.Value).Name == "after";
                }
            }
            """);
    }

    [Fact]
    public void Nullable_members_preserve_nulls_and_share_non_null_instances()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public class Details {}
            public record Input(Details? Value);
            public class Output { public Details? Value { get; set; } = new Details(); }
            [Mapper(AllowReferenceAssignment = true)] public static partial class M {
                public static partial Output Map(Input source);
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var value = new Details();
                    var target = new Output();
                    M.Update(new Input(null), target);
                    return target.Value == null && M.Map(new Input(null)).Value == null &&
                        object.ReferenceEquals(value, M.Map(new Input(value)).Value);
                }
            }
            """);
    }

    [Theory]
    [InlineData("enable", "Details", "Details?")]
    [InlineData("disable", "Details", "Details")]
    public void Non_nullable_and_legacy_members_can_flow_to_compatible_targets(
        string context, string sourceType, string targetType)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            #nullable {{context}}
            public class Details {}
            public record Input({{sourceType}} Value);
            public record Output({{targetType}} Value);
            [Mapper(AllowReferenceAssignment = true)] public static partial class M {
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var value = new Details();
                    return object.ReferenceEquals(value, M.Map(new Input(value)).Value);
                }
            }
            """);
    }

    [Theory]
    [InlineData("List<Details?>", "List<Details?>", "new List<Details?> { value, null }")]
    [InlineData("ICollection<Details?>", "List<Details?>", "new List<Details?> { value, null }")]
    [InlineData("Details?[]", "Details?[]", "new Details?[] { value, null }")]
    public void Collections_get_new_containers_while_sharing_same_type_elements(
        string sourceType, string targetType, string values)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public class Details {}
            public record Input({{sourceType}}? Values);
            public class Output { public {{targetType}}? Values { get; set; } }
            [Mapper(AllowReferenceAssignment = true)] public static partial class M {
                public static partial Output Map(Input source);
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var value = new Details();
                    {{sourceType}} values = {{values}};
                    var input = new Input(values);
                    var created = M.Map(input);
                    var updated = new Output();
                    M.Update(input, updated);
                    return !object.ReferenceEquals(values, created.Values) &&
                        !object.ReferenceEquals(values, updated.Values) &&
                        !object.ReferenceEquals(created.Values, updated.Values) &&
                        object.ReferenceEquals(value, created.Values!.First()) &&
                        object.ReferenceEquals(value, updated.Values!.First()) &&
                        created.Values!.Last() == null && M.Map(new Input(null)).Values == null;
                }
            }
            """);
    }

    [Theory]
    [InlineData("private static partial Details Clone(Details source);")]
    [InlineData("private static Details Clone(Details source) => new Details { Name = source.Name };")]
    public void Declared_same_type_mappings_and_converters_take_precedence(string converter)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public class Details { public string Name { get; set; } = ""; }
            public record Input(Details Value, List<Details> Values);
            public record Output(Details Value, List<Details> Values);
            [Mapper(AllowReferenceAssignment = true)] public static partial class M {
                public static partial Output Map(Input source);
                {{converter}}
            }
            public static class Scenario {
                public static bool Verify() {
                    var value = new Details { Name = "original" };
                    var created = M.Map(new Input(value, new List<Details> { value }));
                    return !object.ReferenceEquals(value, created.Value) &&
                        !object.ReferenceEquals(value, created.Values[0]) && created.Value.Name == "original";
                }
            }
            """);
    }

    [Fact]
    public void Named_helpers_still_override_reference_assignment()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public class Details { public string Name { get; set; } = ""; }
            public record Input(Details Value);
            public record Output(Details Value);
            [Mapper(AllowReferenceAssignment = true)] public static partial class M {
                [MapProperty(nameof(Input.Value), nameof(Output.Value), Use = nameof(Clone))]
                public static partial Output Map(Input source);
                private static Details Clone(Details source) => new Details { Name = source.Name };
            }
            public static class Scenario {
                public static bool Verify() {
                    var value = new Details { Name = "original" };
                    var created = M.Map(new Input(value));
                    return !object.ReferenceEquals(value, created.Value) && created.Value.Name == "original";
                }
            }
            """);
    }

    [Theory]
    [InlineData("Details?", "Details")]
    [InlineData("Details", "OtherDetails")]
    [InlineData("Details", "object")]
    [InlineData("Box<string?>", "Box<string>")]
    [InlineData("List<Details?>", "List<Details>")]
    [InlineData("IQueryable<Details>", "IQueryable<Details>")]
    public void Opt_in_does_not_allow_unsafe_nullability_different_types_or_queryables(string sourceType, string targetType)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public class Details {}
            public class OtherDetails {}
            public class Box<T> {}
            public record Input({{sourceType}} Value);
            public record Output({{targetType}} Value);
            [Mapper(AllowReferenceAssignment = true)] public static partial class M {
                public static partial Output Map(Input source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("[Mapper]")]
    [InlineData("[Mapper(AllowReferenceAssignment = false)]")]
    public void Reference_assignment_remains_disabled_by_default(string attribute)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public class Details {}
            public record Input(Details Value);
            public record Output(Details Value);
            {{attribute}} public static partial class M {
                public static partial Output Map(Input source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
    }
}
