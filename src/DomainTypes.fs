namespace DomainTypes

(*
    Goal: multiple scoring modes
    1) Linear bounded (current)
    2) Exponential decay (new)

    Linear bounded requires a distance threshold, exponential decay does not
    To avoid passing the DuckDB connection everywhere, we have a function defined in `Program`, and the `Evaluation` code simple executes that function
    The "normalized" distance is the main difference between the 2 scoring modes
*)
type EvalOption =
    | LinearBoundedDistance
    | ExponentialDecayDistance
    // Weighted versions of all the above
    // Versions with count of points rather than closest
    static member TryParse = function
        | "lin" -> Ok LinearBoundedDistance
        | "exp" -> Ok ExponentialDecayDistance
        | x -> Error $"Invalid eval option: {x}"
    static member toStr = function
        | LinearBoundedDistance -> "lin"
        | ExponentialDecayDistance -> "exp"

type Location = {
    Id: int option
    Name: string
    Address: string option
    Lat: double option
    Lon: double option
    Notes: string option
    PriceAmt: int option
    PriceCcy: string option
}

type Region =
    | NewYork
    | WashingtonDC

type Poi = {
    Id: int
    BatchId: int
    Source: string
    SourceXref: string option
    CategoryId: int option
    Lat: double option
    Lon: double option
}

type BoundingBox = {
    MinLat: double
    MinLon: double
    MaxLat: double
    MaxLon: double
}

// type Criterion = {
//     Id: int
//     Left: int
//     Right: int
//     Operator: int option
//     CategoryId: int option
//     TargetValue: decimal option
// }

type Category =
    | Job = 0
    | Airport = 1
    | BusStation = 2
    | CoffeeShop = 3
    | FireStation = 4
    | Grocery = 5
    | Library = 6
    | Park = 7
    | PoliceStation = 8
    | School = 9
    | TrainStation = 10
    | WholeFoods = 11
    | TraderJoes = 12
    | Giant = 13
    | Safeway = 14
    | HarrisTeeter = 15
    | BikeTrail = 16

type OperatorType =
    | And = 0
    | Or = 1

// Raw database row
type CriterionRow = {
    Id: int
    Lft: int
    Rgt: int
    Operator: int option
    CategoryId: int option
    DistAmt: double option
}

// Tree node types
type CriteriaNode =
    | GroupNode of id: int * operator: OperatorType * children: CriteriaNode list
    | TermNode of id: int * category: Category * distAmt: double

type ScoreRow = {
    Id: int
    EvalMode: string // TODO enum
    LocationId: int
    CriterionId: int
    KeyPoiId: int
    Raw: double
    Normalized: double
}

type Score = {
    Node: CriteriaNode
    Raw: double
    Normalized: double
    KeyPoi: Poi option
    Children: Score list
}
