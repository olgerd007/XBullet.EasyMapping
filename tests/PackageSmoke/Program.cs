var source = new PersonInput(42, "Ada", ["original"], "internal only");
var result = SmokeMappings.Map(source);
var wholeSourceResult = SmokeMappings.MapWithWholeSource(source);
source.Tags!.Add("mutation");
if (result.Id != 42 || result.DisplayName != "Ada" || result.Tags?.Count != 1 || NullableSmokeMappings.Map(null) is not null)
{
    throw new InvalidOperationException("The packed mapper did not preserve mapping and collection-copy behavior.");
}

if (wholeSourceResult.Id != 42 || wholeSourceResult.DisplayName != "42:Ada" || wholeSourceResult.Tags?.Count != 1)
{
    throw new InvalidOperationException("The packed mapper did not apply the whole-source helper with strict source coverage.");
}

var missingOptions = OptionSmokeMappings.Map(new OptionSmokeInput(null, null, null));
var child = new OptionSmokeChild(7);
var values = new List<int> { 3 };
var presentOptions = OptionSmokeMappings.Map(new OptionSmokeInput(child, 5, values));
if (missingOptions.Child is not null || missingOptions.Count != 0 || missingOptions.NestedValue != 0 ||
    missingOptions.Values.Count != 0 || !ReferenceEquals(child, presentOptions.Child) ||
    presentOptions.Count != 5 || presentOptions.NestedValue != 7 || presentOptions.Values.Count != 1 ||
    ReferenceEquals(values, presentOptions.Values))
{
    throw new InvalidOperationException("The packed mapper did not apply reference assignment and null-safe member options.");
}

if (typeof(SmokeMappings).Assembly.GetReferencedAssemblies().Any(reference =>
    reference.Name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) ||
    reference.Name == "XBullet.EasyMapping.Generator"))
{
    throw new InvalidOperationException("Compiler tooling leaked into consumer runtime references.");
}

Console.WriteLine("Packed mapper consumer passed.");
