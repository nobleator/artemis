namespace DataFeeds

open FSharp.Data
open DomainTypes
open System.Web

// module GlobalUtils =
//     let regions =
//         Map.ofList [
//             // London, (51.470050, -0.136642, 51.539823, -0.043430)
//             NewYork,         (40.696951,  -74.022437, 40.758613,  -73.952075)
//             WashingtonDC,    (38.64287,   -77.555545, 39.140061,  -76.728104)
//             SanDiego,        (32.614624, -117.321743, 32.830673, -116.972894)
//             LosAngeles,      (34.032911, -118.283255, 34.069655, -118.234676)
//             Chicago,         (41.868164,  -87.652981, 41.907202,  -87.612951)
//             Houston,         (29.735866,  -95.384607, 29.774127,  -95.348109)
//             Phoenix,         (33.430866, -112.096876, 33.470053, -112.048969)
//             Philadelphia,    (39.939152,  -75.176011, 39.965578,  -75.14382)
//             SanAntonio,      (29.410548,  -98.502244, 29.435556,  -98.469192)
//             Dallas,          (32.766481,  -96.815506, 32.796213,  -96.780762)
//             Austin,          (30.253746,  -97.755432, 30.281765,  -97.729263)
//             Jacksonville,    (30.316138,  -81.681806, 30.342879,  -81.647116)
//             FortWorth,       (32.744784,  -97.339758, 32.766786,  -97.312088)
//             SanJose,         (37.324715, -121.904297, 37.352759, -121.870651)
//             Columbus,        (39.945157,  -83.021484, 39.976837,  -82.977674)
//             Charlotte,       (35.216774,  -80.860214, 35.238742,  -80.828171)
//             Indianapolis,    (39.756072,  -86.176376, 39.780155,  -86.14212)
//             SanFrancisco,    (37.7595,   -122.4494,   37.8018,   -122.3936)
//             Seattle,         (47.593742, -122.352622, 47.626504, -122.319908)
//             Denver,          (39.734211, -105.005844, 39.762921, -104.977226)
//             Boston,          (42.34718,   -71.076146, 42.372307,  -71.038284)
//             ElPaso,          (31.752233, -106.502113, 31.774822, -106.478271)
//             Nashville,       (36.147074,  -86.795387, 36.176452,  -86.766663)
//             Detroit,         (42.324146,  -83.06549,  42.348189,  -83.032913)
//             OklahomaCity,    (35.457948,  -97.532883, 35.481077,  -97.502136)
//             Portland,        (45.512223, -122.694244, 45.531525, -122.665062)
//             LasVegas,        (36.153229, -115.166893, 36.180212, -115.129547)
//             Memphis,         (35.135331,  -90.061188, 35.156097,  -90.032349)
//             Louisville,      (38.240272,  -85.769806, 38.264954,  -85.734558)
//             Baltimore,       (39.279842,  -76.626511, 39.30798,   -76.590233)
//             Milwaukee,       (43.028005,  -87.923355, 43.05248,   -87.895355)
//             Albuquerque,     (35.07709,  -106.66301,  35.096577, -106.634674)
//             Tucson,          (32.209934, -110.988464, 32.235504, -110.958843)
//             Fresno,          (36.727974, -119.79554,  36.752308, -119.766769)
//             Sacramento,      (38.566666, -121.503296, 38.588528, -121.478882)
//             Mesa,            (33.402946, -111.850281, 33.428528, -111.821899)
//             KansasCity,      (39.089478,  -94.591064, 39.111328,  -94.567566)
//             Atlanta,         (33.740498,  -84.404411, 33.774845,  -84.365387)
//             Omaha,           (41.246788,  -95.955238, 41.267685,  -95.930344)
//             ColoradoSprings, (38.826904, -104.834213, 38.848907, -104.807053)
//             Raleigh,         (35.767925,  -78.653275, 35.789364,  -78.628723)
//             Miami,           (25.756672,  -80.211258, 25.792282,  -80.183258)
//             LongBeach,       (33.762436, -118.207932, 33.786285, -118.181229)
//             VirginiaBeach,   (36.836052,  -76.062012, 36.858032,  -76.033325)
//             Oakland,         (37.788666, -122.279152, 37.815315, -122.254333)
//             Minneapolis,     (44.965313,  -93.28009,  44.989979,  -93.25325)
//             Tulsa,           (36.140587,  -95.998283, 36.161221,  -95.976318)
//             Tampa,           (27.936527,  -82.470856, 27.958233,  -82.44574)
//             Arlington,       (32.732208,  -97.111282, 32.756577,  -97.086655)
//             NewOrleans,      (29.941002,  -90.083618, 29.964153,  -90.056)
//         ]

