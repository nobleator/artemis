namespace Evaluation

open DomainTypes

module Scoring =
    [<Literal>]
    let earthRadiusKm = 6371.0
    let toRadians deg = deg * System.Math.PI / 180.0
    let toDegrees rad = rad * 180.0 / System.Math.PI

    let calculateDistance (loc1Lat: double) (loc1Lon: double) (loc2Lat: double) (loc2Lon: double) : double =
        let lat1 = toRadians loc1Lat
        let lon1 = toRadians loc1Lon
        let lat2 = toRadians loc2Lat
        let lon2 = toRadians loc2Lon
        let dLat = lat2 - lat1
        let dLon = lon2 - lon1
        let a = sin(dLat / 2.0) ** 2.0 + cos lat1 * cos lat2 * sin(dLon / 2.0) ** 2.0
        let c = double 2.0 * atan2 (sqrt a) (sqrt(1.0 - a))
        earthRadiusKm * c

    let findClosestPoi (location: Location) (category: Category) (pois: Poi list) : (double * Poi) option =
        match location.Lat, location.Lon with
        | Some locLat, Some locLon ->
            pois
            |> List.filter (fun poi -> 
                poi.CategoryId = Some (int category) && 
                poi.Lat.IsSome && 
                poi.Lon.IsSome)
            |> List.map (fun poi -> 
                let distance = calculateDistance locLat locLon poi.Lat.Value poi.Lon.Value
                distance, poi)
            |> List.sortBy fst
            |> List.tryHead
        | _ -> None

    let normalizeDistance (distance: double) (maxDistance: double) : double =
        let maxDist = double maxDistance
        if distance >= maxDist then 0.0
        else 1.0 - distance / maxDist

    let getBoundingBox (location: Location) (distKm: double) : BoundingBox option =
        match location.Lat, location.Lon with
        | Some lat, Some lon ->
            let angularDistRad = distKm / earthRadiusKm
            let latRad = toRadians lat
            // Calculate latitude delta (same at all longitudes)
            let latDelta = toDegrees angularDistRad
            // Calculate longitude delta (varies with latitude)
            // At higher latitudes, longitude lines are closer together
            let lonDelta =
                if System.Math.Abs(float (System.Math.Cos(float latRad))) > 0.0001 then
                    toDegrees (angularDistRad / System.Math.Cos(float latRad))
                else
                    // Near poles, use a large value
                    180.0
            Some {
                MinLat = lat - latDelta
                MaxLat = lat + latDelta
                MinLon = lon - lonDelta
                MaxLon = lon + lonDelta
            }
        | _ -> None
    
    let scoreTermNode (location: Location) (id: int) (category: Category) (distAmt: double) (pois: Poi list) : Score =
        match findClosestPoi location category pois with
        | Some (distance, poi) ->
            printfn "Found POI %A km away" distance
            { Node = TermNode(id, category, distAmt)
              Raw = distance
              Normalized = normalizeDistance distance distAmt
              KeyPoi = Some poi
              Children = [] }
        | None ->
            printfn "No POI with category %A within %A km of %A" category distAmt location.Name
            { Node = TermNode(id, category, distAmt)
              Raw = System.Double.PositiveInfinity
              Normalized = 0.0
              KeyPoi = None
              Children = [] }

    let rec scoreTree (location: Location) (getPoiFunc: Category -> BoundingBox -> Poi list) (node: CriteriaNode) : Score =
        match node with
        | TermNode(id, category, distAmt) ->
            match getBoundingBox location distAmt with
            | Some bbox -> 
                let poiList = getPoiFunc category bbox
                printfn "%A POI found for category %A" poiList.Length category
                poiList
                |> scoreTermNode location id category distAmt
            | _ ->
                printfn "Uh oh, no bbox for location %A" location.Name
                { Node = TermNode(id, category, distAmt)
                  Raw = System.Double.PositiveInfinity
                  Normalized = 0.0
                  KeyPoi = None
                  Children = [] }
        | GroupNode(id, operator, children) ->
            let childScores = children |> List.map (scoreTree location getPoiFunc)
            let aggregatedScore = 
                match operator with
                | OperatorType.And -> childScores |> List.map (fun s -> s.Normalized) |> List.min
                | OperatorType.Or -> childScores |> List.map (fun s -> s.Normalized) |> List.max
                | _ -> 0.0
            let keyPoi =
                match operator with
                | OperatorType.And ->
                    childScores
                    |> List.minBy (fun s -> s.Normalized)
                    |> fun s -> s.KeyPoi
                | OperatorType.Or ->
                    childScores 
                    |> List.maxBy (fun s -> s.Normalized)
                    |> fun s -> s.KeyPoi
                | _ -> None
            { Node = GroupNode(id, operator, childScores |> List.map (fun s -> s.Node))
              Raw = 0.0
              Normalized = aggregatedScore
              KeyPoi = keyPoi
              Children = childScores }

    let rec printScores indent (score: Score) =
        match score.Node with
        | GroupNode(id, op, _) ->
            let opStr = if op = OperatorType.And then "AND" else "OR"
            let keyPoiStr = 
                match score.KeyPoi with
                | Some poi -> sprintf " [Key POI: %d]" poi.Id
                | None -> ""
            printfn "%s%s (id: %d) - Score: %.3f%s" indent opStr id score.Normalized keyPoiStr
            score.Children |> List.iter (printScores (indent + "  "))
        | TermNode(id, cat, dist) ->
            let poiStr = 
                match score.KeyPoi with
                | Some poi -> sprintf " [POI: %d, %.2f km]" poi.Id score.Raw
                | None -> " [No POI found]"
            printfn "%s%A < %.3f (id: %d) - Score: %.3f%s" indent cat dist id score.Normalized poiStr
