namespace ELT

open System
open System.Globalization
open System.IO
open System.Net
open System.Net.Http
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
open DuckDB.NET.Data
open ExcelDataReader
open Logger

module Database =
    let exec (conn: DuckDBConnection) (sql: string) =
        use cmd = conn.CreateCommand()
        cmd.CommandText <- sql
        cmd.ExecuteNonQuery()

    let attempt (action: unit -> int) =
        try
            Ok (action ())
        with ex ->
            Error ex.Message

module HttpUtils =
    let maxRetries = 3
    let baseDelayMs = 30_000
    let minRequestIntervalMs = 1_500

    let private client =
        let c = new HttpClient(Timeout = TimeSpan.FromSeconds 120.0)
        c.DefaultRequestHeaders.UserAgent.ParseAdd "Artemis/1.0 (https://github.com/nobleator/artemis)"
        c

    let private isTransient (status: HttpStatusCode) =
        status = HttpStatusCode.TooManyRequests || int status >= 500

    let getStringWithRetry (url: string) =
        let rec attempt n =
            let outcome =
                try
                    use response = client.GetAsync(url).GetAwaiter().GetResult()
                    let body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    if response.IsSuccessStatusCode then
                        Ok body
                    else
                        Error (isTransient response.StatusCode, sprintf "HTTP %d: %s" (int response.StatusCode) body)
                with
                | :? HttpRequestException as ex -> Error (true, ex.Message)
                | :? TaskCanceledException as ex -> Error (true, ex.Message)
            match outcome with
            | Ok body -> body
            | Error (true, message) when n < maxRetries ->
                let delayMs = baseDelayMs * (1 <<< n)
                log "Request failed (attempt %d/%d): %s. Retrying in %dms..."
                    (n + 1) (maxRetries + 1) message delayMs
                Thread.Sleep delayMs
                attempt (n + 1)
            | Error (_, message) -> failwithf "Request failed: %s" message
        attempt 0

