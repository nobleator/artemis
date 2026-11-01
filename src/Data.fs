namespace Data

open System
open FSharp.Data
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

    let insertLocations (conn: DuckDBConnection) =
        conn.Open()
        use tran = conn.BeginTransaction()
        let cmd = conn.CreateCommand()
        cmd.Transaction <- tran
        cmd.CommandText <- """
            INSERT INTO main.location (name, address, lat, lon, notes, price_amt, price_ccy)
            SELECT
                address AS name,
                address,
                NULL AS lat,
                NULL AS lon,
                NULL AS notes,
                CASE
                    WHEN p = '--' OR p IS NULL THEN NULL
                    WHEN p LIKE '%k' THEN
                        CAST(CAST(REPLACE(REPLACE(p, ',', ''), 'k', '') AS DOUBLE) * 1000 AS INTEGER)
                    WHEN p LIKE '%m' THEN
                        CAST(CAST(REPLACE(REPLACE(p, ',', ''), 'm', '') AS DOUBLE) * 1000000 AS INTEGER)
                    ELSE
                        CAST(REPLACE(p, ',', '') AS INTEGER)
                END AS price_amt,
                'USD' AS price_ccy
            FROM (
                SELECT
                    address,
                    url,
                    lower(
                        trim(
                            REPLACE(REPLACE(price, 'Est.', ''), '$', '')
                        )
                    ) AS p
                FROM read_csv(
                    './data/listings.csv',
                    delim=',',
                    header=false,
                    columns={
                        'address': 'VARCHAR',
                        'url': 'VARCHAR',
                        'price': 'VARCHAR'
                    }
                )
            )
            ON CONFLICT(address) DO NOTHING;
        """
        let insertCount = cmd.ExecuteNonQuery()
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

    let getClosestPoiByCategory (conn: DuckDBConnection) (cat: Category) (loc: Location) =
        match loc.Lat, loc.Lon with
        | Some lat, Some lon ->
            conn.Open()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                SELECT id, batch_id, source, source_xref, category_id, lat, lon
                FROM main.poi
                WHERE category_id = ?
                ORDER BY abs(lat - ?) + abs(lon - ?)
                LIMIT 1
            """
            addParam cmd cat
            addParam cmd lat
            addParam cmd lon
            use reader = cmd.ExecuteReader()
            let result =
                match reader.Read() with
                | true -> Some (readPoi reader)
                | false -> None
            conn.Close()
            result
        | _ -> failwith "Location must have latitude and longitude"
    
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

module Region =
    let readRegion (reader: DuckDBDataReader) =
        {
            Id = reader.GetInt32 0
            Name = reader.GetString 1
            BBox = {
                MinLat = reader.GetDouble 2
                MinLon = reader.GetDouble 3
                MaxLat = reader.GetDouble 4
                MaxLon = reader.GetDouble 5
            }
        }

    let getRegionList (conn: DuckDBConnection) =
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- "SELECT id, name, min_lat, min_lon, max_lat, max_lon FROM main.region"
        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readRegion reader ]
        conn.Close()
        results

    let getRegionsWithoutCharacteristicsList (conn: DuckDBConnection) =
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
        SELECT r.id, r."name", r.min_lat, r.min_lon, r.max_lat, r.max_lon 
        FROM main.region r
        LEFT JOIN main.rc_open_meteo m ON m.region_id = r.id
        WHERE m.id IS NULL;
        """
        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readRegion reader ]
        conn.Close()
        results

