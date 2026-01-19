namespace DomainTypes

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
    | Job = 16
    | BikeTrail = 17

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

type Score = {
    Node: CriteriaNode
    Raw: double
    Normalized: double
    KeyPoi: Poi option
    Children: Score list
}
