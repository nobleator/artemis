module Evaluator

open System
open DomainTypes

let [<Literal>] normMin = 0.0
let [<Literal>] normMax = 10.0

let add x y =
    x + y

let max x y = 
    if x > y then x
    else y

// Calculates if two lat/lon points are within `maxDistanceKm` kilometers.
let isWithin (lat1: float) (lon1: float) (lat2: float) (lon2: float) (maxDistanceKm: float) =
    let degToRad deg = deg * Math.PI / 180.0
    let earthRadiusKm = 6371.0

    let dLat = degToRad (lat2 - lat1)
    let dLon = degToRad (lon2 - lon1)

    let a =
        Math.Sin(dLat / 2.0) ** 2.0 +
        Math.Cos(degToRad lat1) * Math.Cos(degToRad lat2) *
        Math.Sin(dLon / 2.0) ** 2.0

    let c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a))
    let distance = earthRadiusKm * c

    distance <= maxDistanceKm

let normalizeScores (items: ListingCard list) =    
    let scores =
        items
        |> List.choose (fun i -> i.score)
    match scores with
    | [] -> items
    | _ ->
        let minScore = scores |> List.min
        let maxScore = scores |> List.max
        let range = maxScore - minScore
        if range = normMin then
            // if all scores are the same, then set all Some values to the midpoint
            items
            |> List.map (fun i ->
                match i.score with
                | Some _ -> { i with score = Some ((normMax - normMin) / 2.0) }
                | None -> i)
        else
            items
            |> List.map (fun i ->
                match i.score with
                | Some s -> { i with score = Some ((s - minScore) / range * normMax) }
                | None -> i)

let rec score (tree: TreeNode) (poiList: POI list) (listing: ListingCard) =
    match tree.flat.nodeType with
    | NodeType.GROUP ->
        match tree.flat.operator with
        | Some "AND" -> List.fold add 0.0 (tree.children |> List.map (fun t -> score t poiList listing))
        | Some "OR" -> List.fold max 0.0 (tree.children |> List.map (fun t -> score t poiList listing))
        | _ -> failwith $"Invalid operator: {tree.flat.operator}"
    | NodeType.TERM ->
        match tree.flat.category with
        | Some cat -> 
            match tree.flat.radius with
            | Some r ->
                poiList
                |> List.fold (fun acc x ->
                    if x.category = cat && isWithin x.latitude x.longitude listing.lat listing.lng r
                    then acc + 1.0 else acc) 0.0
            | _ -> failwith $"Invalid radius {tree.flat.radius}"
        | _ -> failwith $"Invalid category: {tree.flat.category}"
    | _ -> failwith $"Unexpected node type: {tree.flat.nodeType}"
