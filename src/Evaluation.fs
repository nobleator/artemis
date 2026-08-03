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
    
    let scoreTermNode (location: Location) (id: int) (category: Category) (distAmt: double) (normalizeDistanceFunc: double -> double -> double) (closestPoi: Poi option)  : Score =
        match location.Lat, location.Lon, Option.bind (fun x -> x.Lat) closestPoi, Option.bind (fun x -> x.Lon) closestPoi with
        | Some loc1Lat, Some loc1Lon, Some loc2Lat, Some loc2Lon ->
            let distance = calculateDistance loc1Lat loc1Lon loc2Lat loc2Lon
            {
                Node = TermNode(id, category, distAmt)
                Raw = distance
                Normalized = normalizeDistanceFunc distance distAmt
                KeyPoi = closestPoi
                Children = []
            }
        | _ ->
            {
                Node = TermNode(id, category, distAmt)
                Raw = System.Double.PositiveInfinity
                Normalized = 0.0
                KeyPoi = None
                Children = []
            }

    let rec scoreTree (location: Location) (getPoiFunc: Category -> Location -> Poi option) (normalizeDistanceFunc: double -> double -> double) (node: CriteriaNode) : Score =
        match node with
        | TermNode(id, category, distAmt) ->
            getPoiFunc category location
            |> scoreTermNode location id category distAmt normalizeDistanceFunc
        | GroupNode(id, operator, children) ->
            let childScores = children |> List.map (scoreTree location getPoiFunc normalizeDistanceFunc)
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
            {
                Node = GroupNode(id, operator, childScores |> List.map (fun s -> s.Node))
                Raw = 0.0
                Normalized = aggregatedScore
                KeyPoi = keyPoi
                Children = childScores
            }

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
