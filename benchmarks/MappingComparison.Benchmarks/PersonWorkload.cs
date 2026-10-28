using MappingComparison.Contracts;

namespace MappingComparison.Benchmarks;

public static class PersonWorkload
{
    public const int DatasetSize = 256;

    public static PersonInput Create(PersonScenario scenario, int id = 42)
    {
        var count = scenario switch
        {
            PersonScenario.NullCollections => 0,
            PersonScenario.Small => 4,
            PersonScenario.Large => 128,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        return new PersonInput(
            id,
            $"Person {id}",
            count == 0 ? null : new AddressInput("Kyiv", $"Street {id}"),
            count == 0 ? null : Enumerable.Range(0, count).Select(index => $"tag-{id}-{index}").ToList(),
            count == 0 ? null : Enumerable.Range(0, count)
                .Select(index => new AddressInput($"City {index}", $"Street {id}-{index}")).ToList(),
            "not mapped");
    }

    public static PersonInput[] CreateDataset(PersonScenario scenario) =>
        Enumerable.Range(1, DatasetSize).Select(id => Create(scenario, id)).ToArray();
}
