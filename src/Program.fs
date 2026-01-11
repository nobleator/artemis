open System
open System.IO
open DuckDB.NET.Data
open DomainTypes
open DataFeeds
open Tree.Criteria
open Data.Locations
open Data.Poi

type CommandOption =
    | LoadPoi = 0
    | Score = 1
    | LoadPoiAndScore = 2

module ArgumentParser =
    let parseCommand (arg: string) : CommandOption option =
        match arg.ToLower() with
        | "loadpoi" -> Some CommandOption.LoadPoi
        | "score" -> Some CommandOption.Score
        | "loadpoiandscore" -> Some CommandOption.LoadPoiAndScore
        | _ -> None
    let parseCommandByInt (value: int) : CommandOption option =
        if System.Enum.IsDefined(typeof<CommandOption>, value) then
            Some (enum<CommandOption> value)
        else
            None

[<Literal>]
let dbPath = "artemis.duckdb"

[<Literal>]
let sqlPath = "init.sql"

let loadPoi (conn: DuckDBConnection) =
    printfn "5) Run batch ETL loads"
    printfn "Before load:"
    getPoiList conn (Some 20) |> printfn "%A"
    insertBatchAndPoiList conn NewYork OverpassBatch.execute
    printfn "After load:"
    getPoiList conn (Some 20) |> printfn "%A"

let score () =
    printfn "6) Evaluate scores and persist to DB"
    // let allLocations = getAllLocations conn

[<EntryPoint>]
let main argv =
    match argv with
    | [||] -> 
        printfn "No command provided."
        printfn "Available commands: LoadPoi (0), Score (1), LoadPoiAndScore (2)"
        1
    | _ ->
        let command = 
            match ArgumentParser.parseCommand argv.[0] with
            | Some cmd -> Some cmd
            | None -> 
                match System.Int32.TryParse(argv.[0]) with
                | true, num -> ArgumentParser.parseCommandByInt num
                | _ -> None
        
        printfn "1) Init DB schema via .sql script"
        let sql = File.ReadAllText sqlPath
        let conn = new DuckDBConnection $"DataSource={dbPath}"
        conn.Open()
        let cmd = conn.CreateCommand()
        cmd.CommandText <- sql
        cmd.ExecuteNonQuery() |> ignore
        conn.Close()

        printfn "2) Load user criteria"
        let testRows = [|
            { Id = 1;  Lft = 1;  Rgt = 38; Operator = Some 0; CategoryId = None;    DistAmt = None }
            { Id = 2;  Lft = 2;  Rgt = 3;  Operator = None;   CategoryId = Some 9;  DistAmt = Some 0.1m }
            { Id = 3;  Lft = 4;  Rgt = 5;  Operator = None;   CategoryId = Some 7;  DistAmt = Some 0.2m }
            { Id = 4;  Lft = 6;  Rgt = 7;  Operator = None;   CategoryId = Some 6;  DistAmt = Some 0.5m }
            { Id = 5;  Lft = 8;  Rgt = 9;  Operator = None;   CategoryId = Some 1;  DistAmt = Some 20.0m }
            { Id = 6;  Lft = 10; Rgt = 15; Operator = Some 1; CategoryId = None;    DistAmt = None }
            { Id = 7;  Lft = 11; Rgt = 12; Operator = None;   CategoryId = Some 11; DistAmt = Some 5.0m }
            { Id = 8;  Lft = 13; Rgt = 14; Operator = None;   CategoryId = Some 12; DistAmt = Some 5.0m }
            { Id = 9;  Lft = 16; Rgt = 23; Operator = Some 1; CategoryId = None;    DistAmt = None }
            { Id = 10; Lft = 17; Rgt = 18; Operator = None;   CategoryId = Some 13; DistAmt = Some 1.0m }
            { Id = 11; Lft = 19; Rgt = 20; Operator = None;   CategoryId = Some 14; DistAmt = Some 1.0m }
            { Id = 12; Lft = 21; Rgt = 22; Operator = None;   CategoryId = Some 15; DistAmt = Some 1.0m }
            { Id = 13; Lft = 24; Rgt = 37; Operator = Some 1; CategoryId = None;    DistAmt = None }
            { Id = 14; Lft = 25; Rgt = 26; Operator = None;   CategoryId = Some 16; DistAmt = Some 0.5m }
            { Id = 15; Lft = 27; Rgt = 32; Operator = Some 0; CategoryId = None;    DistAmt = None }
            { Id = 16; Lft = 28; Rgt = 29; Operator = None;   CategoryId = Some 16; DistAmt = Some 5.0m }
            { Id = 17; Lft = 30; Rgt = 31; Operator = None;   CategoryId = Some 17; DistAmt = Some 1.0m }
            { Id = 18; Lft = 33; Rgt = 38; Operator = Some 0; CategoryId = None;    DistAmt = None }
            { Id = 19; Lft = 34; Rgt = 35; Operator = None;   CategoryId = Some 16; DistAmt = Some 10.0m }
            { Id = 20; Lft = 36; Rgt = 37; Operator = None;   CategoryId = Some 10; DistAmt = Some 0.5m }
        |]

        let tree = buildTree (Array.toList testRows)
        printTree "" tree

        printfn "3) Load user locations"
        let testData = [|
            { Id = None; Name = "Central Park"; Address = Some "Central Park, New York, NY"; Lat = None; Lon = None; Notes = Some "Large urban park"; PriceAmt = None; PriceCcy = None }
            { Id = None; Name = "Eiffel Tower"; Address = Some "Champ de Mars, Paris, France"; Lat = None; Lon = None; Notes = Some "Iconic landmark"; PriceAmt = Some 2650; PriceCcy = Some "EUR" }
            { Id = None; Name = "Sydney Opera House"; Address = Some "Bennelong Point, Sydney NSW, Australia"; Lat = None; Lon = None; Notes = None; PriceAmt = Some 4500; PriceCcy = Some "AUD" }
        |]
        insertLocations conn testData |> ignore

        printfn "3) Geocode any locations without lat/lon and persist to DB"
        geocodeAndUpdateLocations conn Geocoder.geocodeAsync |> ignore

        match command with
        | Some CommandOption.LoadPoi ->
            printfn "Loading POI..."
            loadPoi conn
            0
        | Some CommandOption.Score ->
            printfn "Running Score..."
            score ()
            0
        | Some CommandOption.LoadPoiAndScore ->
            printfn "Loading POI and Scoring..."
            loadPoi conn
            score ()
            0
        | _ ->
            printfn "Unknown command: %s" argv.[0]
            printfn "Available commands: LoadPoi (0), Score (1), LoadPoiAndScore (2)"
            1
