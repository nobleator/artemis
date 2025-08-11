module Program

open Elmish
open Elmish.React
open Thoth.Fetch
open Thoth.Json
open Fable.Core
open Fable.Core.JsInterop
open DomainTypes
open Evaluator
open View

let [<Literal>] dbName = "artemis-db"
let [<Literal>] storeName = "tree"

importSideEffects "./style.css"
importSideEffects "leaflet/dist/leaflet.css"

let idb: obj = importAll "idb"

let getDb () : JS.Promise<obj> =
    idb?openDB(dbName, 1, createObj [ 
        "upgrade" ==> fun (db: obj) ->
            if not (db?objectStoreNames?contains storeName) then
                db?createObjectStore storeName |> ignore
    ])

type StorageResult<'T> =
    | Success of 'T
    | Failure of string

type ICriteriaRepository =
    abstract LoadTree: unit -> JS.Promise<StorageResult<FlatNode list>>
    abstract SaveTree: FlatNode list -> JS.Promise<StorageResult<unit>>

// TODO: hybrid storage implementation with sync to backend
type IndexedDbRepository() =
    interface ICriteriaRepository with
        member _.LoadTree() =
            promise {
                try
                    let! db = getDb()
                    let! raw = db?get(storeName, "tree") |> unbox<JS.Promise<string option>>
                    match raw with
                    | Some json ->
                        match Decode.fromString (Decode.list FlatNode.decoder) json with
                        | Ok nodes -> return Success nodes
                        | Error e -> return Failure $"Decode error: {e}"
                    | None -> return Success []
                with ex ->
                    return Failure $"IndexedDB load failed: {ex.Message}"
            }
        member _.SaveTree nodes =
            promise {
                try
                    let json = Encode.toString 2 (Encode.list (List.map FlatNode.encoder nodes))
                    let! db = getDb()
                    do! db?put(storeName, json, "tree") |> unbox<JS.Promise<unit>>
                    return Success ()
                with ex ->
                    return Failure $"IndexedDB save failed: {ex.Message}"
            }

let mutable repo: ICriteriaRepository = IndexedDbRepository()

let loadListings () =
    promise {
        let! res = Fetch.get<unit, ListingCard list>("/data/listing/london.json", decoder = Decode.list ListingCard.decoder)
        return res
    }

let loadPOIsFromJson (filename: string) =
    Fetch.get<unit, POI list>($"/{filename}",decoder = Decode.list POI.decoder)

let loadAllPOIs () =
    promise {
        let! results =
            Category.all
            |> List.map (fun c -> 
                let path = $"data/poi/{Category.name c}.json"
                loadPOIsFromJson path)
            |> Promise.all

        return results |> List.concat
    }

let rec flattenTree (node: TreeNode) : FlatNode list =
    node.flat :: (node.children |> List.collect flattenTree)

let prepareTreeForSaving (tree: TreeNode) : FlatNode list =
    let updatedTree, _ = TreeNode.assignNestedSetIndices 1 tree
    flattenTree updatedTree

let fetchTree : Cmd<Msg> =
    Cmd.OfPromise.either
        (fun () -> repo.LoadTree())
        ()
        (function
         | Success data -> TreeLoaded (data |> List.toArray)
         | Failure err -> TreeSaveFailed err)
        (fun ex -> TreeSaveFailed ex.Message)

let init () : Model * Cmd<Msg> =
    { 
        root = None
        isLoading = true
        leftPanelState = Both
        listings = []
        selectedListingId = None
        sortState = ScoreDesc
        modalHidden = true
    }, fetchTree

let rec toggleNode targetId (node: TreeNode) : TreeNode =
    if node.flat.id = targetId then
        { node with isExpanded = not node.isExpanded }
    else
        { node with children = node.children |> List.map (toggleNode targetId) }

let rec addChildNode (targetId: int) (newNode: TreeNode) (node: TreeNode) : TreeNode =
    if node.flat.id = targetId then
        { node with children = node.children @ [ newNode ] }
    else
        { node with children = node.children |> List.map (addChildNode targetId newNode) }

let rec updateNode mapFn nodeId node =
    if node.flat.id = nodeId then
        { node with flat = mapFn node.flat }
    else
        { node with children = node.children |> List.map (updateNode mapFn nodeId) }

let rec removeNode targetId (node: TreeNode) : TreeNode option =
    if node.flat.id = targetId then
        None
    else
        let updatedChildren =
            node.children
            |> List.choose (removeNode targetId)
        Some { node with children = updatedChildren }

