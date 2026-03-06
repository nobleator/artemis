open System
open System.IO
open DuckDB.NET.Data
open DomainTypes
open DataFeeds
open Tree.Criteria
open Data.Locations
open Data.Poi
open Data.Score
open Evaluation
open System.Text.Json

// TODO: additional options for sensitivity analysis or log verbosity
// TODO: location/region characteristics
// TODO: personas?
type CommandOption =
    | LoadPoi
    | Score
    | LoadPoiAndScore
    static member TryParse = function
        | "load-poi" -> Ok LoadPoi
        | "score" -> Ok Score
        | "load-poi-and-score" -> Ok LoadPoiAndScore
        | x -> Error $"Invalid command: {x}"

type InitOption =
    | Resume
    | Purge
    static member TryParse = function
        | "resume" -> Ok Resume
        | "purge" -> Ok Purge
        | x -> Error $"Invalid init option: {x}"



type LogOption =
    | Verbose
    | Concise
    static member TryParse = function
        | "verbose" -> Ok Verbose
        | "concise" -> Ok Concise
        | x -> Error $"Invalid log option: {x}"

type Options = {
    Command: CommandOption
    Init: InitOption
    Log: LogOption
    Eval: EvalOption
}

let defaultOptions = {
    Command = LoadPoiAndScore
    Init = Resume
    Log = Concise
    Eval = LinearBoundedDistance
}

module ArgumentParser =
    let rec parseArgs (options: Options) (args: string list) =
        match args with
        | "--command" :: value :: rest ->
            match CommandOption.TryParse value with
            | Ok v -> parseArgs { options with Command = v } rest
            | Error e -> failwith e
        | "--init" :: value :: rest ->
            match InitOption.TryParse value with
            | Ok v -> parseArgs { options with Init = v } rest
            | Error e -> failwith e
        | "--log" :: value :: rest ->
            match LogOption.TryParse value with
            | Ok v -> parseArgs { options with Log = v } rest
            | Error e -> failwith e
        | "--eval" :: value :: rest ->
            match EvalOption.TryParse value with
            | Ok v -> parseArgs { options with Eval = v } rest
            | Error e -> failwith e
        | [] -> options
        | arg :: _ -> failwith $"Unknown argument: {arg}"

[<Literal>]
let dbPath = "./data/artemis.duckdb"

[<Literal>]
let purgeSqlPath = "purge.sql"

[<Literal>]
let initSqlPath = "init.sql"

let sw = Diagnostics.Stopwatch.StartNew()
let printTimed fmt =
    Printf.kprintf (fun msg -> printf "[%s] %s" (sw.Elapsed.ToString("mm\:ss\.ff")) msg) fmt

let loadPoi (conn: DuckDBConnection) =
    insertBatchAndPoiList conn WashingtonDC OverpassBatch.execute

let simpleNorm (distance: double) (maxDistance: double) : double =
    let maxDist = double maxDistance
    if distance >= maxDist then 0.0
    else 1.0 - distance / maxDist

let exponentialDecay (distance: double) (noop: double) =
    // TODO max distance isn't required, but keeping a second `double` param so that the signature will match
    // This beta value can be tweaked
    Math.Exp(-0.4 * distance)

let scoreAndSave (conn: DuckDBConnection) (evalMode: EvalOption) (node: CriteriaNode) =
    let getPoiFunc category location = 
        getClosestPoiByCategory conn category location
    getAllLocations conn
    |> List.choose (fun loc ->
        match loc.Lat, loc.Lon with
        | Some lat, Some lon -> Some { loc with Lat = Some lat; Lon = Some lon }
        | _ -> None)
    // |> List.take 10
    |> List.map (fun x ->
        let s =
            match evalMode with
            | LinearBoundedDistance -> Scoring.scoreTree x getPoiFunc simpleNorm node
            | ExponentialDecayDistance -> Scoring.scoreTree x getPoiFunc exponentialDecay node
        x, s)
    |> List.sortByDescending (fun (l, s) -> s.Normalized)
    |> List.map (fun (l, s) ->
        printTimed "Saving %A..." l.Name
        match l.Id with
        | Some id -> Tree.Score.toRows evalMode id s |> insertScoreList conn
        | _ -> failwith "Location has no ID!"
        |> ignore
        l, s)
    |> List.map (fun (l, s) ->
        printTimed "Location %A:" l.Name
        Scoring.printScores "  " s |> ignore)

type ZillowSearch = {
    North: float
    South: float
    East: float
    West: float
    MinBeds: int
    PriceMin: int
    PriceMax: int
    SqFtMin: int
    SqFtMax: int
    SortBy: string
}

let defaultSearch = {
    North = 39.16016996910429
    South = 38.50225811136643
    East = -76.67019020497641
    West = -77.65895973622641
    MinBeds = 4
    PriceMin = 500000
    PriceMax = 1000000
    SqFtMin = 2500
    SqFtMax = 5000
    SortBy = "lot"
}

let buildZillowUrl (s: ZillowSearch) =
    let queryObj = 
        {| 
            isMapVisible = true
            mapBounds = {| north = s.North; south = s.South; east = s.East; west = s.West |}
            filterState = 
                {| 
                    sort = {| value = s.SortBy |}
                    beds = {| min = s.MinBeds; max = null |}
                    price = {| min = s.PriceMin; max = s.PriceMax |}
                    mp = {| min = s.SqFtMin; max = s.SqFtMax |}
                    // land = {| value = false |}
                    apa = {| value = false |}
                    manu = {| value = false |}
                    con = {| value = false |}
                    apco = {| value = false |}
                    mf = {| value = false |}
                |}
            isListVisible = true
            category = "cat1"
            pagination = {| |}
            usersSearchTerm = ""
        |}
    let json = JsonSerializer.Serialize queryObj
    let encoded = Uri.EscapeDataString json
    $"https://www.zillow.com/homes/for_sale/?searchQueryState={encoded}"