module OpenMeteo =
    open HttpUtils

    let startDate = DateOnly(2020, 1, 1)
    let endDate = DateOnly(2025, 12, 31)
    let maxRegionsPerRun = 5
    let isoDate (d: DateOnly) = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)

    let private dailyFields =
        String.Join(
            ",",
            [ "temperature_2m_min"; "temperature_2m_mean"; "temperature_2m_max"
              "relative_humidity_2m_min"; "relative_humidity_2m_max"; "relative_humidity_2m_mean"
              "dew_point_2m_mean"; "dew_point_2m_max"; "dew_point_2m_min" ])

    let private coordinate (value: float) = value.ToString CultureInfo.InvariantCulture

    let private url (lat: float) (lon: float) =
        "https://archive-api.open-meteo.com/v1/archive"
        + $"?start_date={isoDate startDate}"
        + $"&end_date={isoDate endDate}"
        + "&temperature_unit=fahrenheit"
        + $"&daily={dailyFields}"
        + $"&latitude={coordinate lat}"
        + $"&longitude={coordinate lon}"

    let private pendingRegions (conn: DuckDBConnection) =
        use cmd = conn.CreateCommand()
        cmd.CommandText <- $"""
        SELECT
            r.cbsa_code,
            r.lat,
            r.lon,
            count(*) OVER () AS total_pending
        FROM core.dim_region AS r
        WHERE r.is_active
        AND NOT EXISTS (
            SELECT 1
            FROM raw.open_meteo_historical_weather AS w
            WHERE w.cbsa_code = r.cbsa_code
                AND w.start_date = DATE '{isoDate startDate}'
                AND w.end_date = DATE '{isoDate endDate}'
        )
        ORDER BY r.region_id
        LIMIT {maxRegionsPerRun}
        """
        use reader = cmd.ExecuteReader()
        let rows =
            [ while reader.Read() do
                yield (reader.GetString 0, reader.GetDouble 1, reader.GetDouble 2), reader.GetInt64 3 ]
        let totalPending =
            match rows with
            | [] -> 0L
            | (_, total) :: _ -> total
        totalPending, List.map fst rows

    let private save (conn: DuckDBConnection) (cbsa: string) (lat: float) (lon: float) (body: string) =
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
        INSERT OR REPLACE INTO raw.open_meteo_historical_weather
            (cbsa_code, latitude, longitude, start_date, end_date, response)
        VALUES (?, ?, ?, ?, ?, ?)
        """
        for value in [ box cbsa; box lat; box lon; box startDate; box endDate; box body ] do
            cmd.Parameters.Add(DuckDBParameter value) |> ignore
        cmd.ExecuteNonQuery()

    let private fetchRegion conn (cbsa: string, lat: float, lon: float) =
        try
            let body = HttpUtils.getStringWithRetry (url lat lon)
            let saved = save conn cbsa lat lon body
            log "Loaded %s" cbsa
            Ok saved
        with ex ->
            log "Failed %s: %s" cbsa ex.Message
            Error ex.Message

    let load (conn: DuckDBConnection) =
        let totalPending, regions = pendingRegions conn
        log "%d region(s) pending, processing %d this run" totalPending regions.Length
        let results =
            regions
            |> List.map (fun region ->
                let result = fetchRegion conn region
                Thread.Sleep minRequestIntervalMs
                result)
        let loaded = results |> List.sumBy (function Ok n -> n | Error _ -> 0)
        let failed = results |> List.sumBy (function Error _ -> 1 | Ok _ -> 0)
        loaded, failed

module HudCoc =
    open Database
    let private cocNumberPattern = Regex(@"^[A-Z]{2}-\d{3}$", RegexOptions.Compiled)

    let private cell (reader: IExcelDataReader) (index: int) =
        match reader.GetValue index with
        | null -> ""
        | :? DBNull -> ""
        | value -> (string value).Trim()

    let private findHeader (reader: IExcelDataReader) =
        let rec scan remaining =
            if remaining > 0 && reader.Read() then
                let headers = [ for i in 0 .. reader.FieldCount - 1 -> cell reader i ]
                let find (prefix: string) =
                    headers |> List.tryFindIndex (fun h -> h.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                match find "CoC Number", find "CoC Name" with
                | Some numberColumn, Some nameColumn -> Some (numberColumn, nameColumn)
                | _ -> scan (remaining - 1)
            else
                None
        scan 1

    let private readSheet (reader: IExcelDataReader) =
        match Int32.TryParse(reader.Name) with
        | true, year ->
            match findHeader reader with
            | Some (numberColumn, nameColumn) ->
                [ while reader.Read() do
                    let number = cell reader numberColumn
                    let name = cell reader nameColumn
                    if cocNumberPattern.IsMatch number && name <> "" then
                        yield year, number, name ]
            | None -> []
        | _ -> []

    let private readWorkbook (path: string) =
        use stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)
        use reader = ExcelReaderFactory.CreateReader stream
        let rec loop acc =
            let acc = acc @ readSheet reader
            if reader.NextResult() then loop acc else acc
        loop []

    let private save (conn: DuckDBConnection) (rows: (int * string * string) list) =
        exec conn "BEGIN TRANSACTION;" |> ignore
        use cmd = conn.CreateCommand()
        cmd.CommandText <- "INSERT INTO raw.hud_coc (sheet_year, coc_number, coc_name) VALUES (?, ?, ?)"
        let parameters = [ DuckDBParameter(); DuckDBParameter(); DuckDBParameter() ]
        for p in parameters do
            cmd.Parameters.Add p |> ignore
        let inserted =
            rows
            |> List.sumBy (fun (year, number, name) ->
                parameters[0].Value <- box year
                parameters[1].Value <- box number
                parameters[2].Value <- box name
                cmd.ExecuteNonQuery())
        exec conn "COMMIT;" |> ignore
        inserted

    let load (conn: DuckDBConnection) =
        let rows = readWorkbook "/Users/bnoble/Projects/artemis/data/2007-2025-PIT-Counts-by-CoC.xlsb"
        log "Read %d CoC row(s) from workbook" rows.Length
        save conn rows

module Pipeline =
    open Database

    let startDate = DateOnly(2020, 1, 1)
    let endDate = DateOnly(2025, 12, 31)
    let isoDate (d: DateOnly) = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)

    let private runStep (acc: Result<int, string>) (name: string, action: unit -> int) =
        acc
        |> Result.bind (fun total ->
            log "%s" name
            attempt action |> Result.map ((+) total))

    let run (conn: DuckDBConnection) =
        Text.Encoding.RegisterProvider Text.CodePagesEncodingProvider.Instance

        let apiFailures = ref 0

        let loadWeather () =
            let loaded, failed = OpenMeteo.load conn
            apiFailures.Value <- failed
            loaded

        let transform () =
            exec conn $"SET VARIABLE d0 = DATE '{isoDate startDate}'; SET VARIABLE d1 = DATE '{isoDate endDate}';"
            |> ignore
            File.ReadAllText "elt.sql" |> exec conn

        let steps : (string * (unit -> int)) list =
            [ "Loading HUD CoC workbook to `raw.hud_coc`...", (fun () -> HudCoc.load conn)
              "Loading region dimensions...", (fun () -> File.ReadAllText "load_regions.sql" |> exec conn)
              "Loading Open Meteo API results to `raw.open_meteo_historical_weather`...", loadWeather
              "Transforming `raw` to `core`...", transform ]

        match List.fold runStep (Ok 0) steps with
        | Ok modified when apiFailures.Value = 0 ->
            log "Data load completed (%d record(s) modified), ready for analysis." modified
            0
        | Ok modified ->
            log "Data load completed (%d record(s) modified) with %d failed API request(s)." modified apiFailures.Value
            1
        | Error message ->
            log "Data load failed: %s" message
            1