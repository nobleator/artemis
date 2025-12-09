namespace Artemis.Core.Models;

public sealed record OverpassResponse(
    double Version,
    string Generator,
    Osm3sInfo Osm3s,
    List<Element> Elements
);

public sealed record Osm3sInfo(
    string Timestamp_Osm_Base,
    string Copyright
);

public sealed record Element(
    string Type,
    long Id,
    Center Center,
    List<long>? Nodes,
    Dictionary<string, string>? Tags
);

public sealed record Center(
    double Lat,
    double Lon
);

