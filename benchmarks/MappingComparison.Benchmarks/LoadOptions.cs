using System.Globalization;

namespace MappingComparison.Benchmarks;

public sealed record LoadOptions(int Seconds, int Repetitions, int[] Workers, string Output)
{
    public static LoadOptions Parse(string[] args)
    {
        var seconds = 3;
        var repetitions = 3;
        int[] workers = [1, Math.Min(4, Environment.ProcessorCount)];
        var output = "artifacts/performance/load.json";
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 == args.Length)
            {
                throw new ArgumentException($"Missing value for {args[index]}.");
            }

            var value = args[index + 1];
            switch (args[index])
            {
                case "--seconds":
                    seconds = ParseBounded(value, 1, 300);
                    break;
                case "--repetitions":
                    repetitions = ParseBounded(value, 1, 20);
                    break;
                case "--workers":
                    workers = value.Split(',').Select(item => ParseBounded(item, 1, 256)).Distinct().ToArray();
                    break;
                case "--output":
                    ArgumentException.ThrowIfNullOrWhiteSpace(value);
                    output = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option {args[index]}.");
            }
        }

        return new LoadOptions(seconds, repetitions, workers.Distinct().ToArray(), output);
    }

    private static int ParseBounded(string value, int minimum, int maximum)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            || result < minimum || result > maximum)
        {
            throw new ArgumentException($"Expected an integer between {minimum} and {maximum}: {value}.");
        }

        return result;
    }
}