module HttpUtils =
    open System.Threading

    let httpRequestWithRetry url timeout maxRetries =
        let rec attempt retryCount =
            try
                Http.RequestString(url, timeout = timeout, headers = [HttpRequestHeaders.UserAgent "Artemis/1.0 (https://github.com/nobleator/artemis)"])
            with
            | ex when retryCount < maxRetries ->
                let delayMs = int (90_000.0 * 2.0 ** float retryCount)
                printfn "Request failed (attempt %d/%d): %s. Retrying in %dms..." 
                    (retryCount + 1) maxRetries ex.Message delayMs
                Thread.Sleep(delayMs)
                attempt (retryCount + 1)
        attempt 0

    let httpRequestBodyWithRetry url body timeout maxRetries =
        let rec attempt retryCount =
            try
                Http.RequestString(url, body = body, timeout = timeout, headers = [HttpRequestHeaders.UserAgent "Artemis/1.0 (https://github.com/nobleator/artemis)"])
            with
            | ex when retryCount < maxRetries ->
                let delayMs = int (1500.0 * 2.0 ** float retryCount)
                printfn "Request failed (attempt %d/%d): %s. Retrying in %dms..." 
                    (retryCount + 1) maxRetries ex.Message delayMs
                Thread.Sleep(delayMs)
                attempt (retryCount + 1)
        attempt 0
    
    let rateLimitSemaphore = new SemaphoreSlim 1
    let minDelayMs = 1500
    let executeThrottledQueryAsync queryFunc = async {
        do! rateLimitSemaphore.WaitAsync() |> Async.AwaitTask
        try
            do! Async.Sleep minDelayMs
            return queryFunc
        finally
            rateLimitSemaphore.Release() |> ignore
    }

module OverpassBatch =
    open HttpUtils
    open System
    type OverpassResult = JsonProvider<"overpass_sample.json">

    let [<Literal>] url = "https://www.overpass-api.de/api/interpreter"

    let getAllCategories =
        Enum.GetValues typeof<Category>
        |> Seq.cast<Category>
        |> Seq.toList

    let getTagFilter cat =
        match cat with
        | Category.Job             -> "[TODO=IMPOSSIBLE_TO_MATCH]"
        | Category.Airport         -> "[aeroway=terminal]"
        | Category.BusStation      -> "[building][amenity=bus_station]"
        | Category.CoffeeShop      -> "[building][amenity=cafe][cuisine=coffee_shop]"
        | Category.FireStation     -> "[building][amenity=fire_station]"
        | Category.Grocery         -> "[building][shop=supermarket]"
        | Category.Library         -> "[building][amenity=library]"
        | Category.Park            -> "[leisure=park]"
        | Category.PoliceStation   -> "[building][amenity=police]"
        | Category.School          -> "[building][amenity=school]"
        | Category.TrainStation    -> "[building][building=train_station]"
        | Category.WholeFoods      -> "[shop=supermarket][brand=\"Whole Foods Market\"]"
        | Category.TraderJoes      -> "[shop=supermarket][brand=\"Trader Joe's\"]"
        | Category.Giant           -> "[shop=supermarket][brand=Giant]"
        | Category.Safeway         -> "[shop=supermarket][brand=Safeway]"
        | Category.HarrisTeeter    -> "[shop=supermarket][brand=\"Harris Teeter\"]"
        | Category.BikeTrail       -> "[bicycle=yes]"
        | Category.ComfortableDays -> "[TODO=IMPOSSIBLE_TO_MATCH]"
        | _ -> failwith "Uh oh, didn't expect this!"

    let buildBatchQuery (region: Region) =
        let { MinLat = a; MinLon = b; MaxLat = c; MaxLon = d } = region.BBox
        let filters =
            getAllCategories
            |> List.map (fun cat -> $"nwr{getTagFilter cat}({a},{b},{c},{d});")
            |> String.concat "\n"
        $"[out:json];(\n{filters}\n);out center;"

    let executeQuery q =
        try
            httpRequestBodyWithRetry url (FormValues [ "data", q ]) 180000 3
            |> OverpassResult.Parse
            |> fun r -> r.Elements |> Array.toList
        with
        | ex ->
            printfn "Error: %s" ex.Message
            printfn "Query: %s" q
            reraise()

    let getTags (e: OverpassResult.Element) =
        e.Tags.JsonValue.Properties()
        |> Array.map (fun (k, v) -> k, v.AsString())
        |> Map.ofArray

    let categoryRules : (Category * (string * string) list) list =
        [
            // Category.Job - skipped as it's impossible to match via tags
            Category.Airport,       [ "aeroway", "terminal" ]
            Category.BusStation,    [ "amenity", "bus_station" ]
            Category.CoffeeShop,    [ "amenity", "cafe"; "cuisine", "coffee_shop" ]
            Category.FireStation,   [ "amenity", "fire_station" ]
            Category.Library,       [ "amenity", "library" ]
            Category.Park,          [ "leisure", "park" ]
            Category.PoliceStation, [ "amenity", "police" ]
            Category.School,        [ "amenity", "school" ]
            Category.TrainStation,  [ "building", "train_station" ]
            Category.WholeFoods,    [ "shop", "supermarket"; "brand", "Whole Foods Market" ]
            Category.TraderJoes,    [ "shop", "supermarket"; "brand", "Trader Joe's" ]
            Category.Giant,         [ "shop", "supermarket"; "brand", "Giant" ]
            Category.Safeway,       [ "shop", "supermarket"; "brand", "Safeway" ]
            Category.HarrisTeeter,  [ "shop", "supermarket"; "brand", "Harris Teeter" ]
            // Need to place the more generic category after the more specific overlapping categories so that `classify` will work properly
            Category.Grocery,       [ "shop", "supermarket" ]
            Category.BikeTrail,     [ "bicycle", "yes" ]
        ]

    let classify (e: OverpassResult.Element) =
        let tags = getTags e
        categoryRules
        |> List.tryPick (fun (cat, reqs) ->
            if reqs |> List.forall (fun (k, v) -> tags |> Map.tryFind k = Some v)
            then Some cat
            else None
        )

    let execute region =
        printfn "Loading Overpass POI data for %A" region
        let query = buildBatchQuery region
        printfn "Query: %A" query
        let elements = executeQuery query
        elements
        |> List.choose (fun e ->
            match classify e, e.Center with
            | Some cat, Some c -> Some (region, cat, c.Lat, c.Lon, e.Id)
            | _ -> None
        )
        |> Seq.toList