module RegionalCharacteristics =
    open System.IO
    open System.Text
    open ExcelDataReader
    open Sql
    open Region

    Encoding.RegisterProvider CodePagesEncodingProvider.Instance

    let [<Literal>] ResolutionFolder = __SOURCE_DIRECTORY__

    type Pollen = CsvProvider<"../data/cdc_pollen_bgxp-nxmn_sample.csv", ResolutionFolder=ResolutionFolder>
    type County  = CsvProvider<"../data/2026_Gaz_counties_national_sample.txt", Separators="|", ResolutionFolder=ResolutionFolder>

    // Source CSV downloaded from: https://data.cdc.gov/National-Center-for-Environmental-Health/Weekly-Grouped-Pollen-Concentrations-Trees-and-Wee/bgxp-nxmn/about_data
    let [<Literal>] latestPollenDataFile = "/../data/cdc_pollen_bgxp-nxmn_20260927.csv"

    // Source TXT downloaded from: https://www.census.gov/geographies/reference-files/time-series/geo/gazetteer-files.html
    let [<Literal>] latestCountyDataFile = "/../data/2026_Gaz_counties_national.txt"

    // Source XLSB downloaded from: https://www.huduser.gov/portal/datasets/ahar/2025-ahar-part-1-pit-estimates-of-homelessness-in-the-us.html
    let [<Literal>] latestHomelessDataFile = "/../data/2007-2025-PIT-Counts-by-CoC.xlsb"

    let readRegionalClimateStatistics (reader: DuckDBDataReader) =
        {
            RegionId = reader.GetInt32 0
            RegionName = reader.GetString 1
            AllConditionsMetPct = reader.GetDouble 2
            AboveTempMinPct = reader.GetDouble 3
            BelowTempMaxPct = reader.GetDouble 4
            WithinTempRangePct = reader.GetDouble 5
            AboveHumMinPct = reader.GetDouble 6
            BelowHumMaxPct = reader.GetDouble 7
            WithinHumRangePct = reader.GetDouble 8
        }

    let getRegionalClimateStatistics (conn: DuckDBConnection) (treeId: int) =
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            SELECT
                r.id AS region_id,
                r."name" AS region_name,
                ROUND(SUM(CASE WHEN rom.temperature_2m_min > c.temp_min
                        AND rom.temperature_2m_max < c.temp_max
                        AND rom.dew_point_2m_min > c.humidity_min
                        AND rom.dew_point_2m_max < c.humidity_max
                    THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS all_conditions_met_pct,
                ROUND(SUM(CASE WHEN rom.temperature_2m_min > c.temp_min THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS above_temp_min_pct,
                ROUND(SUM(CASE WHEN rom.temperature_2m_max < c.temp_max THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS below_temp_max_pct,
                ROUND(SUM(CASE WHEN rom.temperature_2m_min > c.temp_min
                        AND rom.temperature_2m_max < c.temp_max
                    THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS within_temp_rng_pct,
                ROUND(SUM(CASE WHEN rom.dew_point_2m_min > c.humidity_min THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS above_hum_min_pct,
                ROUND(SUM(CASE WHEN rom.dew_point_2m_max < c.humidity_max THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS below_hum_max_pct,
                ROUND(SUM(CASE WHEN rom.dew_point_2m_min > c.humidity_min
                        AND rom.dew_point_2m_max < c.humidity_max
                    THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS within_hum_rng_pct
            FROM main.rc_open_meteo rom
            JOIN main.region r ON rom.region_id = r.id
            CROSS JOIN main.regional_criterion c
            WHERE c.tree_id = $tree_id
            GROUP BY r.id, r."name"
            ORDER BY all_conditions_met_pct DESC;
            """

        let p = cmd.CreateParameter(ParameterName = "tree_id", Value = box treeId)
        cmd.Parameters.Add p |> ignore

        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readRegionalClimateStatistics reader ]
        conn.Close()
        results

    let haversineKm (lat1: float, lon1: float) (lat2: float, lon2: float) =
        let rad d = d * Math.PI / 180.0
        let dLat = rad (lat2 - lat1)
        let dLon = rad (lon2 - lon1)
        let a = sin (dLat / 2.0) ** 2.0 + cos (rad lat1) * cos (rad lat2) * sin (dLon / 2.0) ** 2.0
        6371.0 * 2.0 * asin (sqrt a)

    let [<Literal>] maxDistanceKm = 50.0

    let getPollenStatisticsClosestToRegion (conn: DuckDBConnection) (treeId: int) =
        let regions = getRegionList conn
        let pollenData = Pollen.Load(__SOURCE_DIRECTORY__ + latestPollenDataFile)
        let countyMappingData = County.Load(__SOURCE_DIRECTORY__ + latestCountyDataFile)

        let countyCentroids =
            countyMappingData.Rows
            |> Seq.map (fun c -> c.GEOIDFQ.Substring(c.GEOIDFQ.Length - 5), (float c.INTPTLAT, float c.INTPTLONG))
            |> dict

        let countyHighPct =
            pollenData.Rows
            |> Seq.filter (fun r -> r.OBS_PER_WEEK > 5)
            |> Seq.groupBy (fun r -> r.COUNTY_FIPS.Trim().TrimStart('\'').PadLeft(5, '0'))
            |> Seq.filter (fun (_, rows) -> rows |> Seq.distinctBy (fun r -> r.YEAR) |> Seq.length > 10)
            |> Seq.choose (fun (fips, rows) ->
                match countyCentroids.TryGetValue fips with
                | true, centroid ->
                    let rows = Seq.toArray rows
                    let high = rows |> Array.filter (fun r -> r.POLLEN_COUNT > 100m) |> Array.length
                    Some(fips, centroid, 100.0 * float high / float rows.Length)
                | _ -> None)
            |> Seq.toArray

        regions
        |> Seq.choose (fun r ->
            let center = (r.BBox.MinLat + r.BBox.MaxLat) / 2.0, (r.BBox.MinLon + r.BBox.MaxLon) / 2.0
            let fips, centroid, pct =
                countyHighPct |> Array.minBy (fun (_, centroid, _) -> haversineKm center centroid)
            if haversineKm center centroid <= maxDistanceKm then Some(r, pct) else None)
        |> Seq.toList
        
    let getPollenStatisticsWithinRegion (conn: DuckDBConnection) (treeId: int) =
        printfn "Loading pollen data from %s" latestPollenDataFile
        // Note: this version doesn't work because the regions are too small
        // eligible rows with > 5 observations and > 10 years of data
        // join pollenData to countyMappingData to regions to return (region, percent weeks with high pollen count)

        let regions = getRegionList conn
        let pollenData = Pollen.Load(__SOURCE_DIRECTORY__ + latestPollenDataFile)
        let countyMappingData = County.Load(__SOURCE_DIRECTORY__ + latestCountyDataFile)

        let countyRegionMap =
            countyMappingData.Rows
            |> Seq.choose (fun c ->
                let fips = c.GEOIDFQ.Substring(c.GEOIDFQ.Length - 5)
                let lat = float c.INTPTLAT
                let lon = float c.INTPTLONG
                regions
                |> Seq.tryFind (fun r ->
                    lat >= r.BBox.MinLat && lat <= r.BBox.MaxLat
                    && lon >= r.BBox.MinLon && lon <= r.BBox.MaxLon)
                |> Option.map (fun r -> fips, r.Id))
            |> dict
        
        countyRegionMap
        |> Seq.countBy (fun kv -> kv.Value)
        |> Seq.iter (fun (id, n) -> printfn "map: region %d has %d counties" id n)

        pollenData.Rows
        |> Seq.filter (fun r -> r.OBS_PER_WEEK > 5)
        |> Seq.groupBy (fun r -> r.COUNTY_FIPS.Trim().TrimStart('\'').PadLeft(5, '0'))
        |> Seq.filter (fun (_, rows) -> rows |> Seq.distinctBy (fun r -> r.YEAR) |> Seq.length > 10)
        |> Seq.choose (fun (fips, rows) ->
            match countyRegionMap.TryGetValue fips with
            | true, regionId -> Some(regionId, rows)
            | _ -> None)
        |> Seq.groupBy fst
        |> Seq.map (fun (regionId, countyGroups) ->
            let rows = countyGroups |> Seq.collect snd |> Seq.toArray
            let highWeeks = rows |> Array.filter (fun r -> r.POLLEN_COUNT > 100m) |> Array.length
            regionId, 100.0 * float highWeeks / float rows.Length)
        |> Seq.toList

    let getHomelessStatistics =
        let readSheets path =
            use stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            use reader = ExcelReaderFactory.CreateReader(stream)
            let ds = reader.AsDataSet()
            [ for table in ds.Tables ->
                table.TableName,
                [ for row in table.Rows -> [ for item in row.ItemArray -> item ] ] ]

        let showFields path sheetName =
            use stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            use reader = ExcelReaderFactory.CreateReader(stream)
            let ds = reader.AsDataSet()
            let table = ds.Tables.[sheetName: string]
            table.Rows.[0].ItemArray
            |> Array.iteri (fun i v -> printfn "%d: %O" i v)
        readSheets (__SOURCE_DIRECTORY__ + latestHomelessDataFile)
        |> List.iter (fun (name, rows) -> printfn "%s: %d rows" name rows.Length)
        |> ignore
        printfn "2025 sheet contains the following columns:"
        showFields (__SOURCE_DIRECTORY__ + latestHomelessDataFile) "2025"
        printfn "CoCs, States, DDs sheet contains the following columns:"
        showFields (__SOURCE_DIRECTORY__ + latestHomelessDataFile) "CoCs, States, DDs"

    let insertBatchAndDailyWeatherList (conn: DuckDBConnection) (region: Region) (batchFunc: Region -> List<float * float * OpenMeteoDailyWeather>) =
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
        let dailyWeatherList = batchFunc region
        let insertCount = 
            dailyWeatherList
            |> List.sumBy (fun (lat, lon, dailyWeather) ->
                let cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- """
                    INSERT INTO main.rc_open_meteo (
                        batch_id,
                        region_id,
                        lat,
                        lon,
                        "time",
                        temperature_2m_min,
                        temperature_2m_mean,
                        temperature_2m_max,
                        dew_point_2m_min,
                        dew_point_2m_mean,
                        dew_point_2m_max)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    ON CONFLICT(lat, lon, "time") DO NOTHING
                """
                addParam cmd batchId
                addParam cmd region.Id
                addParam cmd lat
                addParam cmd lon
                addParam cmd dailyWeather.Date
                addParam cmd dailyWeather.TempMin
                addParam cmd dailyWeather.TempMean
                addParam cmd dailyWeather.TempMax
                addParam cmd dailyWeather.HumidityMin
                addParam cmd dailyWeather.HumidityMean
                addParam cmd dailyWeather.HumidityMax
                cmd.ExecuteNonQuery()
            )
        printfn "Inserted %d new Open-Meteo daily weather records (skipped %d duplicates)" insertCount (dailyWeatherList.Length - insertCount)
        let updateCmd = conn.CreateCommand()
        updateCmd.Transaction <- tran
        updateCmd.CommandText <- "UPDATE main.batch SET status = ?, end_utc = ? WHERE id = ?"
        addParam updateCmd "Success"
        addParam updateCmd DateTime.UtcNow
        addParam updateCmd batchId
        updateCmd.ExecuteNonQuery() |> ignore
        tran.Commit()
        conn.Close()

module Score =
    open Sql

    let readScore (reader: DuckDBDataReader) =
        {
            Id = reader.GetInt32(0)
            EvalMode = reader.GetString(1)
            LocationId = reader.GetInt32(2)
            CriterionId = reader.GetInt32(3)
            KeyPoiId = reader.GetInt32(4)
            Raw = reader.GetDouble(5)
            Normalized = reader.GetDouble(6)
        }

    let getScoreList (conn: DuckDBConnection) (limit0: int option) =
        let limit = defaultArg limit0 20
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            SELECT id, location_id, criterion_id, key_poi_id, raw, normalized
            FROM main.score
            LIMIT ?
        """
        addParam cmd limit
        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readScore reader ]
        conn.Close()
        results

    let insertScoreList (conn: DuckDBConnection) (scores: ScoreRow list) =
        conn.Open()
        use tran = conn.BeginTransaction()

        let insertCount =
            scores
            |> List.sumBy (fun s ->
                let cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- """
                    INSERT INTO main.score (eval_mode, location_id, criterion_id, key_poi_id, raw_value, norm_value)
                    VALUES (?, ?, ?, ?, ?, ?)
                """
                addParam cmd s.EvalMode
                addParam cmd s.LocationId
                addParam cmd s.CriterionId
                addParam cmd s.KeyPoiId
                addParam cmd s.Raw
                addParam cmd s.Normalized
                cmd.ExecuteNonQuery()
            )

        tran.Commit()
        conn.Close()
        insertCount

module Criterion =
    open Sql

    let readTree (reader: DuckDBDataReader) =
        {
            Id = reader.GetInt32 0
            Name = reader.GetString 1
        }

    let readCriterion (reader: DuckDBDataReader) =
        {
            Id = reader.GetInt32 0
            TreeId = reader.GetInt32 1
            Lft = reader.GetInt32 2
            Rgt = reader.GetInt32 3
            Operator = if reader.IsDBNull 4 then None else Some (reader.GetInt32 4)
            CategoryId = if reader.IsDBNull 5 then None else Some (reader.GetInt32 5)
            DistAmt = if reader.IsDBNull 6 then None else Some (reader.GetDouble 6)
        }

    let getTreeList (conn: DuckDBConnection) =
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            SELECT id, "name"
            FROM main.tree
        """
        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readTree reader ]
        conn.Close()
        results

    let getCriterionList (conn: DuckDBConnection) (treeId: int) (limit0: int option) =
        let limit = defaultArg limit0 20
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            SELECT id, tree_id, lft, rgt, operator, category_id, dist_amt
            FROM main.criterion
            WHERE tree_id = ?
            LIMIT ?
        """
        addParam cmd treeId
        addParam cmd limit
        use reader = cmd.ExecuteReader()
        let results = [ while reader.Read() do readCriterion reader ]
        conn.Close()
        results

    let treeIdToName i = char (int 'a' + i - 1)
    let replaceRegionalCriterionList (conn: DuckDBConnection) (rows: RegionalCriterionRow list) =
        conn.Open()
        use tran = conn.BeginTransaction()

        let treeIds = rows |> List.map (fun x -> x.TreeId) |> List.distinct

        let criteriaDeleteCount =
            treeIds
            |> List.sumBy (fun treeId ->
                use cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- "DELETE FROM main.regional_criterion WHERE tree_id = ?"
                addParam cmd treeId
                cmd.ExecuteNonQuery()
            )

        let treeUpsertCount =
            treeIds
            |> List.sumBy (fun treeId ->
                use cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- """
                    INSERT INTO main.tree (id, "name") VALUES (?, ?)
                    ON CONFLICT (id) DO UPDATE SET "name" = excluded."name"
                """
                addParam cmd treeId
                addParam cmd (treeIdToName treeId)
                cmd.ExecuteNonQuery()
            )

        let criteriaInsertCount =
            rows
            |> List.sumBy (fun r ->
                use cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- """
                    INSERT INTO main.regional_criterion (tree_id, temp_min, temp_max, humidity_min, humidity_max, pollen_max, job_soc_code, job_count_min, job_median_annual_wage_min)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                """
                addParam cmd r.TreeId
                addParam cmd (optionToObj r.TempMin)
                addParam cmd (optionToObj r.TempMax)
                addParam cmd (optionToObj r.HumMin)
                addParam cmd (optionToObj r.HumMax)
                addParam cmd (optionToObj r.PollenMax)
                addParam cmd (optionToObj r.JobSocCode)
                addParam cmd (optionToObj r.JobCountMin)
                addParam cmd (optionToObj r.JobMedianAnnualWageMin)
                cmd.ExecuteNonQuery()
            )

        tran.Commit()
        conn.Close()
        criteriaDeleteCount + treeUpsertCount + criteriaInsertCount

    let replaceCriterionList (conn: DuckDBConnection) (rows: CriterionRow list) =
        conn.Open()
        use tran = conn.BeginTransaction()

        let treeIds = rows |> List.map (fun x -> x.TreeId) |> List.distinct

        let criteriaDeleteCount =
            treeIds
            |> List.sumBy (fun treeId ->
                use cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- "DELETE FROM main.criterion WHERE tree_id = ?"
                addParam cmd treeId
                cmd.ExecuteNonQuery()
            )

        let treeUpsertCount =
            treeIds
            |> List.sumBy (fun treeId ->
                use cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- """
                    INSERT INTO main.tree (id, "name") VALUES (?, ?)
                    ON CONFLICT (id) DO UPDATE SET "name" = excluded."name"
                """
                addParam cmd treeId
                addParam cmd (treeIdToName treeId)
                cmd.ExecuteNonQuery()
            )

        let criteriaInsertCount =
            rows
            |> List.sumBy (fun r ->
                use cmd = conn.CreateCommand()
                cmd.Transaction <- tran
                cmd.CommandText <- """
                    INSERT INTO main.criterion (tree_id, lft, rgt, operator, category_id, dist_amt)
                    VALUES (?, ?, ?, ?, ?, ?)
                """
                addParam cmd r.TreeId
                addParam cmd r.Lft
                addParam cmd r.Rgt
                addParam cmd (optionToObj r.Operator)
                addParam cmd (optionToObj r.CategoryId)
                addParam cmd (optionToObj r.DistAmt)
                cmd.ExecuteNonQuery()
            )

        tran.Commit()
        conn.Close()
        criteriaDeleteCount + treeUpsertCount + criteriaInsertCount
