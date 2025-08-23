#r "nuget: FSharp.Data"
#r "nuget: Thoth.Json.Net"
#r @"../app/bin/debug/net9.0/WebClient.dll"

open FSharp.Data
open Microsoft.FSharp.Reflection
open System.Threading
open System.IO
open DomainTypes

type OverpassResult = JsonProvider<"overpass_sample.json">

let [<Literal>] url = "https://www.overpass-api.de/api/interpreter"
let regions =
    Map.ofList [
        "London", (51.470050, -0.136642, 51.539823, -0.043430)
        // "New York", (40.696951, -74.022437, 40.758613, -73.952075)
    ]

let getAllCategories =
    FSharpType.GetUnionCases typeof<Category>
    |> Array.map (fun case -> FSharpValue.MakeUnion(case, [||]) :?> Category)
    |> Array.toList

let getTagFilter cat = 
    match cat with
    | Library -> "[building][amenity=library]"
    | School -> "[building][amenity=school]"    
    | Park -> "[leisure=park]"
    | Grocery -> "[building][shop=supermarket]"
    | CoffeeShop -> "[building][amenity=cafe][cuisine=coffee_shop]"
    | Airport -> "[aeroway=terminal]"
    | TrainStation -> "[building][building=train_station]"
    | BusStation -> "[building][amenity=bus_station]"
    | PoliceStation -> "[building][amenity=police]"
    | FireStation -> "[building][amenity=fire_station]"

let buildQuery region filter =
    match regions.TryFind region with
    | Some (a, b, c, d) -> $"[out:json];nwr{filter}({a}, {b}, {c}, {d});out center;"
    | None -> failwith "Unknown region"    

let executeQuery q =
    Thread.Sleep 1500
    Http.RequestString(url, body = FormValues [ "data", q ])
    |> OverpassResult.Parse
    |> fun x -> x.Elements
    |> Array.toList

let writeJson (cat: Category) (osmData: OverpassResult.Element list) =
    try
        let rows =
            osmData
            |> List.choose (fun el ->
                match el.Center with
                | Some center ->
                    Some {
                        category = cat
                        source = "OSM"
                        source_xref = el.Id.ToString()
                        latitude = center.Lat
                        longitude = center.Lon
                    }
                | None -> None
            )

        // Using Thoth here for consistency with app encoders, but have to use the dotnet version
        let json = Thoth.Json.Net.Encode.toString 4 (Thoth.Json.Net.Encode.list (List.map POI.netEncoder rows))

        let filename = $"./out/poi/{Category.name cat}.json"
        File.WriteAllText(filename, json)

        printfn $"Wrote {List.length rows} records to {filename}"
    with ex ->
        printfn $"JSON write failed: {ex}"

regions.Keys
|> Seq.map (fun r ->
    getAllCategories
    |> List.map (fun c -> c, getTagFilter c)
    |> List.map (fun (c, f) -> c, buildQuery r f)
    |> List.map (fun (c, q) -> c, executeQuery q)
    |> List.map (fun (c, d) -> writeJson c d)
)
|> printfn "%A"