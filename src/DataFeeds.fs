namespace DataFeeds

open FSharp.Data
open DomainTypes
open Microsoft.FSharp.Reflection
open System.Web
open System.Threading
open Category

module OverpassBatch =
    type OverpassResult = JsonProvider<"overpass_sample.json">

    let [<Literal>] url = "https://www.overpass-api.de/api/interpreter"

    let regions =
        Map.ofList [
            // "London", (51.470050, -0.136642, 51.539823, -0.043430)
            NewYork, (40.696951, -74.022437, 40.758613, -73.952075)
        ]

    let getAllCategories =
        FSharpType.GetUnionCases typeof<Category>
        |> Array.map (fun c -> FSharpValue.MakeUnion(c, [||]) :?> Category)
        |> Array.toList

    let getTagFilter cat =
        match cat with
        // | Category.Library       -> "[building][amenity=library]"
        // | Category.School        -> "[building][amenity=school]"
        // | Category.Park          -> "[leisure=park]"
        // | Category.Grocery       -> "[building][shop=supermarket]"
        // | Category.CoffeeShop    -> "[building][amenity=cafe][cuisine=coffee_shop]"
        | Category.Airport       -> "[aeroway=terminal]"
        // | Category.TrainStation  -> "[building][building=train_station]"
        // | Category.BusStation    -> "[building][amenity=bus_station]"
        // | Category.PoliceStation -> "[building][amenity=police]"
        // | Category.FireStation   -> "[building][amenity=fire_station]"

    let buildBatchQuery region =
        match regions.TryFind region with
        | None -> failwith "Unknown region"
        | Some (a, b, c, d) ->
            let filters =
                getAllCategories
                |> List.map (fun cat -> $"nwr{getTagFilter cat}({a},{b},{c},{d});")
                |> String.concat "\n"
            $"[out:json];(\n{filters}\n);out center;"

    let executeQuery q =
        try
            Http.RequestString(
                url,
                body = FormValues [ "data", q ], 
                timeout = 180000
            )
            |> OverpassResult.Parse
            |> fun r -> r.Elements |> Array.toList
        with
        | ex ->
            printfn "Error: %s" ex.Message
            printfn "Query: %s" q
            reraise()
    
    let rateLimitSemaphore = new System.Threading.SemaphoreSlim(1)
    let minDelayMs = 1500
    let executeThrottledQueryAsync q = async {
        do! rateLimitSemaphore.WaitAsync() |> Async.AwaitTask
        try
            do! Async.Sleep minDelayMs
            return executeQuery q
        finally
            rateLimitSemaphore.Release() |> ignore
    }

    let getTags (e: OverpassResult.Element) =
        e.Tags.JsonValue.Properties()
        |> Array.map (fun (k, v) -> k, v.AsString())
        |> Map.ofArray

    let categoryRules : (Category * (string * string) list) list =
        [
            // Category.Library,       [ "amenity", "library" ]
            // Category.School,        [ "amenity", "school" ]
            // Category.Park,          [ "leisure", "park" ]
            // Category.Grocery,       [ "shop", "supermarket" ]
            // Category.CoffeeShop,    [ "amenity", "cafe" ]
            Category.Airport,       [ "aeroway", "terminal" ]
            // Category.TrainStation,  [ "building", "train_station" ]
            // Category.BusStation,    [ "amenity", "bus_station" ]
            // Category.PoliceStation, [ "amenity", "police" ]
            // Category.FireStation,   [ "amenity", "fire_station" ]
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
        printfn $"Geocoding {location.Name}..."
        match location.Address with
        | Some address ->
            try
                let uri = $"{baseUrl}/geocoder/locations/onelineaddress?benchmark=4&format=json&address={HttpUtility.UrlEncode address}"
                let response = Http.RequestString uri
                let data = CensusResponse.Parse response
                match data.Result.AddressMatches with
                | [||] ->
                    printfn $"No matches found for {location.Name}"
                    location
                | matches ->
                    let firstMatch = matches.[0]
                    printfn "Location found."
                    { location with Lat = Some firstMatch.Coordinates.Y; Lon = Some firstMatch.Coordinates.X }
            with
            | ex ->
                printfn $"Exception encountered while geocoding: {ex}"
                location
        | _ ->
            printfn "No address available to geocode"
            location
