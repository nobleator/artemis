namespace Artemis.Core.Models;

public sealed class CensusGeocoderRespone
{
    public Result Result { get; init; } = default!;
}

public sealed class Result
{
    public Input Input { get; init; } = default!;
    public AddressMatch[] AddressMatches { get; init; } = Array.Empty<AddressMatch>();
}

public sealed class Input
{
    public InputAddress Address { get; init; } = default!;
    public Benchmark Benchmark { get; init; } = default!;
}

public sealed class InputAddress
{
    public string Address { get; init; } = string.Empty;
}

public sealed class Benchmark
{
    public bool IsDefault { get; init; }
    public string BenchmarkDescription { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string BenchmarkName { get; init; } = string.Empty;
}

public sealed class AddressMatch
{
    public TigerLine TigerLine { get; init; } = default!;
    public Coordinates Coordinates { get; init; } = default!;
    public AddressComponents AddressComponents { get; init; } = default!;
    public string MatchedAddress { get; init; } = string.Empty;
}

public sealed class TigerLine
{
    public string Side { get; init; } = string.Empty;
    public string TigerLineId { get; init; } = string.Empty;
}

public sealed class Coordinates
{
    public double X { get; init; }
    public double Y { get; init; }
}

public sealed class AddressComponents
{
    public string Zip { get; init; } = string.Empty;
    public string StreetName { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string ToAddress { get; init; } = string.Empty;

    // Kept for completeness, but still minimal
    public string PreType { get; init; } = string.Empty;
    public string PreDirection { get; init; } = string.Empty;
    public string SuffixDirection { get; init; } = string.Empty;
    public string SuffixType { get; init; } = string.Empty;
    public string SuffixQualifier { get; init; } = string.Empty;
    public string PreQualifier { get; init; } = string.Empty;
}
