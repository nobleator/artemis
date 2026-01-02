#r "nuget: DuckDB.NET.Data.Full"
#load "DomainTypes.fs"
#load "osm.fsx"
open System.IO
open DuckDB.NET.Data
open System
open DomainTypes

printfn "1) Init DB schema via .sql script"
[<Literal>]
let dbPath = "artemis.duckdb"
[<Literal>]
let sqlPath = "init.sql"
let sql = File.ReadAllText sqlPath

let conn = new DuckDBConnection $"DataSource={dbPath}"
conn.Open()
let cmd = conn.CreateCommand()
cmd.CommandText <- sql
cmd.ExecuteNonQuery() |> ignore
conn.Close()

printfn "2) Load user data (criteria & locations)"
printfn "3) Geocode any locations without lat/lon and persist to DB"
printfn "4) Run batch ETL loads"

let addParam (cmd:DuckDBCommand) name value =
    let p = cmd.CreateParameter()
    p.ParameterName <- name
    p.Value <- value
    cmd.Parameters.Add(p) |> ignore

let insertBatchAndPois (region:Region) (conn:DuckDBConnection) =
    conn.Open()
    use tran = conn.BeginTransaction()
    let batchId =
        let cmd = conn.CreateCommand()
        cmd.Transaction <- tran
        cmd.CommandText <- "INSERT INTO batch (source, status, start_utc) VALUES (@source, @status, @start) RETURNING id"
        addParam cmd "@source" region
        addParam cmd "@status" "running"
        addParam cmd "@start" DateTime.UtcNow
        cmd.ExecuteScalar() :?> int
    printfn "Batch ID: %A" batchId
    OverpassBatch.execute region
    |> List.iter (fun (_, cat, lat, lon, sourceXref) ->
        let cmd = conn.CreateCommand()
        cmd.Transaction <- tran
        cmd.CommandText <- """
            INSERT INTO poi (batch_id, source_xref, category_id, lat, lon)
            VALUES (@batch, @xref, @cat, @lat, @lon)
        """
        addParam cmd "@batch" batchId
        addParam cmd "@xref" sourceXref
        addParam cmd "@cat" cat
        addParam cmd "@lat" lat
        addParam cmd "@lon" lon
        cmd.ExecuteNonQuery() |> ignore
    )
    tran.Commit()
    conn.Close()

insertBatchAndPois NewYork
// OverpassBatch.execute NewYork |> printfn "%A"
printfn "5) Evaluate scores and persist to DB"
