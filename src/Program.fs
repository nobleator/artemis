open System
open System.IO
open DuckDB.NET.Data
open DomainTypes
open DataFeeds

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

let buildTree (rows: CriterionRow list) : CriteriaNode =
    let rec buildFrom rows parentRgt =
        match rows with
        | [] -> ([], [])
        | row :: rest when row.Lft >= parentRgt -> ([], rows)
        | row :: rest ->
            match row.Operator with
            | None ->
                let node = TermNode(row.Id, enum<Category>(row.CategoryId.Value), row.DistAmt.Value)
                let (siblings, remaining) = buildFrom rest parentRgt
                (node :: siblings, remaining)
            | Some op ->
                let (children, afterChildren) = buildFrom rest row.Rgt
                let node = GroupNode(row.Id, enum<OperatorType>(op), children)
                let (siblings, remaining) = buildFrom afterChildren parentRgt
                (node :: siblings, remaining)
    match rows with
    | [] -> failwith "No rows"
    | root :: rest ->
        match root.Operator with
        | None -> failwith "Root must be a group node"
        | Some op ->
            let (children, _) = buildFrom rest root.Rgt
            GroupNode(root.Id, enum<OperatorType>(op), children)

let rec printTree indent node =
    match node with
    | GroupNode(id, op, children) ->
        printfn "%s%s (id: %d)" indent (if op = OperatorType.And then "AND" else "OR") id
        children |> List.iter (printTree (indent + "  "))
    | TermNode(id, cat, dist) ->
        printfn "%s%A < %.3f (id: %d)" indent cat dist id

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

printfn "3) Geocode any locations without lat/lon and persist to DB"
printfn "4) Run batch ETL loads"

let addParam (cmd:DuckDBCommand) value =
    let p = cmd.CreateParameter()
    p.Value <- value
    cmd.Parameters.Add(p) |> ignore

let insertBatchAndPoiList (conn:DuckDBConnection) (region:Region) =
    conn.Open()
    use tran = conn.BeginTransaction()
    let batchId =
        let cmd = conn.CreateCommand()
        cmd.Transaction <- tran
        cmd.CommandText <- "INSERT INTO batch (source, status, start_utc) VALUES (?, ?, ?) RETURNING id"
        addParam cmd "Overpass"
        addParam cmd "Pending"
        addParam cmd DateTime.UtcNow
        cmd.ExecuteScalar() :?> int
    printfn "Batch ID: %A" batchId
    let PoiList = OverpassBatch.execute region
    let insertCount = 
        PoiList
        |> List.sumBy (fun (_, cat, lat, lon, sourceXref) ->
            let cmd = conn.CreateCommand()
            cmd.Transaction <- tran
            cmd.CommandText <- """
                INSERT INTO poi (batch_id, source, source_xref, category_id, lat, lon)
                VALUES (?, ?, ?, ?, ?, ?)
                ON CONFLICT (source, source_xref) DO NOTHING
            """
            addParam cmd batchId
            addParam cmd "Overpass"
            addParam cmd sourceXref
            addParam cmd (Category.toId cat)
            addParam cmd lat
            addParam cmd lon
            cmd.ExecuteNonQuery()
        )
    printfn "Inserted %d new PoiList (skipped %d duplicates)" insertCount (PoiList.Length - insertCount)
    let updateCmd = conn.CreateCommand()
    updateCmd.Transaction <- tran
    updateCmd.CommandText <- "UPDATE batch SET status = ?, end_utc = ? WHERE id = ?"
    addParam updateCmd "Success"
    addParam updateCmd DateTime.UtcNow
    addParam updateCmd batchId
    updateCmd.ExecuteNonQuery() |> ignore
    tran.Commit()
    conn.Close()

let readPoi (reader: DuckDBDataReader) =
    {
        Id = reader.GetInt32(0)
        BatchId = reader.GetInt32(1)
        Source = reader.GetString(2)
        SourceXref = if reader.IsDBNull(3) then None else Some(reader.GetString(3))
        CategoryId = if reader.IsDBNull(4) then None else Some(reader.GetInt32(4))
        Lat = if reader.IsDBNull(5) then None else Some(reader.GetDecimal(5))
        Lon = if reader.IsDBNull(6) then None else Some(reader.GetDecimal(6))
    }

let getPoiList (conn: DuckDBConnection) (limit0: int option) =
    let limit = defaultArg limit0 20
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "SELECT id, batch_id, source, source_xref, category_id, lat, lon FROM poi LIMIT ?"
    addParam cmd limit
    use reader = cmd.ExecuteReader()
    let results = [ while reader.Read() do readPoi reader ]
    conn.Close()
    results

printfn "Before load:"
getPoiList conn (Some 20) |> printfn "%A"
insertBatchAndPoiList conn NewYork
printfn "After load:"
getPoiList conn (Some 20) |> printfn "%A"

printfn "5) Evaluate scores and persist to DB"
