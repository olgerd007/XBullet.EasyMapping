using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters.Json;
using MappingComparison.AutoMapper;
using MappingComparison.Contracts;
using MappingComparison.EasyMapping;
using MappingComparison.Facet;
using MappingComparison.Manual;
using MappingComparison.Mapperly;

namespace MappingComparison.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.Full]
public class PersonMappingBenchmarks
{
    private PersonInput _source = null!;
    private AutoMapperMappings _autoMapper = null!;
    private AutoMapperMappings _nullableAutoMapper = null!;

    [ParamsAllValues]
    public PersonScenario Scenario
    {
        get; set;
    }

    [GlobalSetup]
    public void Setup()
    {
        _source = PersonWorkload.Create(Scenario);
        foreach (var mapper in MapperCatalog.Create())
        {
            MapperCatalog.Validate(mapper, _source);
        }

        _autoMapper = new AutoMapperMappings();
        _nullableAutoMapper = new AutoMapperMappings(allowNullCollections: true);
        _autoMapper.Configuration.CompileMappings();
        _nullableAutoMapper.Configuration.CompileMappings();
        _ = _autoMapper.ToDto(_source);
        _ = _nullableAutoMapper.ToDto(_source);
    }

    [Benchmark(Baseline = true)]
    public PersonDto? Manual() => ManualMappings.ToDto(_source);

    [Benchmark]
    public PersonDto? AutoMapper() => _autoMapper.ToDto(_source);

    [Benchmark]
    public PersonDto? AutoMapperNullable() => _nullableAutoMapper.ToDto(_source);

    [Benchmark]
    public PersonDto? Mapperly() => DtoMappings.ToDto(_source);

    [Benchmark]
    public PersonDto? MapperlyCopying() => CopyingDtoMappings.ToDto(_source);

    [Benchmark]
    public PersonFacet? Facet() => FacetMappings.ToDto(_source);

    [Benchmark]
    public PersonDto? EasyMapping() => PersonMappings.ToDto(_source);

    [Benchmark]
    public PersonDto? EasyMappingBefore() => PreOptimizationMappings.ToDto(_source);
}
