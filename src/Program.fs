open System.IO
open System.Reflection
open DuckDB.NET.Data
open Logger
open DomainTypes
open Data.Criterion
open Data.Locations
open DataFeeds

let version =
    Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        .InformationalVersion

[<Literal>]
let dbPath = "./data/artemis.duckdb"

let exec (conn: DuckDBConnection) (sql: string) =
    use cmd = conn.CreateCommand()
    cmd.CommandText <- sql
    cmd.ExecuteNonQuery() |> ignore

[<EntryPoint>]
let main argv =
    log "Running Artemis v%s" version
    use conn = new DuckDBConnection $"Data Source={dbPath}"
    conn.Open()

    log "Initializing database..."
    File.ReadAllText "init.sql" |> exec conn

    log "Beginning ELT run..."
    let res = ELT.Pipeline.run conn
    // match res with
    // | 0 ->
    //     log "Data load completed, ready for analysis."
    //     0
    // | _ ->
    //     log "Data load failed."
    //     1
    log "Upserting criteria tree..."
    let criteriaRows = [
        // Alice
        { Id = 1; TreeId = 1; Lft = 1;  Rgt = 6;  Operator = Some 0; CategoryId = None;    DistAmt = None }
        { Id = 2; TreeId = 1; Lft = 2;  Rgt = 3;  Operator = None;   CategoryId = Some 6;  DistAmt = Some 0.5 }
        { Id = 3; TreeId = 1; Lft = 4;  Rgt = 5;  Operator = None;   CategoryId = Some 7;  DistAmt = Some 0.25 }

        // Bob
        { Id = 1;  TreeId = 2; Lft = 1;  Rgt = 40; Operator = Some 0; CategoryId = None;    DistAmt = None }
        { Id = 2;  TreeId = 2; Lft = 2;  Rgt = 3;  Operator = None;   CategoryId = Some 9;  DistAmt = Some 1.0 }
        { Id = 3;  TreeId = 2; Lft = 4;  Rgt = 5;  Operator = None;   CategoryId = Some 7;  DistAmt = Some 0.2 }
        { Id = 4;  TreeId = 2; Lft = 6;  Rgt = 7;  Operator = None;   CategoryId = Some 6;  DistAmt = Some 2.5 }
        { Id = 5;  TreeId = 2; Lft = 8;  Rgt = 9;  Operator = None;   CategoryId = Some 1;  DistAmt = Some 20.0 }
        // Groceries
        { Id = 6;  TreeId = 2; Lft = 10; Rgt = 15; Operator = Some 1; CategoryId = None;    DistAmt = None }
        { Id = 7;  TreeId = 2; Lft = 11; Rgt = 12; Operator = None;   CategoryId = Some 11; DistAmt = Some 5.0 }
        { Id = 8;  TreeId = 2; Lft = 13; Rgt = 14; Operator = None;   CategoryId = Some 12; DistAmt = Some 5.0 }
        { Id = 9;  TreeId = 2; Lft = 16; Rgt = 23; Operator = Some 1; CategoryId = None;    DistAmt = None }
        { Id = 10; TreeId = 2; Lft = 17; Rgt = 18; Operator = None;   CategoryId = Some 13; DistAmt = Some 3.0 }
        { Id = 11; TreeId = 2; Lft = 19; Rgt = 20; Operator = None;   CategoryId = Some 14; DistAmt = Some 3.0 }
        { Id = 12; TreeId = 2; Lft = 21; Rgt = 22; Operator = None;   CategoryId = Some 15; DistAmt = Some 3.0 }
        // Job/commute
        { Id = 13; TreeId = 2; Lft = 24; Rgt = 39; Operator = Some 1; CategoryId = None;    DistAmt = None }
        { Id = 14; TreeId = 2; Lft = 25; Rgt = 26; Operator = None;   CategoryId = Some 10; DistAmt = Some 0.5 }
        { Id = 15; TreeId = 2; Lft = 27; Rgt = 32; Operator = Some 0; CategoryId = None;    DistAmt = None }
        { Id = 16; TreeId = 2; Lft = 28; Rgt = 29; Operator = None;   CategoryId = Some 0;  DistAmt = Some 5.0 }
        { Id = 17; TreeId = 2; Lft = 30; Rgt = 31; Operator = None;   CategoryId = Some 16; DistAmt = Some 1.0 }
        { Id = 18; TreeId = 2; Lft = 33; Rgt = 38; Operator = Some 0; CategoryId = None;    DistAmt = None }
        { Id = 19; TreeId = 2; Lft = 34; Rgt = 35; Operator = None;   CategoryId = Some 0;  DistAmt = Some 10.0 }
        { Id = 20; TreeId = 2; Lft = 36; Rgt = 37; Operator = None;   CategoryId = Some 10; DistAmt = Some 0.5 }

        // Charlie
        { Id = 21;  TreeId = 3; Lft = 1;  Rgt = 40; Operator = Some 0; CategoryId = None;    DistAmt = None }
        { Id = 22;  TreeId = 3; Lft = 2;  Rgt = 3;  Operator = None;   CategoryId = Some 9;  DistAmt = Some 1.0 }
        { Id = 23;  TreeId = 3; Lft = 4;  Rgt = 5;  Operator = None;   CategoryId = Some 7;  DistAmt = Some 0.2 }
        { Id = 24;  TreeId = 3; Lft = 6;  Rgt = 7;  Operator = None;   CategoryId = Some 6;  DistAmt = Some 2.5 }
        { Id = 25;  TreeId = 3; Lft = 8;  Rgt = 9;  Operator = None;   CategoryId = Some 1;  DistAmt = Some 20.0 }
        // Groceries
        { Id = 26;  TreeId = 3; Lft = 10; Rgt = 15; Operator = Some 1; CategoryId = None;    DistAmt = None }
        { Id = 27;  TreeId = 3; Lft = 11; Rgt = 12; Operator = None;   CategoryId = Some 11; DistAmt = Some 2.5 }
        { Id = 28;  TreeId = 3; Lft = 13; Rgt = 14; Operator = None;   CategoryId = Some 12; DistAmt = Some 2.5 }
        { Id = 29;  TreeId = 3; Lft = 16; Rgt = 23; Operator = Some 1; CategoryId = None;    DistAmt = None }
        { Id = 30; TreeId = 3; Lft = 17; Rgt = 18; Operator = None;   CategoryId = Some 13; DistAmt = Some 1.0 }
        { Id = 31; TreeId = 3; Lft = 19; Rgt = 20; Operator = None;   CategoryId = Some 14; DistAmt = Some 1.0 }
        { Id = 32; TreeId = 3; Lft = 21; Rgt = 22; Operator = None;   CategoryId = Some 15; DistAmt = Some 1.0 }
        // Job/commute
        { Id = 33; TreeId = 3; Lft = 24; Rgt = 39; Operator = Some 1; CategoryId = None;    DistAmt = None }
        { Id = 34; TreeId = 3; Lft = 25; Rgt = 26; Operator = None;   CategoryId = Some 10; DistAmt = Some 0.5 }
        // { Id = 35; TreeId = 3; Lft = 27; Rgt = 32; Operator = Some 0; CategoryId = None;    DistAmt = None }
        // { Id = 36; TreeId = 3; Lft = 28; Rgt = 29; Operator = None;   CategoryId = Some 0;  DistAmt = Some 5.0 }
        // { Id = 37; TreeId = 3; Lft = 30; Rgt = 31; Operator = None;   CategoryId = Some 16; DistAmt = Some 1.0 }
        { Id = 38; TreeId = 3; Lft = 33; Rgt = 38; Operator = Some 0; CategoryId = None;    DistAmt = None }
        { Id = 39; TreeId = 3; Lft = 34; Rgt = 35; Operator = None;   CategoryId = Some 0;  DistAmt = Some 5.0 }
        { Id = 40; TreeId = 3; Lft = 36; Rgt = 37; Operator = None;   CategoryId = Some 10; DistAmt = Some 0.25 }
    ]
    replaceCriterionList conn criteriaRows |> ignore
    let trees = getTreeList conn

    log "Load user locations..."
    insertLocations conn |> ignore

    log "Geocode any locations without lat/lon and persist to DB..."
    geocodeAndUpdateLocations conn Geocoder.geocodeAsync |> ignore

    log "TODO Scoring %d trees..." trees.Length
    // trees
    // |> List.map (fun t ->
    //     log "Evaluating tree %i..." t.Id
    //     getCriterionList conn t.Id (Some 1_000)
    //     |> buildTree
    //     |> scoreAndSave conn options.Eval t.Name
    // )
    // |> ignore
    0
