namespace DomainTypes

open Thoth.Json

type Category =
    | Airport
    | BusStation
    | CoffeeShop
    | FireStation
    | Grocery
    | Library
    | Park
    | PoliceStation
    | School
    | TrainStation

module Category =
    let all = [Airport; BusStation; CoffeeShop; FireStation; Grocery; Library; Park; PoliceStation; School; TrainStation]

    let name = function
        | Airport -> "airport"
        | BusStation -> "busstation"
        | CoffeeShop -> "coffeeshop"
        | FireStation -> "firestation"
        | Grocery -> "grocery"
        | Library -> "library"
        | Park -> "park"
        | PoliceStation -> "policestation"
        | School -> "school"
        | TrainStation -> "trainstation"

    let label = function
        | Airport -> "Airport"
        | BusStation -> "Bus Station"
        | CoffeeShop -> "Coffee Shop"
        | FireStation -> "Fire Station"
        | Grocery -> "Grocery"
        | Library -> "Library"
        | Park -> "Park"
        | PoliceStation -> "Police Station"
        | School -> "School"
        | TrainStation -> "Train Station"

    let ofName = function
        | "airport" -> Some Airport
        | "busstation" -> Some BusStation
        | "coffeeshop" -> Some CoffeeShop
        | "firestation" -> Some FireStation
        | "grocery" -> Some Grocery
        | "library" -> Some Library
        | "park" -> Some Park
        | "policestation" -> Some PoliceStation
        | "school" -> Some School
        | "trainstation" -> Some TrainStation
        | _ -> None

    let encoder (c: Category) = Encode.string (name c)
    let netEncoder (c: Category) = Thoth.Json.Net.Encode.string (name c)
    let decoder : Decoder<Category> =
        Decode.string
        |> Decode.map (fun s -> s.ToLowerInvariant())
        |> Decode.andThen (fun s ->
            match ofName s with
            | Some c -> Decode.succeed c
            | None -> Decode.fail $"Unknown category '{s}'")

type ListingCard = {
    id: int
    address: string
    lat: double
    lon: double
    price: int
    score: double option
}

module ListingCard =
    let decoder : Decoder<ListingCard> =
        Decode.object (fun get ->
            {
                id = get.Required.Field "id" Decode.int
                address = get.Required.Field "address" Decode.string
                lat = get.Required.Field "lat" Decode.float
                lon = get.Required.Field "lon" Decode.float
                price = get.Required.Field "price" Decode.int
                score = get.Optional.Field "score" Decode.float
            }
        )

type NodeType =
    | GROUP = 0
    | TERM = 1

// TODO Operator type instead of string
type FlatNode = {
    id: int
    parent_id: int option
    lft: int
    rgt: int
    nodeType: NodeType
    operator: string option
    category: Category option
    radius: float option
}

type Node =
    | Group of FlatNode * Node list
    | Term of FlatNode

module NodeType =
    let toString nt =
        match nt with
        | NodeType.GROUP -> "GROUP"
        | NodeType.TERM -> "TERM"
        | _ -> failwith "Unknown NodeType"
    
    let fromString (s: string) =
        match s.ToUpperInvariant() with
        | "GROUP" -> Ok NodeType.GROUP
        | "TERM" -> Ok NodeType.TERM
        | other -> failwithf "Invalid NodeType value: '%s'" other

    let decoder : Decoder<NodeType> =
        Decode.oneOf [
            Decode.string
            |> Decode.andThen (fun str ->
                match str.ToUpperInvariant() with
                | "GROUP" -> Decode.succeed NodeType.GROUP
                | "TERM" -> Decode.succeed NodeType.TERM
                | other -> Decode.fail $"Unexpected NodeType string: {other}"
            )
            Decode.int
            |> Decode.andThen (fun i ->
                match i with
                | 0 -> Decode.succeed NodeType.GROUP
                | 1 -> Decode.succeed NodeType.TERM
                | x -> Decode.fail $"Unexpected NodeType int: {x}"
            )
        ]

    let encoder (nt: NodeType) =
        nt |> toString |> Encode.string

module FlatNode =
    let decoder : Decoder<FlatNode> =
        Decode.object (fun get ->
        {
            id = get.Required.Field "id" Decode.int
            parent_id = get.Optional.Field "parent_id" Decode.int
            lft = get.Required.Field "lft" Decode.int
            rgt = get.Required.Field "rgt" Decode.int
            nodeType = get.Required.Field "nodeType" NodeType.decoder
            operator = get.Optional.Field "operator" Decode.string
            category = get.Optional.Field "category" Category.decoder
            radius = get.Optional.Field "radius" Decode.float
        })

    let encoder (node: FlatNode) : JsonValue =
        Encode.object [
            "id", Encode.int node.id
            "parent_id", Encode.option Encode.int node.parent_id
            "lft", Encode.int node.lft
            "rgt", Encode.int node.rgt
            "nodeType", NodeType.encoder node.nodeType
            "operator", Encode.option Encode.string node.operator
            "category", Encode.option Category.encoder node.category
            "radius", Encode.option Encode.float node.radius
        ]

