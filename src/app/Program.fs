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
open Browser
open Data

importSideEffects "./style.css"
importSideEffects "leaflet/dist/leaflet.css"

let mutable repo: ICriteriaRepository = IndexedDbRepository()

// TODO add to hybrid storage solution, which would also cache data
let loadListings () =
    promise {
        let! listings =
            Supabase.supabase?from("listing")?select "*"
            |> unbox<JS.Promise<obj>>
            |> Promise.bind (fun raw ->
                match Decode.fromString (Decode.list ListingCard.decoder) (JS.JSON.stringify raw?data) with
                | Ok listings -> Promise.lift listings
                | Error err -> Promise.reject (System.Exception $"Decoding failed: {err}")
            )
        return listings
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

let fetchTree : Cmd<Msg> =
    Cmd.OfPromise.either
        (fun () -> repo.LoadTree())
        ()
        (function
         | Success data -> TreeLoaded data
         | Failure err -> TreeSaveFailed err)
        (fun ex -> TreeSaveFailed ex.Message)

let searchAddress (query: string) : Cmd<Msg> =
    let url =
        $"https://nominatim.openstreetmap.org/search?q={System.Uri.EscapeDataString query}&format=jsonv2"
    Cmd.OfPromise.either
        (fun () ->
            promise {
                let! response = Fetch.fetch url []
                let! text = response.text()
                match Decode.fromString (Decode.list NominatimResult.decoder) text with
                | Ok data -> return data
                | Error err -> return! Promise.reject err 
            })
        ()
        (fun data -> ListingSearchResult (Ok data))
        (fun ex -> ListingSearchResult (Error ex.Message))

// TODO load login settings from local storage
let defaultModel =
    { 
        page = Page.Login
        auth = LoggedOut
        loginEmail = Some "demo@example.com"
        loginPassword = Some "demo"
        loginError = None
        tree = None
        isLoading = true
        leftPanelState = BottomExpanded
        listingSearchModalHidden = true
        listingSearchQuery = None
        listingSearchResults = None
        listingSearchResultSelections = None
        listings = []
        selectedListingId = None
        sortState = ScoreDesc
        userPanelHidden = true
        tutorialState = Hidden
        tutorialCategories = Set.empty
        tutorialDistance = None
    }

let getSession () : JS.Promise<string option> =
    promise {
        let! result = Supabase.supabase?auth?getSession()
        let session = result?data?session
        match isNullOrUndefined session with
        | true -> return None
        | false ->
            let user = session?user
            match isNullOrUndefined user with
            | true -> return None
            | false -> return Some(user?email |> string)
    }

let detectPage =
    match window.location.pathname with
    | "/login" -> Page.Login
    | _ -> Page.Main

let navigateTo page =
    let path =
        match page with
        | Page.Main -> "/"
        | Page.Login -> "/login"
    window.history.pushState(null, "", path)
    page

let init () : Model * Cmd<Msg> =
    let page = detectPage
    let cmd =
        Cmd.OfPromise.either
            getSession
            ()
            (fun maybeEmail ->
                match maybeEmail with
                | Some email -> LoginResult (Ok email)
                | None -> LogoutResult
            )
            (fun ex -> LoginResult (Error ex.Message))
    { defaultModel with page = page }, cmd

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

let buildTutorialTree (categories: Set<Category>) (distance: float option) : TreeNode option =
    match Set.isEmpty categories with
    | true -> None
    | false ->
        let rootFlat: FlatNode = {
            id = 1
            parent_id = None
            lft = 0
            rgt = 0
            nodeType = NodeType.GROUP
            operator = Some "AND"
            category = None
            radius = None
        }
        
        let children =
            categories
            |> Seq.mapi (fun i cat ->
                let flat: FlatNode = {
                    id = i + 2
                    parent_id = Some 1
                    lft = 0
                    rgt = 0
                    nodeType = NodeType.TERM
                    operator = None
                    category = Some cat
                    radius = distance
                }
                { flat = flat; isExpanded = true; children = [] })
            |> Seq.toList
        Some { flat = rootFlat; isExpanded = true; children = children }

let update msg model : Model * Cmd<Msg> =
    match msg with
    | Navigate page ->
        match model.auth with
        | LoggedIn -> { model with page = navigateTo page }, Cmd.none
        | _ -> { model with page = navigateTo Page.Login }, Cmd.none
    | SetLoginEmail email -> { model with loginEmail = Some email }, Cmd.none
    | SetLoginPassword password -> { model with loginPassword = Some password }, Cmd.none
    | Register (email, password) ->
        let register (email, password) =
            Supabase.supabase?auth?signUp {| email = email; password = password |}
            |> unbox<JS.Promise<obj>>
        let cmd =
            Cmd.OfPromise.either
                register
                (email, password)
                (fun result ->
                    // TODO friendly username: https://supabase.com/docs/guides/auth/managing-user-data#adding-and-retrieving-user-metadata
                    let user = result?data?user
                    let error = result?error
                    match isNull user, isNull error with
                    | true, true -> failwith $"Unexpected reply: {result}"
                    | false, true -> LoginResult (Ok (user?email))
                    | _, false -> LoginResult (Error (error?message))
                )
                (fun ex -> LoginResult (Error ex.Message))
        { model with auth = Unknown; tutorialState = Landing }, cmd
    | Login (email, password) ->
        let signIn (email, password) =
            Supabase.supabase?auth?signInWithPassword {| email = email; password = password |}
            |> unbox<JS.Promise<obj>>
        let cmd =
            Cmd.OfPromise.either
                signIn
                (email, password)
                (fun result ->
                    let user = result?data?user
                    let error = result?error
                    match isNull user, isNull error with
                    | true, true -> failwith $"Unexpected reply: {result}"
                    | false, true -> LoginResult (Ok (user?email))
                    | _, false -> LoginResult (Error (error?message))
                )
                (fun ex -> LoginResult (Error ex.Message))
        { model with auth = Unknown }, cmd
    | Logout ->
        let signOut () =
            Supabase.supabase?auth?signOut()
            |> unbox<JS.Promise<obj>>
        let cmd =
            Cmd.OfPromise.perform
                signOut
                ()
                (fun _ -> LogoutResult)
        model, cmd
    | LoginResult (Ok email) -> { model with auth = LoggedIn; loginEmail = Some email; page = navigateTo Page.Main }, fetchTree
    | LoginResult (Error err) -> { model with auth = LoggedOut; loginError = Some err; page = navigateTo Page.Login }, Cmd.none
    | LogoutResult -> { defaultModel with page = navigateTo Page.Login }, Cmd.none
    | TutorialNext ->
        match model.tutorialState with
        | Hidden -> failwith "You shouldn't be able to do this..."
        | Landing -> { model with tutorialState = CategorySelect }, Cmd.none
        | CategorySelect -> { model with tutorialState = DistanceSelect }, Cmd.none
        | DistanceSelect ->
            match buildTutorialTree model.tutorialCategories model.tutorialDistance with
            | Some newRoot -> 
                match model.tree with
                | Some tree -> { model with tree = Some { tree with root = newRoot }; tutorialState = Hidden }, Cmd.ofMsg SaveTree
                | None ->
                    let tree: Tree = { id = -1; label = "Tutorial"; root = newRoot; lastModified = System.DateTime.UtcNow }
                    { model with tree = Some tree; tutorialState = Hidden }, Cmd.ofMsg SaveTree
            | None -> { model with tutorialState = Hidden }, Cmd.none
    | TutorialBack ->
        match model.tutorialState with
        | Hidden | Landing -> failwith "You shouldn't be able to do this..."
        | CategorySelect -> { model with tutorialState = Landing }, Cmd.none
        | DistanceSelect -> { model with tutorialState = CategorySelect }, Cmd.none
    | TutorialToggleCategorySelect cat ->
        let selected' =
            match model.tutorialCategories.Contains cat with
            | true -> model.tutorialCategories.Remove cat
            | false -> model.tutorialCategories.Add cat
        { model with tutorialCategories = selected' }, Cmd.none
    | TutorialToggleDistanceSelect d -> { model with tutorialDistance = Some d }, Cmd.none
    | TreeLoaded data ->
        let cmd =
            Cmd.OfPromise.either
                loadListings
                ()
                (fun listings -> ListingsLoaded listings)
                (fun ex -> ListingsLoadFailed ex.Message)
        match data with
        | Some tree  -> { model with tree = Some tree; isLoading = false }, cmd
        | None ->
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
            let root = TreeBuilder.fromFlatNodes [root]
            let tree: Tree = { id = -1; label = $"Saved search 1"; root = root; lastModified = System.DateTime.UtcNow }
            { model with tree = Some tree; isLoading = false }, cmd
    | SaveTree ->
        match model.tree with
        | Some tree ->
            let cmd =
                Cmd.OfPromise.either
                    (fun () -> repo.SaveTree tree)
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
    | ClearTree ->
        // TODO clear by tree ID to support "library"
        let cmd =
            Cmd.OfPromise.either
                (fun () -> repo.ClearTree())
                ()
                (function
                    | Success _ -> TreeCleared
                    | Failure err -> TreeClearFailed err)
                (fun ex -> TreeClearFailed ex.Message)
        model, cmd
    | TreeCleared -> model, fetchTree
    | TreeClearFailed msg ->
        printfn $"Tree clearing failed: {msg}"
        model, Cmd.none
    | ListingsLoaded listings ->
        // TODO un-scored listings flash briefly on the screen
        let cmd =
            Cmd.OfPromise.either
                loadAllPOIs              
                ()
                (fun points -> POIsLoaded points)
                (fun ex -> POILoadFailed ex.Message)
        match model.tree with
        | Some _ -> { model with listings = listings }, cmd
        | None -> model, Cmd.none
    | ListingsLoadFailed err -> 
        printfn "Failed to load listings: %s" err
        model, Cmd.none
    | ToggleListingSearchModal ->
        { model with listingSearchModalHidden = not model.listingSearchModalHidden }, Cmd.none
    | UpdateListingSearchQuery query ->
        { model with listingSearchQuery = Some query }, Cmd.none
    | RunListingSearchQuery ->
        match model.listingSearchQuery with
        | Some ""|None -> model, Cmd.none
        | Some q -> model, searchAddress q
    | ListingSearchResult (Ok res) ->
        { model with listingSearchResults = Some res }, Cmd.none
    | ListingSearchResult (Error err) ->
        printfn "Failed to search listings: %s" err
        model, Cmd.none
    | ListingSearchResultSelected res  ->
        match model.listingSearchResultSelections with
        | None -> { model with listingSearchResultSelections = Some [res] }, Cmd.none
        | Some v -> 
            match List.contains res v with
            | true -> { model with listingSearchResultSelections = Some (v |> List.filter ((<>) res)) }, Cmd.none
            | false -> { model with listingSearchResultSelections = Some (v @ [res]) }, Cmd.none
    | ListingSearchResultSelectionsSaved  ->
        match model.listingSearchResultSelections with
        | Some selections -> 
            let newListings =
                selections
                |> List.map (fun x ->
                    {
                        id = -1
                        address = x.display_name
                        lat = float x.lat
                        lon = float x.lon
                        price = -1
                        score = Some 5.0
                        source = Some "https://nominatim.openstreetmap.org"
                    })
            { model with listings = model.listings @ newListings; listingSearchModalHidden = true }, Cmd.none
        | None ->
            printfn "Nothing to save"
            model, Cmd.none
    | POIsLoaded poiList ->
        match model.tree with
        | Some tree ->
            { model with listings = model.listings |> List.map (fun l -> { l with score = Some (score tree.root poiList l) }) |> normalizeScores }, Cmd.none
        | None -> model, Cmd.none
    | POILoadFailed err ->
        printfn "Failed to load POIs: %s" err
        model, Cmd.none
    | Toggle id ->
        match model.tree with
        | Some tree ->
            let updatedRoot = toggleNode id tree.root
            { model with tree = Some { tree with root = updatedRoot } }, Cmd.none
        | None -> model, Cmd.none
    | UpdateLabel label ->
        match model.tree with
        | Some tree -> { model with tree = Some { tree with label = label } }, Cmd.none
        | None -> model, Cmd.none
    | AddTermChild parentId ->
        match model.tree with
        | Some tree ->
            let newId = TreeNode.findMaxId tree.root 0 + 1
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
            let updatedRoot = addChildNode parentId newTreeNode tree.root
            { model with tree = Some { tree with root = updatedRoot } }, Cmd.none
        | None -> model, Cmd.none
    | AddGroupChild parentId ->
        match model.tree with
        | Some tree ->
            let newId = TreeNode.findMaxId tree.root 0 + 1
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
            let updatedRoot = addChildNode parentId newTreeNode tree.root
            { model with tree = Some { tree with root = updatedRoot } }, Cmd.none
        | None -> model, Cmd.none
    | UpdateTermCategory (id, newCategory) ->
        match model.tree with
        | Some tree ->
            let updated = updateNode (fun flat ->
                match flat.nodeType with
                | NodeType.TERM -> { flat with category = Some newCategory }
                | _ -> flat) id tree.root
            { model with tree = Some { tree with root = updated } }, Cmd.none
        | None -> model, Cmd.none
    | UpdateTermRadius (id, newRadius) ->
        match model.tree with
        | Some tree ->
            let updated = updateNode (fun flat ->
                match flat.nodeType with
                | NodeType.TERM -> { flat with radius = Some newRadius }
                | _ -> flat) id tree.root
            { model with tree = Some { tree with root = updated } }, Cmd.none
        | None -> model, Cmd.none
    | UpdateGroupOperator (id, newOp) ->
        match model.tree with
        | Some tree ->
            let updated = updateNode (fun flat ->
                match flat.nodeType with
                | NodeType.GROUP -> { flat with operator = Some newOp }
                | _ -> flat) id tree.root
            { model with tree = Some { tree with root = updated } }, Cmd.none
        | None -> model, Cmd.none
    | DeleteNode targetId ->
        match model.tree with
        | Some tree ->
            // Prevent deletion of root
            if tree.root.flat.id = targetId then
                printfn "Cannot delete the root node."
                model, Cmd.none
            else
                match removeNode targetId tree.root with
                | Some updatedRoot ->
                    { model with tree = Some { tree with root = updatedRoot } }, Cmd.none
                | None ->
                    printfn "Unexpected: root was deleted"
                    model, Cmd.none
        | None -> model, Cmd.none
    | ToggleLeftPanels ->
        let newState =
            match model.leftPanelState with
            | BottomExpanded -> TopExpanded
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
    | ToggleModal ->
        match model.tutorialState with
        | Hidden -> { model with tutorialState = Landing }, Cmd.none 
        | _ -> { model with tutorialState = Hidden }, Cmd.none 
    | ToggleUserPanel -> { model with userPanelHidden = not model.userPanelHidden }, Cmd.none

// TODO wire up Supabase.supabase?auth?onAuthStateChange for cross-tab auth events
Program.mkProgram init update view
|> Program.withReactBatched "artemis-app"
|> Program.run