[<EntryPoint>]
let main argv =
    printTimed "Begin execution..."
    let url = buildZillowUrl defaultSearch
    printTimed "See README for instructions on prepping locations."
    printTimed "Navigate here to begin: %A" url
    match argv with
    | [||] -> 
        printTimed "No command provided."
        1
    | _ ->
        let options = ArgumentParser.parseArgs defaultOptions (argv |> Array.toList)
        printTimed "Running with options: %A" options
        match options.Init with
        | Purge ->
            printTimed "0) Purging existing database objects..."
            let purgeSql = File.ReadAllText purgeSqlPath
            let conn = new DuckDBConnection $"DataSource={dbPath}"
            conn.Open()
            let cmd = conn.CreateCommand()
            cmd.CommandText <- purgeSql
            cmd.ExecuteNonQuery() |> ignore
            conn.Close()
        | Resume -> printTimed "No purge required, continuing."
        printTimed "1) Init DB schema via .sql script..."
        let sql = File.ReadAllText initSqlPath
        let conn = new DuckDBConnection $"DataSource={dbPath}"
        conn.Open()
        let cmd = conn.CreateCommand()
        cmd.CommandText <- sql
        cmd.ExecuteNonQuery() |> ignore
        conn.Close()

        printTimed "2) Load user criteria..."
        let testRows = [|
            { Id = 1;  Lft = 1;  Rgt = 38; Operator = Some 0; CategoryId = None;    DistAmt = None }
            { Id = 2;  Lft = 2;  Rgt = 3;  Operator = None;   CategoryId = Some 9;  DistAmt = Some 1.0 }
            { Id = 3;  Lft = 4;  Rgt = 5;  Operator = None;   CategoryId = Some 7;  DistAmt = Some 0.2 }
            { Id = 4;  Lft = 6;  Rgt = 7;  Operator = None;   CategoryId = Some 6;  DistAmt = Some 2.5 }
            { Id = 5;  Lft = 8;  Rgt = 9;  Operator = None;   CategoryId = Some 1;  DistAmt = Some 20.0 }
            // Groceries
            { Id = 6;  Lft = 10; Rgt = 15; Operator = Some 1; CategoryId = None;    DistAmt = None }
            { Id = 7;  Lft = 11; Rgt = 12; Operator = None;   CategoryId = Some 11; DistAmt = Some 5.0 }
            { Id = 8;  Lft = 13; Rgt = 14; Operator = None;   CategoryId = Some 12; DistAmt = Some 5.0 }
            { Id = 9;  Lft = 16; Rgt = 23; Operator = Some 1; CategoryId = None;    DistAmt = None }
            { Id = 10; Lft = 17; Rgt = 18; Operator = None;   CategoryId = Some 13; DistAmt = Some 3.0 }
            { Id = 11; Lft = 19; Rgt = 20; Operator = None;   CategoryId = Some 14; DistAmt = Some 3.0 }
            { Id = 12; Lft = 21; Rgt = 22; Operator = None;   CategoryId = Some 15; DistAmt = Some 3.0 }
            // Job/commute
            { Id = 13; Lft = 24; Rgt = 37; Operator = Some 1; CategoryId = None;    DistAmt = None }
            { Id = 14; Lft = 25; Rgt = 26; Operator = None;   CategoryId = Some 10; DistAmt = Some 0.5 }
            { Id = 15; Lft = 27; Rgt = 32; Operator = Some 0; CategoryId = None;    DistAmt = None }
            { Id = 16; Lft = 28; Rgt = 29; Operator = None;   CategoryId = Some 0;  DistAmt = Some 5.0 }
            { Id = 17; Lft = 30; Rgt = 31; Operator = None;   CategoryId = Some 16; DistAmt = Some 1.0 }
            { Id = 18; Lft = 33; Rgt = 38; Operator = Some 0; CategoryId = None;    DistAmt = None }
            { Id = 19; Lft = 34; Rgt = 35; Operator = None;   CategoryId = Some 0;  DistAmt = Some 10.0 }
            { Id = 20; Lft = 36; Rgt = 37; Operator = None;   CategoryId = Some 10; DistAmt = Some 0.5 }
        |]

        let tree = buildTree (Array.toList testRows)
        printTree "" tree

        printTimed "3) Load user locations..."
        insertLocations conn |> ignore

        printTimed "4) Geocode any locations without lat/lon and persist to DB..."
        geocodeAndUpdateLocations conn Geocoder.geocodeAsync |> ignore

        match options.Command with
        | LoadPoi ->
            printTimed "5) Loading POI..."
            loadPoi conn
            printTimed "Skipping step 6)"
        | Score ->
            printTimed "Skipping step 5)"
            printTimed "6) Evaluating scores..."
            scoreAndSave conn options.Eval tree |> ignore
        | LoadPoiAndScore ->
            printTimed "5) Loading POI"
            loadPoi conn
            printTimed "6) Evaluating scores ..."
            scoreAndSave conn options.Eval tree |> ignore
        printTimed "Done."
        0
