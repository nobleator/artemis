module OverpassBatch

#r "nuget: FSharp.Data"
#r "nuget: Thoth.Json.Net"
#load "DomainTypes.fs"

open FSharp.Data
open Microsoft.FSharp.Reflection
open System.Threading
open DomainTypes

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
    | Category.Library       -> "[building][amenity=library]"
    | Category.School        -> "[building][amenity=school]"
    | Category.Park          -> "[leisure=park]"
    | Category.Grocery       -> "[building][shop=supermarket]"
    | Category.CoffeeShop    -> "[building][amenity=cafe][cuisine=coffee_shop]"
    | Category.Airport       -> "[aeroway=terminal]"
    | Category.TrainStation  -> "[building][building=train_station]"
    | Category.BusStation    -> "[building][amenity=bus_station]"
    | Category.PoliceStation -> "[building][amenity=police]"
    | Category.FireStation   -> "[building][amenity=fire_station]"
    | _ -> ""

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
    Thread.Sleep 1500
    Http.RequestString(url, body = FormValues [ "data", q ])
    |> OverpassResult.Parse
    |> fun r -> r.Elements |> Array.toList

let getTags (e: OverpassResult.Element) =
    e.Tags.JsonValue.Properties()
    |> Array.map (fun (k, v) -> k, v.AsString())
    |> Map.ofArray

let categoryRules : (Category * (string * string) list) list =
    [
        Category.Library,       [ "amenity", "library" ]
        Category.School,        [ "amenity", "school" ]
        Category.Park,          [ "leisure", "park" ]
        Category.Grocery,       [ "shop", "supermarket" ]
        Category.CoffeeShop,    [ "amenity", "cafe" ]
        Category.Airport,       [ "aeroway", "terminal" ]
        Category.TrainStation,  [ "building", "train_station" ]
        Category.BusStation,    [ "amenity", "bus_station" ]
        Category.PoliceStation, [ "amenity", "police" ]
        Category.FireStation,   [ "amenity", "fire_station" ]
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
    printfn "Hello from osm.fsx %A" region
    let query = buildBatchQuery region
    let elements = executeQuery query
    elements
    |> List.choose (fun e ->
        match classify e, e.Center with
        | Some cat, Some c -> Some (region, cat, c.Lat, c.Lon, e.Id)
        | _ -> None
    )
    |> Seq.toList
