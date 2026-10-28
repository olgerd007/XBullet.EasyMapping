using XBullet.EasyMapping.Generator.Tests.ComplexModels;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ComplexModelMappingTests
{
    [Fact]
    public void Maps_a_populated_graph_with_inherited_identity_required_init_members_and_constructor_defaults()
    {
        var source = CreateOrder();
        var result = new OrderMapper("missing").Map(source);

        Assert.Equal(source.Id, result.Id);
        Assert.Equal(source.Name, result.DisplayName);
        Assert.Equal(source.CreatedAt, result.CreatedAt);
        Assert.Equal(source.CreatedAt.Offset, result.CreatedAt.Offset);
        Assert.Equal(source.Status, result.Status);
        Assert.Equal("web", result.Channel);
        var customer = Assert.IsType<CustomerDto>(result.Customer);
        Assert.Equal("Ada", customer.Name);
        var address = Assert.IsType<AddressDto>(customer.ShippingAddress);
        Assert.Equal("Kyiv", address.City);
        Assert.Equal(["primary"], address.Labels);
        Assert.Collection(result.Lines!,
            line =>
            {
                Assert.Equal("SKU-1", line.Sku);
                Assert.Equal(new Money(12.34m), line.UnitPrice);
                Assert.Equal(2L, line.Quantity);
                Assert.Equal("warehouse-A", line.Reference);
                Assert.Collection(line.Discounts!,
                    discount => Assert.Equal(new DiscountDto("PROMO", 1.5m), discount),
                    discount => Assert.Null(discount));
            },
            line =>
            {
                Assert.Equal("SKU-2", line.Sku);
                Assert.Equal("missing", line.Reference);
                Assert.NotNull(line.Discounts);
                Assert.Empty(line.Discounts);
            });
        Assert.Equal(["priority", null, string.Empty], result.Notes);
    }

    [Fact]
    public void Source_mutations_cannot_change_mapped_collections_or_nested_objects()
    {
        var source = CreateOrder();
        var result = new OrderMapper("missing").Map(source);
        source.Customer!.Name = "changed";
        source.Customer.ShippingAddress!.City = "changed";
        source.Customer.ShippingAddress.Labels![0] = "changed";
        source.Lines![0].Discounts![0]!.Code = "changed";
        source.Lines.Clear();
        source.Notes![0] = "changed";
        source.Batches![0]![0] = 99;

        Assert.Equal("Ada", result.Customer!.Name);
        Assert.Equal("Kyiv", result.Customer.ShippingAddress!.City);
        Assert.Equal(["primary"], result.Customer.ShippingAddress.Labels);
        Assert.Equal("PROMO", result.Lines![0].Discounts![0]!.Code);
        Assert.Equal(["SKU-1", "SKU-2"], result.Lines.Select(line => line.Sku));
        Assert.Equal(["priority", null, string.Empty], result.Notes);
        Assert.Equal(1L, result.Batches![0]![0]);
    }

    [Fact]
    public void Null_children_and_collections_remain_null()
    {
        var result = new OrderMapper("missing").Map(new OrderInput { Name = "minimal" });

        Assert.Equal("minimal", result.DisplayName);
        Assert.Null(result.Customer);
        Assert.Null(result.Lines);
        Assert.Null(result.Notes);
        Assert.Null(result.Batches);
    }

    [Fact]
    public void Empty_collections_remain_empty_instead_of_becoming_null()
    {
        var source = new OrderInput
        {
            Name = "empty",
            Lines = [],
            Notes = [],
            Batches = [],
            Customer = new CustomerInput
            {
                Name = "Ada",
                ShippingAddress = new AddressInput { City = "Kyiv", Labels = [] }
            }
        };
        var result = new OrderMapper("missing").Map(source);

        Assert.NotNull(result.Lines);
        Assert.Empty(result.Lines);
        Assert.NotNull(result.Notes);
        Assert.Empty(result.Notes);
        Assert.NotNull(result.Batches);
        Assert.Empty(result.Batches);
        Assert.NotNull(result.Customer!.ShippingAddress!.Labels);
        Assert.Empty(result.Customer.ShippingAddress.Labels);
        Assert.NotSame(source.Lines, result.Lines);
        Assert.NotSame(source.Notes, result.Notes);
    }

    [Fact]
    public void Jagged_collections_preserve_null_rows_empty_rows_and_nullable_numeric_boundaries()
    {
        var result = new OrderMapper("missing").Map(CreateOrder());

        Assert.Collection(result.Batches!,
            row => Assert.Equal(new long?[] { 1, null, int.MaxValue }, row),
            row => Assert.Null(row),
            row =>
            {
                Assert.NotNull(row);
                Assert.Empty(row);
            },
            row => Assert.Equal(new long?[] { int.MinValue }, row));
    }

    [Fact]
    public void Repeated_mapping_produces_independent_graphs()
    {
        var source = CreateOrder();
        var mapper = new OrderMapper("missing");
        var first = mapper.Map(source);
        var second = mapper.Map(source);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Customer, second.Customer);
        Assert.NotSame(first.Customer!.ShippingAddress, second.Customer!.ShippingAddress);
        Assert.NotSame(first.Customer.ShippingAddress!.Labels, second.Customer.ShippingAddress!.Labels);
        Assert.NotSame(first.Lines, second.Lines);
        Assert.NotSame(first.Lines![0], second.Lines![0]);
        Assert.NotSame(first.Lines[0].Discounts, second.Lines[0].Discounts);
        Assert.NotSame(first.Lines[0].Discounts![0], second.Lines[0].Discounts![0]);
        Assert.NotSame(first.Batches![0], second.Batches![0]);
        Assert.Equal(first.Lines.Select(line => line.Sku), second.Lines.Select(line => line.Sku));
    }

    [Fact]
    public void Shared_source_references_are_copied_per_occurrence_without_aliasing()
    {
        var shared = new DiscountInput { Code = "shared", Amount = 3m };
        var discounts = new List<DiscountInput?> { shared };
        var source = new OrderInput
        {
            Name = "shared references",
            Lines = [new("A", 1m, 1, discounts, null), new("B", 2m, 1, discounts, null)]
        };
        var result = new OrderMapper("missing").Map(source);

        Assert.NotSame(result.Lines![0].Discounts, result.Lines[1].Discounts);
        Assert.NotSame(result.Lines[0].Discounts![0], result.Lines[1].Discounts![0]);
        shared.Code = "changed";
        Assert.All(result.Lines, line => Assert.Equal("shared", Assert.Single(line.Discounts!)!.Code));
    }

    [Theory]
    [InlineData("unassigned")]
    [InlineData("")]
    public void Instance_converter_state_is_used_for_nullable_value_objects(string fallback)
    {
        var source = CreateOrder();
        var result = new OrderMapper(fallback).Map(source);

        Assert.Equal("warehouse-A", result.Lines![0].Reference);
        Assert.Equal(fallback, result.Lines[1].Reference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(257)]
    public void Maps_ordered_batches_without_dropping_or_reusing_line_items(int count)
    {
        var source = new OrderInput
        {
            Name = "batch",
            Lines = Enumerable.Range(0, count).Select(index =>
                new OrderLineInput("SKU-" + index, index + 0.25m, index, null, null)).ToList()
        };
        var result = new OrderMapper("missing").Map(source);

        Assert.NotNull(result.Lines);
        Assert.Equal(source.Lines.Select(line => line.Code), result.Lines.Select(line => line.Sku));
        Assert.Equal(source.Lines.Select(line => (long)line.Quantity), result.Lines.Select(line => line.Quantity));
        Assert.Equal(source.Lines.Select(line => line.UnitPrice), result.Lines.Select(line => line.UnitPrice.Amount));
        Assert.All(result.Lines, line => Assert.Null(line.Discounts));
        Assert.NotSame(source.Lines, result.Lines);
    }

    [Fact]
    public void Updates_replace_nested_graphs_and_collections_while_preserving_ignored_server_state()
    {
        var source = CreateOrder();
        var target = CreateTarget();
        var oldCustomer = target.Customer;
        var oldLines = target.Lines;
        var oldNotes = target.Notes;

        new OrderMapper("missing").Update(new OrderUpdateInput(source.Customer, source.Lines, source.Notes), target);

        Assert.Equal(new OrderId(Guid.Empty), target.Id);
        Assert.Equal(OrderStatus.Shipped, target.Status);
        Assert.Equal(17, target.Version);
        Assert.Equal("Ada", target.Customer!.Name);
        Assert.NotSame(oldCustomer, target.Customer);
        Assert.NotSame(oldLines, target.Lines);
        Assert.NotSame(oldNotes, target.Notes);
        Assert.NotSame(source.Lines, target.Lines);
        source.Customer!.ShippingAddress!.City = "changed";
        source.Lines![0].Discounts![0]!.Code = "changed";
        Assert.Equal("Kyiv", target.Customer.ShippingAddress!.City);
        Assert.Equal("PROMO", target.Lines![0].Discounts![0]!.Code);
    }

    [Fact]
    public void Explicit_nulls_in_updates_clear_values_instead_of_behaving_like_patch_fields()
    {
        var target = CreateTarget();
        new OrderMapper("missing").Update(new OrderUpdateInput(null, null, null), target);

        Assert.Null(target.Customer);
        Assert.Null(target.Lines);
        Assert.Null(target.Notes);
        Assert.Equal(OrderStatus.Shipped, target.Status);
        Assert.Equal(17, target.Version);
    }

    [Fact]
    public void Empty_updates_replace_collections_with_empty_copies()
    {
        var target = CreateTarget();
        var source = new OrderUpdateInput(null, [], []);
        new OrderMapper("missing").Update(source, target);

        Assert.NotNull(target.Lines);
        Assert.Empty(target.Lines);
        Assert.NotNull(target.Notes);
        Assert.Empty(target.Notes);
        Assert.NotSame(source.Lines, target.Lines);
        Assert.NotSame(source.Notes, target.Notes);
    }

    [Fact]
    public void Guards_report_argument_names_and_reject_a_null_update_before_mutating_the_target()
    {
        var mapper = new OrderMapper("missing");
        var target = CreateTarget();
        var customer = target.Customer;
        var lines = target.Lines;

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => mapper.Map(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => mapper.Update(null!, target)).ParamName);
        Assert.Equal("target", Assert.Throws<ArgumentNullException>(() => mapper.Update(new OrderUpdateInput(null, null, null), null!)).ParamName);
        Assert.Same(customer, target.Customer);
        Assert.Same(lines, target.Lines);
        Assert.Equal(17, target.Version);
    }

    [Fact]
    public void Scalar_boundaries_and_unknown_values_of_the_same_enum_are_preserved()
    {
        var source = new OrderInput
        {
            Name = string.Empty,
            Status = (OrderStatus)999,
            Lines =
            [
                new(string.Empty, decimal.MaxValue, int.MaxValue, null, new ProductCode(string.Empty)),
                new(string.Empty, decimal.MinValue, int.MinValue, null, new ProductCode(string.Empty))
            ]
        };
        var result = new OrderMapper("missing").Map(source);

        Assert.Equal(new OrderId(Guid.Empty), result.Id);
        Assert.Equal(string.Empty, result.DisplayName);
        Assert.Equal((OrderStatus)999, result.Status);
        Assert.Collection(result.Lines!,
            line =>
            {
                Assert.Equal(decimal.MaxValue, line.UnitPrice.Amount);
                Assert.Equal((long)int.MaxValue, line.Quantity);
                Assert.Equal(string.Empty, line.Reference);
            },
            line =>
            {
                Assert.Equal(decimal.MinValue, line.UnitPrice.Amount);
                Assert.Equal((long)int.MinValue, line.Quantity);
                Assert.Equal(string.Empty, line.Reference);
            });
    }

    private static OrderInput CreateOrder() => new()
    {
        Id = new OrderId(Guid.Parse("c27fe1af-07bc-4ca9-8fe5-e71f958a59dc")),
        AuditTag = "internal audit only",
        Name = "Wholesale order",
        CreatedAt = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.FromHours(2)),
        Status = OrderStatus.Confirmed,
        Customer = new CustomerInput
        {
            Name = "Ada",
            ShippingAddress = new AddressInput { City = "Kyiv", Labels = ["primary"] }
        },
        Lines =
        [
            new("SKU-1", 12.34m, 2, [new DiscountInput { Code = "PROMO", Amount = 1.5m }, null], new ProductCode("warehouse-A")),
            new("SKU-2", 0m, 0, [], null)
        ],
        Notes = ["priority", null, string.Empty],
        Batches = [[1, null, int.MaxValue], null, [], [int.MinValue]]
    };

    private static MutableOrderDto CreateTarget() => new()
    {
        Id = new OrderId(Guid.Empty),
        Status = OrderStatus.Shipped,
        Version = 17,
        Customer = new CustomerDto("old", null),
        Lines = [new OrderLineDto("old", new Money(0m), 0, null, "old")],
        Notes = ["old"]
    };
}
