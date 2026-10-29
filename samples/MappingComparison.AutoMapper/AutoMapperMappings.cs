using AutoMapper;
using MappingComparison.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace MappingComparison.AutoMapper;

public sealed class AutoMapperMappings
{
    private readonly IMapper _mapper;

    public AutoMapperMappings(bool allowNullCollections = false)
    {
        Configuration = new MapperConfiguration(configuration =>
        {
            configuration.AllowNullCollections = allowNullCollections;
            configuration.AddProfile<ComparisonProfile>();
        }, NullLoggerFactory.Instance);

        Configuration.AssertConfigurationIsValid();
        _mapper = Configuration.CreateMapper();
    }

    public MapperConfiguration Configuration
    {
        get;
    }

    public PersonDto? ToDto(PersonInput? source) => _mapper.Map<PersonDto?>(source);

    public void Update(ProfileUpdate source, EditableProfile target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        _mapper.Map(source, target);
    }
}