module Geocoder =
    type CensusResponse = JsonProvider<"census_sample.json">
    
    let [<Literal>] baseUrl = "https://geocoding.geo.census.gov"
    
    let geocodeAsync (location: Location) =
        // printfn $"Geocoding {location.Name}..."
        match location.Address with
        | Some address ->
            try
                let uri = $"{baseUrl}/geocoder/locations/onelineaddress?benchmark=4&format=json&address={HttpUtility.UrlEncode address}"
                let response = Http.RequestString uri
                let data = CensusResponse.Parse response
                match data.Result.AddressMatches with
                | [||] ->
                    // printfn $"No matches found for {location.Name}"
                    location
                | matches ->
                    let firstMatch = matches.[0]
                    // printfn "Location found."
                    { location with Lat = Some firstMatch.Coordinates.Y; Lon = Some firstMatch.Coordinates.X }
            with
            | ex ->
                printfn $"Exception encountered while geocoding: {ex}"
                location
        | _ ->
            printfn "No address available to geocode"
            location

module OpenMeteoBatch =
    open HttpUtils
    type OpenMeteoResult = JsonProvider<"openmeteo_sample.json">

    // type DailyWeather = {
    //     Date: DateTime
    //     TempMin: decimal
    //     TempMax: decimal
    //     TempMean: decimal
    //     HumidityMean: float
    //     HumidityMax: float
    //     HumidityMin: float
    // }

    let [<Literal>] root = "https://archive-api.open-meteo.com/v1/archive?start_date=2015-01-01&end_date=2025-12-31&temperature_unit=fahrenheit&daily=temperature_2m_min,temperature_2m_mean,temperature_2m_max,relative_humidity_2m_min,relative_humidity_2m_max,relative_humidity_2m_mean,dew_point_2m_mean,dew_point_2m_max,dew_point_2m_min"

    let executeQuery url =
        try
            let data =
                httpRequestWithRetry url 180000 3
                |> OpenMeteoResult.Parse
                |> fun r -> r.Daily
            let test =
                data.Time
                |> Array.mapi (fun i date -> {
                    Date = date
                    TempMin = data.Temperature2mMin.[i]
                    TempMax = data.Temperature2mMax.[i]
                    TempMean = data.Temperature2mMean.[i]
                    HumidityMean = data.DewPoint2mMean.[i]
                    HumidityMax = data.DewPoint2mMax.[i]
                    HumidityMin = data.DewPoint2mMin.[i]
                })
                |> Array.toList
            // printfn "Response: %A" test
            test
        with 
        | ex ->
            printfn "Error: %s" ex.Message
            printfn "URL: %s" url
            reraise()

    // let isComfortable (day: DailyWeather) =
    //     // TODO make this user configurable
    //     // Comfortable days:
    //     // - min temperature 50 degF
    //     // - max temp 80 degF
    //     // - min relative humidity 30%
    //     // - max RH 50%
    //     day.TempMin > 30m && day.TempMax < 90m && day.HumidityMin > 10 && day.HumidityMax < 80

    // let percentComfortableDays (data: DailyWeather list) =
    //     let x =
    //         data
    //         |> List.filter isComfortable
    //         |> List.length
    //     printfn "%f of %f days are comfortable" (float x) (float data.Length)
    //     float x / float data.Length

    let execute region =
        // TODO return the raw data or compute the "comfortable days" score here first?
        printfn "Loading Open-Meteo historical data for %A" region
        let lat = (region.BBox.MinLat + region.BBox.MaxLat) / 2.
        let lon = (region.BBox.MinLon + region.BBox.MaxLon) / 2.
        sprintf "%s&latitude=%f&longitude=%f" root lat lon
        |> executeQuery
        |> List.choose (fun x -> Some(lat, lon, x))
        |> Seq.toList
        // let d =
        //     sprintf "%s&latitude=%f&longitude=%f" root lat lon
        //     |> executeQuery
        //     |> percentComfortableDays
        // printfn "Region %A has %f%% comfortable days" region d
        // lat, lon, d