let update msg model : Model * Cmd<Msg> =
    match msg with
    | TreeLoaded flatNodes ->
        let cmd =
            Cmd.OfPromise.either
                loadListings
                ()
                (fun listings -> ListingsLoaded listings)
                (fun ex -> ListingsLoadFailed ex.Message)
        match flatNodes.Length = 0 with
        | true ->
            let root = {
                id = 0;
                lft = 1;
                rgt = 2;
                nodeType = NodeType.GROUP;
                operator = Some "AND";
                parent_id = None
                category = None
                radius = None
            }
            let rootTree = TreeBuilder.fromFlatNodes [root]
            { model with root = Some rootTree; isLoading = false }, cmd
        | false -> 
            let rootTree = TreeBuilder.fromFlatNodes (Array.toList flatNodes)
            { model with root = Some rootTree; isLoading = false }, cmd
    | SaveTree ->
        match model.root with
        | Some root ->
            let flatNodes = prepareTreeForSaving root
            let cmd =
                Cmd.OfPromise.either
                    (fun () -> repo.SaveTree flatNodes)
                    ()
                    (function
                        | Success _ -> TreeSaved
                        | Failure err -> TreeSaveFailed err)
                    (fun ex -> TreeSaveFailed ex.Message)
            model, cmd
        | None -> model, Cmd.none
    | TreeSaved ->
        let cmd =
            Cmd.OfPromise.either
                loadAllPOIs              
                ()
                (fun points -> POIsLoaded points)
                (fun ex -> POILoadFailed ex.Message)
        model, cmd
    | TreeSaveFailed msg ->
        printfn "Tree save failed: %s" msg
        model, Cmd.none
    | ListingsLoaded listings ->
        // TODO un-scored listings flash briefly on the screen
        let cmd =
            Cmd.OfPromise.either
                loadAllPOIs              
                ()
                (fun points -> POIsLoaded points)
                (fun ex -> POILoadFailed ex.Message)
        match model.root with
        | Some _ -> { model with listings = listings }, cmd
        | None -> model, Cmd.none
    | ListingsLoadFailed err -> 
        printfn "Failed to load listings: %s" err
        model, Cmd.none
    | POIsLoaded poiList ->
        match model.root with
        | Some tree ->
            { model with listings = model.listings |> List.map (fun l -> { l with score = Some (score tree poiList l) }) |> normalizeScores }, Cmd.none
        | None -> model, Cmd.none
    | POILoadFailed err ->
        printfn "Failed to load POIs: %s" err
        model, Cmd.none
    | Toggle id ->
        match model.root with
        | Some root ->
            let updatedRoot = toggleNode id root
            { model with root = Some updatedRoot }, Cmd.none
        | None -> model, Cmd.none
    | AddTermChild parentId ->
        match model.root with
        | Some root ->
            let newId = TreeNode.findMaxId root 0 + 1
            let newFlatNode: FlatNode =
                { id = newId
                  parent_id = Some parentId
                  lft = 0
                  rgt = 0
                  nodeType = NodeType.TERM
                  operator = None
                  category = Some Library
                  radius = Some 1.0 }
            let newTreeNode: TreeNode = { flat = newFlatNode; isExpanded = true; children = [] }
            let updatedRoot = addChildNode parentId newTreeNode root
            { model with root = Some updatedRoot }, Cmd.none
        | None -> model, Cmd.none
    | AddGroupChild parentId ->
        match model.root with
        | Some root ->
            let newId = TreeNode.findMaxId root 0 + 1
            let newFlatNode: FlatNode =
                { id = newId
                  parent_id = Some parentId
                  lft = 0
                  rgt = 0
                  nodeType = NodeType.GROUP
                  operator = Some "AND"
                  category = None
                  radius = None }
            let newTreeNode: TreeNode = { flat = newFlatNode; isExpanded = true; children = [] }
            let updatedRoot = addChildNode parentId newTreeNode root
            { model with root = Some updatedRoot }, Cmd.none
        | None -> model, Cmd.none
    | UpdateTermCategory (id, newCategory) ->
        match model.root with
        | Some root ->
            let updated = updateNode (fun flat ->
                match flat.nodeType with
                | NodeType.TERM -> { flat with category = Some newCategory }
                | _ -> flat) id root
            { model with root = Some updated }, Cmd.none
        | None -> model, Cmd.none
    | UpdateTermRadius (id, newRadius) ->
        match model.root with
        | Some root ->
            let updated = updateNode (fun flat ->
                match flat.nodeType with
                | NodeType.TERM -> { flat with radius = Some newRadius }
                | _ -> flat) id root
            { model with root = Some updated }, Cmd.none
        | None -> model, Cmd.none
    | UpdateGroupOperator (id, newOp) ->
        match model.root with
        | Some root ->
            let updated = updateNode (fun flat ->
                match flat.nodeType with
                | NodeType.GROUP -> { flat with operator = Some newOp }
                | _ -> flat) id root
            { model with root = Some updated }, Cmd.none
        | None -> model, Cmd.none
    | DeleteNode targetId ->
        match model.root with
        | Some root ->
            // Prevent deletion of root
            if root.flat.id = targetId then
                printfn "Cannot delete the root node."
                model, Cmd.none
            else
                match removeNode targetId root with
                | Some updatedRoot ->
                    { model with root = Some updatedRoot }, Cmd.none
                | None ->
                    printfn "Unexpected: root was deleted"
                    model, Cmd.none
        | None -> model, Cmd.none
    | ToggleTopPanel ->
        let newState =
            match model.leftPanelState with
            | Both -> TopExpanded
            | TopExpanded -> Both
            | BottomExpanded -> TopExpanded
        { model with leftPanelState = newState }, Cmd.none
    | ToggleBottomPanel ->
        let newState =
            match model.leftPanelState with
            | Both -> BottomExpanded
            | BottomExpanded -> Both
            | TopExpanded -> BottomExpanded
        { model with leftPanelState = newState }, Cmd.none    
    | SelectListing id -> { model with selectedListingId = Some id }, Cmd.none
    | MarkerClicked id -> { model with selectedListingId = Some id }, Cmd.none
    | ToggleSort ->
        let newState =
            match model.sortState with
            | ScoreDesc -> ScoreAsc
            | ScoreAsc -> PriceDesc
            | PriceDesc -> PriceAsc
            | PriceAsc -> ScoreDesc
        { model with sortState = newState }, Cmd.none 
    | ToggleModal -> { model with modalHidden = not model.modalHidden }, Cmd.none 

Program.mkProgram init update view
|> Program.withReactBatched "artemis-app"
|> Program.run
