using AutoMapper;
using MappingComparison.Contracts;

namespace MappingComparison.AutoMapper;

public sealed class ComparisonProfile : Profile
{
    public ComparisonProfile()
    {
        CreateMap<AddressInput, AddressDto>();
        CreateMap<PersonInput, PersonDto>()
            .ForCtorParam(nameof(PersonDto.DisplayName), options => options.MapFrom(source => source.Name));
        CreateMap<ProfileUpdate, EditableProfile>()
            .ForMember(target => target.Id, options => options.Ignore())
            .ForMember(target => target.ServerNote, options => options.Ignore());
    }
}
