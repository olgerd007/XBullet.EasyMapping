using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class RepeatedNestedTypeMappingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sibling_properties_of_the_same_type_map_independently_for_creation_and_updates(bool sharedAddress)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public sealed class Address {
                public string City { get; set; } = "";
            }
            public sealed class AddressDto {
                public string City { get; set; } = "";
            }
            public sealed class Contact {
                public Address? Address1 { get; set; }
                public Address? Address2 { get; set; }
            }
            public sealed class ContactDto {
                public AddressDto? Address1 { get; set; }
                public AddressDto? Address2 { get; set; }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class ContactMapper {
                public static partial ContactDto Map(Contact source);
                public static partial void Update(Contact source, ContactDto target);
                private static partial AddressDto? MapAddress(Address? source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var first = new Address { City = "Kyiv" };
                    var second = {{(sharedAddress ? "first" : "new Address { City = \"Lviv\" }")}};
                    var source = new Contact { Address1 = first, Address2 = second };
                    var mapped = ContactMapper.Map(source);
                    var updated = new ContactDto {
                        Address1 = new AddressDto { City = "old first" },
                        Address2 = new AddressDto { City = "old second" }
                    };
                    var oldFirst = updated.Address1;
                    var oldSecond = updated.Address2;
                    ContactMapper.Update(source, updated);
                    if (object.ReferenceEquals(oldFirst, updated.Address1) ||
                        object.ReferenceEquals(oldSecond, updated.Address2)) return false;
                    foreach (var result in new[] { mapped, updated }) {
                        if (result.Address1?.City != first.City || result.Address2?.City != second.City ||
                            object.ReferenceEquals(result.Address1, result.Address2)) return false;
                    }
                    first.City = "changed first";
                    second.City = "changed second";
                    if (mapped.Address1!.City != "Kyiv" || updated.Address1!.City != "Kyiv" ||
                        mapped.Address2!.City != "{{(sharedAddress ? "Kyiv" : "Lviv")}}" ||
                        updated.Address2!.City != mapped.Address2.City) return false;
                    mapped.Address1.City = "changed target";
                    if (mapped.Address2.City == "changed target" || updated.Address1.City != "Kyiv") return false;
                    for (var mask = 0; mask < 4; mask++) {
                        source.Address1 = (mask & 1) == 0 ? first : null;
                        source.Address2 = (mask & 2) == 0 ? second : null;
                        var result = ContactMapper.Map(source);
                        ContactMapper.Update(source, updated);
                        if (result.Address1?.City != source.Address1?.City ||
                            result.Address2?.City != source.Address2?.City ||
                            updated.Address1?.City != source.Address1?.City ||
                            updated.Address2?.City != source.Address2?.City) return false;
                    }
                    return true;
                }
            }
            """);
    }
}
