namespace Data

open System
open DuckDB.NET.Data
open DomainTypes

module Sql =
    let addParam (cmd: DuckDBCommand) value =
        let p = cmd.CreateParameter()
        p.Value <- value
        cmd.Parameters.Add(p) |> ignore

    let optionToObj = function
        | Some v -> box v
        | None -> box DBNull.Value

module Locations =
    open Sql
    
    let readLocation (reader: DuckDBDataReader) =
        {
            Id = Some(reader.GetInt32(0))
            Name = reader.GetString(1)
            Address = if reader.IsDBNull(2) then None else Some(reader.GetString(2))
            Lat = if reader.IsDBNull(3) then None else Some(reader.GetDouble(3))
            Lon = if reader.IsDBNull(4) then None else Some(reader.GetDouble(4))
            Notes = if reader.IsDBNull(5) then None else Some(reader.GetString(5))
            PriceAmt = if reader.IsDBNull(6) then None else Some(reader.GetInt32(6))
            PriceCcy = if reader.IsDBNull(7) then None else Some(reader.GetString(7))
        }

    let getAllLocations (conn: DuckDBConnection) =
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            SELECT id, name, address, lat, lon, notes, price_amt, price_ccy 
            FROM main.location 
            ORDER BY id
        """
        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readLocation reader ]
        conn.Close()
        results

    let insertLocations (conn: DuckDBConnection) (locations: Location array) =
        conn.Open()
        use tran = conn.BeginTransaction()
        
        let insertCount =
            locations
            |> Array.sumBy (fun loc ->
                let cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- """
                    INSERT INTO main.location (name, address, lat, lon, notes, price_amt, price_ccy)
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                    ON CONFLICT (address) DO NOTHING
                """
                addParam cmd loc.Name
                addParam cmd (optionToObj loc.Address)
                addParam cmd (optionToObj loc.Lat)
                addParam cmd (optionToObj loc.Lon)
                addParam cmd (optionToObj loc.Notes)
                addParam cmd (optionToObj loc.PriceAmt)
                addParam cmd (optionToObj loc.PriceCcy)
                cmd.ExecuteNonQuery()
            )
        
        printfn "Inserted %d locations" insertCount
        tran.Commit()
        conn.Close()
        insertCount

    let geocodeAndUpdateLocations (conn: DuckDBConnection) (geocodeFunc: Location -> Location) =
        conn.Open()
        use selectCmd = conn.CreateCommand()
        selectCmd.CommandText <- """
            SELECT id, name, address, lat, lon, notes, price_amt, price_ccy
            FROM main.location 
            WHERE (lat IS NULL OR lon IS NULL) AND address IS NOT NULL
        """
        let locationsToGeocode =
            use reader = selectCmd.ExecuteReader()
            [ while reader.Read() do readLocation reader ]
        printfn "Found %d locations to geocode" locationsToGeocode.Length
        use tran = conn.BeginTransaction()
        let updateCount =
            locationsToGeocode
            |> List.sumBy (fun location ->
                let newLoc = geocodeFunc location
                match newLoc.Id, newLoc.Lat, newLoc.Lon with
                | Some id, Some lat, Some lon ->
                    let cmd = conn.CreateCommand()
                    cmd.Transaction <- tran
                    cmd.CommandText <- "UPDATE main.location SET lat = ?, lon = ? WHERE id = ?"
                    addParam cmd lat
                    addParam cmd lon
                    addParam cmd id
                    cmd.ExecuteNonQuery()
                | _ -> 0
            )
        printfn "Updated %d locations with coordinates" updateCount
        tran.Commit()
        conn.Close()
        updateCount

module Poi =
    open Sql

    let readPoi (reader: DuckDBDataReader) =
        {
            Id = reader.GetInt32(0)
            BatchId = reader.GetInt32(1)
            Source = reader.GetString(2)
            SourceXref = if reader.IsDBNull(3) then None else Some(reader.GetString(3))
            CategoryId = if reader.IsDBNull(4) then None else Some(reader.GetInt32(4))
            Lat = if reader.IsDBNull(5) then None else Some(reader.GetDouble(5))
            Lon = if reader.IsDBNull(6) then None else Some(reader.GetDouble(6))
        }

    let getPoiList (conn: DuckDBConnection) (limit0: int option) =
        let limit = defaultArg limit0 20
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- "SELECT id, batch_id, source, source_xref, category_id, lat, lon FROM main.poi LIMIT ?"
        addParam cmd limit
        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readPoi reader ]
        conn.Close()
        results
    
    let getPoiListByCategoryAndBoundingBox (conn: DuckDBConnection) (cat: Category) (bbox: BoundingBox) =
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            SELECT id, batch_id, source, source_xref, category_id, lat, lon
            FROM main.poi
            WHERE category_id = ?
            AND lat BETWEEN ? AND ?
            AND lon BETWEEN ? AND ?
        """
        addParam cmd cat
        addParam cmd bbox.MinLat
        addParam cmd bbox.MaxLat
        addParam cmd bbox.MinLon
        addParam cmd bbox.MaxLon
        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readPoi reader ]
        conn.Close()
        results
    
    let insertBatchAndPoiList (conn: DuckDBConnection) (region: Region) (batchFunc: Region -> List<Region * Category * float * float * int64>) =
        conn.Open()
        use tran = conn.BeginTransaction()
        let batchId =
            let cmd = conn.CreateCommand()
            cmd.Transaction <- tran
            cmd.CommandText <- "INSERT INTO main.batch (source, status, start_utc) VALUES (?, ?, ?) RETURNING id"
            addParam cmd "Overpass"
            addParam cmd "Pending"
            addParam cmd DateTime.UtcNow
            cmd.ExecuteScalar() :?> int
        printfn "Batch ID: %A" batchId
        let poiList = batchFunc region
        printfn "Summary by category:"
        poiList
        |> List.groupBy (fun (_, category, _, _, _) -> category)
        |> List.map (fun (category, items) -> category, List.length items)
        |> List.iter (fun (category, count) -> printfn "%A: %d" category count)
        let insertCount = 
            poiList
            |> List.sumBy (fun (_, cat, lat, lon, sourceXref) ->
                let cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                // Note that this can overlap with more specific categories, e.g. Harris Teeter is also a Grocery
                cmd.CommandText <- """
                    INSERT INTO main.poi (batch_id, source, source_xref, category_id, lat, lon)
                    VALUES (?, ?, ?, ?, ?, ?)
                    ON CONFLICT (source, source_xref, category_id) DO NOTHING
                """
                addParam cmd batchId
                addParam cmd "Overpass"
                addParam cmd sourceXref
                addParam cmd (int cat)
                addParam cmd lat
                addParam cmd lon
                cmd.ExecuteNonQuery()
            )
        printfn "Inserted %d new PoiList (skipped %d duplicates)" insertCount (poiList.Length - insertCount)
        let updateCmd = conn.CreateCommand()
        updateCmd.Transaction <- tran
        updateCmd.CommandText <- "UPDATE main.batch SET status = ?, end_utc = ? WHERE id = ?"
        addParam updateCmd "Success"
        addParam updateCmd DateTime.UtcNow
        addParam updateCmd batchId
        updateCmd.ExecuteNonQuery() |> ignore
        tran.Commit()
        conn.Close()