type POI = {
    category: Category
    source: string
    source_xref: string
    latitude: float
    longitude: float 
}

module POI =
    let netEncoder (poi: POI) =
        Thoth.Json.Net.Encode.object [
            "category", Category.netEncoder poi.category
            "source", Thoth.Json.Net.Encode.string poi.source
            "source_xref", Thoth.Json.Net.Encode.string poi.source_xref
            "latitude", Thoth.Json.Net.Encode.float poi.latitude
            "longitude", Thoth.Json.Net.Encode.float poi.longitude
        ]

    let decoder : Decoder<POI> =
        Decode.object (fun get ->
        {
            category = get.Required.Field "category" Category.decoder
            source = get.Required.Field "source" Decode.string
            source_xref = get.Required.Field "source_xref" Decode.string
            latitude = get.Required.Field "latitude" Decode.float
            longitude = get.Required.Field "longitude" Decode.float
        })

type NodeId = int

type TreeNode = {
    flat: FlatNode
    children: TreeNode list
    isExpanded: bool
}

module TreeNode =
    /// Recursively assigns lft/rgt values starting from `start`
    /// Returns the updated node and the next available index
    let rec assignNestedSetIndices (start: int) (node: TreeNode) : TreeNode * int =
        // TODO mutable recursive version of this function?
        let mutable idx = start + 1
        let updatedChildren = 
            node.children
            |> List.map (fun child ->
                let updatedChild, nextIdx = assignNestedSetIndices idx child
                idx <- nextIdx
                updatedChild
            )
        let updatedFlat =
            { node.flat with
                lft = start
                rgt = idx }
        { node with flat = updatedFlat; children = updatedChildren }, idx + 1
    
    let rec findMaxId (node: TreeNode) (currentMax: int) : int =
        let newMax = max currentMax node.flat.id
        node.children |> List.fold (fun acc child -> findMaxId child acc) newMax

module TreeBuilder =
    let rec buildTree (nodes: FlatNode list) (parentId: int option) : TreeNode list =
        nodes
        |> List.choose (fun n -> if n.parent_id = parentId then Some n else None)
        |> List.sortBy (fun n -> n.lft)
        |> List.map (fun n ->
            { flat = n
              isExpanded = true
              children = buildTree nodes (Some n.id) })

    let fromFlatNodes (flatNodes: FlatNode list) : TreeNode =
        match buildTree flatNodes None with
        | [ root ] -> root
        | [] -> failwith "No root node found"
        | _ -> failwith "Multiple root nodes found"

type LeftPanelState =
    | Both
    | TopExpanded
    | BottomExpanded

type SortState =
    | ScoreDesc
    | ScoreAsc
    | PriceDesc
    | PriceAsc

type AuthState =
    | Unknown
    | LoggedOut
    | LoggedIn

type TutorialState =
    | Hidden
    | Landing
    | CategorySelect
    | DistanceSelect

type Page =
    | Main
    | Login

type Model = {
    page: Page
    auth: AuthState
    loginEmail: string option
    loginPassword: string option
    loginError: string option
    root: TreeNode option
    isLoading: bool
    leftPanelState: LeftPanelState
    listings: ListingCard list // TODO option instead of initializing with []?
    selectedListingId: int option
    sortState: SortState
    userPanelHidden: bool
    tutorialState: TutorialState
    tutorialCategories: Set<Category>
    tutorialDistance: float option
}

type Msg =
    | Navigate of Page
    | SetLoginEmail of string
    | SetLoginPassword of string
    | Register of string * string // email, password
    | Login of string * string // email, password
    | LoginResult of Result<string, string>
    | Logout
    | LogoutResult
    | TreeLoaded of FlatNode[]
    | SaveTree
    | TreeSaved
    | TreeSaveFailed of string
    | ListingsLoaded of ListingCard list
    | ListingsLoadFailed of string
    | POIsLoaded of POI list
    | POILoadFailed of string
    | Toggle of NodeId
    | AddTermChild of NodeId
    | AddGroupChild of NodeId
    | UpdateTermCategory of NodeId * Category
    | UpdateTermRadius of NodeId * float
    | UpdateGroupOperator of NodeId * string
    | DeleteNode of NodeId
    | ToggleTopPanel
    | ToggleBottomPanel
    | SelectListing of int
    | MarkerClicked of int
    | ToggleSort
    | ToggleModal
    | ToggleUserPanel
    | TutorialNext
    | TutorialBack
    | TutorialToggleCategorySelect of Category
    | TutorialToggleDistanceSelect of float