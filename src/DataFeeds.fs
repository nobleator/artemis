namespace DataFeeds

open FSharp.Data
open DomainTypes
open System.Web

module HttpUtils =
    open System.Threading

    let httpRequestWithRetry url body timeout maxRetries =
        let rec attempt retryCount =
            try
                Http.RequestString(url, body = body, timeout = timeout)
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

    let regions =
        Map.ofList [
            // "London", (51.470050, -0.136642, 51.539823, -0.043430)
            NewYork, (40.696951, -74.022437, 40.758613, -73.952075)
            WashingtonDC, (38.761151, -77.327465, 39.058224, -76.8234205)
        ]

    let getAllCategories =
        Enum.GetValues typeof<Category>
        |> Seq.cast<Category>
        |> Seq.toList

    let getTagFilter cat =
        match cat with
        | Category.Job           -> "[TODO=IMPOSSIBLE_TO_MATCH]"
        | Category.Airport       -> "[aeroway=terminal]"
        | Category.BusStation    -> "[building][amenity=bus_station]"
        | Category.CoffeeShop    -> "[building][amenity=cafe][cuisine=coffee_shop]"
        | Category.FireStation   -> "[building][amenity=fire_station]"
        | Category.Grocery       -> "[building][shop=supermarket]"
        | Category.Library       -> "[building][amenity=library]"
        | Category.Park          -> "[leisure=park]"
        | Category.PoliceStation -> "[building][amenity=police]"
        | Category.School        -> "[building][amenity=school]"
        | Category.TrainStation  -> "[building][building=train_station]"
        | Category.WholeFoods    -> "[shop=supermarket][brand=\"Whole Foods Market\"]"
        | Category.TraderJoes    -> "[shop=supermarket][brand=\"Trader Joe's\"]"
        | Category.Giant         -> "[shop=supermarket][brand=Giant]"
        | Category.Safeway       -> "[shop=supermarket][brand=Safeway]"
        | Category.HarrisTeeter  -> "[shop=supermarket][brand=\"Harris Teeter\"]"
        | Category.BikeTrail     -> "[bicycle=yes]"
        | _ -> failwith "Uh oh, didn't expect this!"

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
            httpRequestWithRetry url (FormValues [ "data", q ]) 180000 3
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
