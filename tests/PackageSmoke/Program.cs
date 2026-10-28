var source = new PersonInput(42, "Ada", ["original"], "internal only");
var result = SmokeMappings.Map(source);
source.Tags!.Add("mutation");
if (result.Id != 42 || result.DisplayName != "Ada" || result.Tags?.Count != 1 || NullableSmokeMappings.Map(null) is not null)
{
    throw new InvalidOperationException("The packed mapper did not preserve mapping and collection-copy behavior.");
}

if (typeof(SmokeMappings).Assembly.GetReferencedAssemblies().Any(reference =>
    reference.Name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) ||
    reference.Name == "XBullet.EasyMapping.Generator"))
{
    throw new InvalidOperationException("Compiler tooling leaked into consumer runtime references.");
}

Console.WriteLine("Packed mapper consumer passed.");
