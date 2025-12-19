using System.Net.Http.Json;
using System.Web;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Services;

public class LocationService(IHttpClientFactory httpClientFactory, ILocationRepository locationRepo) : ILocationService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILocationRepository _locationRepo = locationRepo;
    // Reference: https://geocoding.geo.census.gov/geocoder/
    const string baseUrl = "https://geocoding.geo.census.gov";
    public async Task<Location> GeocodeAsync(Location location, CancellationToken ct = default)
    {
        Console.WriteLine($"Geocoding {location.Name}...");
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);
            var uri = "geocoder/locations/onelineaddress?benchmark=4&format=json&address=" + HttpUtility.UrlEncode(location.Address);
            var resp = await client.GetAsync(uri, ct);
            resp.EnsureSuccessStatusCode();
            var data = await resp.Content.ReadFromJsonAsync<CensusGeocoderRespone>(ct);
            Console.WriteLine(data);
            var match = data?.Result?.AddressMatches?.FirstOrDefault();
            if (match != null)
            {
                location.Latitude = match.Coordinates.Y;
                location.Longitude = match.Coordinates.X;
                Console.WriteLine("Location updated, saving...");
                location = await _locationRepo.UpdateAsync(location, ct);
                Console.WriteLine("Location saved.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception encountered while geocoding: {ex}");
        }
        return location;
    }
}