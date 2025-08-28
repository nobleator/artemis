module View

open Feliz
open Fable.Core
open Fable.Core.JsInterop
open Browser.Types
open DomainTypes

[<Emit("APP_VERSION")>]
let appVersion : string = jsNative

let leaflet: obj = importAll "leaflet"

let formatCurrency (amount: float) (locale: string) (currency: string) : string =
    let ctor: obj = emitJsExpr () "Intl.NumberFormat"
    let formatter = emitJsExpr (ctor, locale, {| style = "currency"; currency = currency |}) "new ($0)($1, $2)"
    emitJsExpr (formatter, amount) "$0.format($1)"

let renderNodeHeader (node: TreeNode) dispatch =
    match node.flat.nodeType with
    | NodeType.GROUP ->
        Html.div [
            Html.text "GROUP: "
            Html.select [
                prop.value (defaultArg node.flat.operator "AND")
                prop.onChange (fun v -> dispatch (UpdateGroupOperator (node.flat.id, v)))
                prop.children [
                    Html.option [ prop.value "AND"; prop.text "AND" ]
                    Html.option [ prop.value "OR"; prop.text "OR" ]
                ]
            ]
        ]
    | NodeType.TERM ->
        Html.div [
            Html.select [
                prop.value (node.flat.category |> Option.map Category.name |> Option.defaultValue "")
                prop.onChange (fun (v: string) ->
                    match Category.ofName v with
                    | Some c -> dispatch (UpdateTermCategory (node.flat.id, c))
                    | None -> ())
                prop.children [
                    for c in Category.all ->
                        Html.option [
                            prop.value (Category.name c)
                            prop.text (Category.label c)
                        ]
                ]
            ]
            // Html.input [
            //     prop.value (defaultArg node.flat.category "")
            //     prop.onChange (fun v -> dispatch (UpdateTermCategory (node.flat.id, v)))
            //     prop.placeholder "Category"
            //     prop.style [ style.marginRight 6 ]
            // ]
            Html.input [
                prop.type' "number"
                prop.step 0.5
                prop.value (node.flat.radius |> Option.map string |> Option.defaultValue "")
                prop.onChange (fun (v: string) ->
                    match System.Double.TryParse v with
                    | true, i -> dispatch (UpdateTermRadius (node.flat.id, i))
                    | _ -> ())
                prop.placeholder "Radius"
                prop.style [ style.width 60; style.marginRight 6 ]
            ]
            Html.span "km(s)"
        ]
    | _ -> failwith "(invalid nodeType)"

let rec renderNode (node: TreeNode) dispatch =
    Html.div [
        Html.div [
            // prop.key($"{node.flat.id}-header")
            prop.className "node-header-container"
            prop.children [
                if node.flat.nodeType = NodeType.GROUP then
                    Html.span [
                        prop.className "node-expander"
                        prop.text (if node.isExpanded then "▼" else "▶")
                        prop.onClick (fun _ -> dispatch (Toggle node.flat.id))
                    ]
                Html.span [
                    prop.className "node-header"
                    prop.children [ renderNodeHeader node dispatch ]
                ]
                if node.flat.nodeType = NodeType.GROUP then
                    Html.button [
                        prop.text "+TERM"
                        prop.onClick (fun _ -> dispatch (AddTermChild node.flat.id))
                    ]
                    Html.button [
                        prop.text "+GROUP"
                        prop.onClick (fun _ -> dispatch (AddGroupChild node.flat.id))
                    ]
                if node.flat.id <> 0 then //root node
                    Html.button [
                    prop.text "×"
                    prop.onClick (fun _ -> dispatch (DeleteNode node.flat.id))
                ]
            ]
        ]
        if node.isExpanded then
            Html.div [
                // prop.key($"{node.flat.id}-children")
                prop.className "node-child"
                prop.children (
                    node.children
                    |> List.map (fun child ->
                        Html.div [
                            // prop.key error will hopefully be resolved in a future Feliz update: https://github.com/fable-hub/Feliz/issues/652
                            // prop.key (string child.flat.id)
                            prop.children [ renderNode child dispatch ]
                        ])
                )
            ]
    ]

let leafletView  =
    React.functionComponent(fun (input: {| listings: ListingCard list; selectedId: int option; onMarkerClick: int -> unit; |}) ->
        let mapRef = React.useRef<HTMLDivElement option> None
        let mapInstance = React.useRef<obj option> None
        let markersRef = React.useRef<Map<int, obj>> Map.empty
        // TODO: heatmap

        // Map initialization
        React.useEffectOnce(fun () ->
            match mapRef.current with
            | Some el ->
                let map = leaflet?map el
                // TODO set starting point from user region
                map?setView(createObj [ "lat" ==> 40.735; "lng" ==> -73.994; ], 13)
                // TODO use map bounds to determine POI load
                // let bounds = map?getBounds()
                // let southWest = bounds?getSouthWest()
                // let northEast = bounds?getNorthEast()

                // let swLat = southWest?lat
                // let swLng = southWest?lng
                // let neLat = northEast?lat
                // let neLng = northEast?lng
                // printfn "(%f, %f, %f, %f)" swLat swLng neLat neLng
                
                let tileLayer =
                    leaflet?tileLayer(
                        "https://tiles.stadiamaps.com/tiles/alidade_smooth/{z}/{x}/{y}{r}.{ext}",
                        createObj [
                            "attribution" ==> "&copy; <a href='https://www.stadiamaps.com/' target='_blank'>Stadia Maps</a> &copy; <a href='https://www.openstreetmap.org/copyright'>OpenStreetMap</a> contributors";
                            "minZoom" ==> 0;
                            "maxZoom" ==> 19;
                            "ext" ==> "png";
                        ]
                    )
                tileLayer?addTo map |> ignore
                mapInstance.current <- Some map
            | None -> ()
        )
        // Update markers
        // TODO: layer group for markers?
        React.useEffect(
            (fun () ->
                match mapInstance.current with
                | Some map ->
                    markersRef.current
                    |> Map.iter (fun _ marker -> marker?remove() |> ignore)

                    let newMarkers =
                        input.listings
                        |> List.map (fun listing ->
                            let offsetPoint = leaflet?point(-15, -10)
                            let marker =
                                leaflet?marker(createObj [ "lat" ==> listing.lat; "lng" ==> listing.lon; ])
                            marker?bindPopup listing.address |> ignore
                            match listing.score with
                            | Some v -> 
                                marker?bindTooltip($"%.2f{v}", createObj [ 
                                    "permanent" ==> true
                                    "direction" ==> "top"
                                    "offset" ==> offsetPoint
                                    "className" ==> "score-tooltip"
                                    ]) |> ignore
                            | _ -> ()
                            marker?on("click", fun _ -> input.onMarkerClick listing.id) |> ignore
                            marker?addTo map |> ignore
                            listing.id, marker
                        )
                        |> Map.ofList
                    markersRef.current <- newMarkers
                | None -> ()
            ),
            [| box input.listings |]
        )
        // React to selected ID change to highlight pins & cards
        React.useEffect(
            (fun () ->
                match input.selectedId, markersRef.current.TryFind(input.selectedId |> Option.defaultValue -1) with
                | Some _, Some marker ->
                    let latlng = marker?getLatLng()
                    match mapInstance.current with
                    | Some map ->
                        map?panTo(latlng) |> ignore
                        marker?openPopup() |> ignore
                    | None -> ()
                | _ -> ()
                ),
            [| box input.selectedId |]
        )
        Html.div [
            prop.ref mapRef
            prop.className "map"
        ]
    )

let renderListings (listings: ListingCard list) (selectedId: int option) (selectCallback: int -> unit) (sortState: SortState) =
    Html.div [
        match sortState with
        | ScoreDesc -> listings |> List.sortByDescending (fun listing -> listing.score)
        | ScoreAsc -> listings |> List.sortBy (fun listing -> listing.score)
        | PriceDesc -> listings |> List.sortByDescending (fun listing -> listing.price)
        | PriceAsc -> listings |> List.sortBy (fun listing -> listing.price)
        |> List.map (fun listing ->
            Html.div [
                prop.className (
                    "listing-card"
                    + if Some listing.id = selectedId then " selected" else ""
                )
                prop.onClick (fun _ -> selectCallback listing.id)
                prop.children [
                    let formatted = formatCurrency listing.price  "en-US" "USD"
                    match listing.score with
                    | Some score ->
                        Html.p [
                            prop.className "listing-card-score"
                            prop.title "Score"
                            prop.text $"%0.2f{score}"
                        ]
                    | None -> Html.p ""
                    Html.span [
                        prop.className "listing-card-details"
                        prop.title "Address"
                        prop.children [
                            Html.p listing.address
                            Html.p [
                                prop.className "listing-card-sub"
                                prop.text $"({listing.lat}, {listing.lon})"
                            ]
                        ]
                    ]
                    Html.p [
                        prop.className "listing-card-price"
                        prop.title "Price"
                        prop.text formatted
                    ]
                ]
            ])
        |> prop.children
    ]

let renderTutorial model dispatch =
    Html.div [
        prop.className "modal-overlay"
        prop.children [
            Html.div [
                prop.className "modal-content"
                prop.children [
                    Html.button [
                        prop.className "modal-close-button"
                        prop.text "×"
                        prop.onClick (fun _ -> dispatch ToggleModal)
                    ]
                    match model.tutorialState with
                    | Hidden -> ()
                    | Landing ->
                        Html.div [
                            prop.children [
                                Html.p "Welcome to"
                                Html.h4 "Artemis"
                                Html.p "Identify your perfect home by precisely specifying your personal preferences."
                                Html.button [
                                    prop.text "Begin"
                                    prop.onClick (fun _ -> dispatch TutorialNext)
                                ]
                            ]
                        ] 
                    | CategorySelect ->
                        Html.div [
                            prop.children [
                                Html.div [
                                    Html.p "Select the places you would like to live close by:"
                                ]
                                Html.div [
                                    yield! Category.all
                                    |> List.map (fun c ->
                                        Html.button [
                                            prop.text (Category.label c)
                                            prop.className (
                                                "modal-select-button" + if model.tutorialCategories.Contains c then " selected" else ""
                                            )
                                            prop.onClick (fun _ -> dispatch (TutorialToggleCategorySelect c))
                                        ]
                                    )
                                ]
                                Html.div [
                                    Html.button [
                                        prop.text "Back"
                                        prop.onClick (fun _ -> dispatch TutorialBack)
                                    ]
                                    Html.button [
                                        prop.text "Next"
                                        prop.onClick (fun _ -> dispatch TutorialNext)
                                    ]
                                ]
                            ]
                        ]
                    | DistanceSelect ->
                        Html.div [
                            prop.children [
                                Html.div [
                                    Html.p "How close would you want to be?"
                                ]
                                Html.div [
                                    Html.button [
                                        prop.text "< 1 kms"
                                        prop.className (
                                            "modal-select-button" + if model.tutorialDistance = Some 1.0 then " selected" else ""
                                        )
                                        prop.onClick (fun _ -> dispatch (TutorialToggleDistanceSelect 1.0))
                                    ]
                                    Html.button [
                                        prop.text "< 3 kms"
                                        prop.className (
                                            "modal-select-button" + if model.tutorialDistance = Some 3.0 then " selected" else ""
                                        )
                                        prop.onClick (fun _ -> dispatch (TutorialToggleDistanceSelect 3.0))
                                    ]
                                    Html.button [
                                        prop.text "< 5 kms"
                                        prop.className (
                                            "modal-select-button" + if model.tutorialDistance = Some 5.0 then " selected" else ""
                                        )
                                        prop.onClick (fun _ -> dispatch (TutorialToggleDistanceSelect 5.0))
                                    ]
                                ]
                                Html.button [
                                    prop.text "Back"
                                    prop.onClick (fun _ -> dispatch TutorialBack)
                                ]
                                Html.button [
                                    prop.text "Next"
                                    prop.onClick (fun _ -> dispatch TutorialNext)
                                ]
                            ]
                        ]
                ]
            ]
        ]
    ]

let renderUserPanel model dispatch =
    Html.div [
        prop.className "user-panel-container"
        prop.children [
            Html.button [
                prop.className "user-panel-toggle"
                prop.text (defaultArg model.loginEmail "User")
                prop.onClick (fun _ -> dispatch ToggleUserPanel)
            ]
            if not model.userPanelHidden then
                Html.div [
                    prop.className "user-panel-content"
                    prop.children [
                        Html.div [
                            prop.text $"Build version: {appVersion}"
                            prop.className "user-panel-version"
                        ]
                        Html.button [
                            prop.text "Logout"
                            prop.onClick (fun _ -> dispatch Logout)
                        ]
                    ]
                ]
        ]
    ]

let renderCriteriaPanel model dispatch =
    Html.div [
        prop.className "panel top-panel"
        prop.children [
            Html.div [
                prop.className "panel-header"
                prop.children [
                    Html.span "Criteria"
                    Html.button [
                        prop.className "toggle-button"
                        prop.text (
                            match model.leftPanelState with
                            | TopExpanded -> "Collapse"
                            | _ -> "Expand"
                        )
                        prop.onClick (fun _ -> dispatch ToggleTopPanel)
                    ]
                ]
            ]
            if model.leftPanelState = Both || model.leftPanelState = TopExpanded then
                Html.div [
                    prop.className "panel-content"
                    prop.children [
                        match model.root with
                        | None ->
                            Html.div [
                                prop.text "Loading..."
                                prop.className "loading-text"
                            ]
                        | Some root ->
                            Html.button [
                                prop.text "Save"
                                prop.onClick (fun _ -> dispatch SaveTree)
                                prop.className "save-button"
                            ]
                            renderNode root dispatch
                    ]
                ]
        ]
    ]

let renderListingsPanel model dispatch =
    Html.div [
        prop.className "panel bottom-panel"
        prop.children [
            Html.div [
                prop.className "panel-header"
                prop.children [
                    Html.span "Listings"
                    Html.button [
                        prop.className "toggle-button"
                        prop.text (
                            match model.leftPanelState with
                            | BottomExpanded -> "Collapse"
                            | _ -> "Expand"
                        )
                        prop.onClick (fun _ -> dispatch ToggleBottomPanel)
                    ]
                ]
            ]
            if model.leftPanelState = Both || model.leftPanelState = BottomExpanded then
                Html.div [
                    prop.className "panel-content"
                    prop.children [
                        Html.div [
                            prop.className "panel-content-subheader"
                            prop.children [
                                Html.button [
                                    match model.sortState with
                                    | ScoreDesc -> "↓ Score"
                                    | ScoreAsc -> "↑ Score"
                                    | PriceDesc -> "↓ Price"
                                    | PriceAsc -> "↑ Price"
                                    |> prop.text
                                    prop.onClick (fun _ -> dispatch ToggleSort)
                                ]
                            ]
                        ]
                        Html.div [
                            prop.children [
                                renderListings model.listings model.selectedListingId (fun id -> dispatch (SelectListing id)) model.sortState
                            ]
                        ]
                    ]
                ]
        ]
    ]

let renderMapPanel model dispatch =
    Html.div [
        prop.className "panel right-panel"
        prop.children [
            Html.div [
                prop.className "panel-content"
                prop.children [
                    leafletView {|
                        listings = model.listings
                        selectedId = model.selectedListingId
                        onMarkerClick = fun id -> dispatch (MarkerClicked id)
                    |}
                ]
            ]
        ]
    ]

let renderHeader model dispatch =
    Html.h1 [
        prop.className "title-header"
        prop.children [
            Html.span [
                prop.className "title-header-content"
                prop.children [
                    Html.span [ 
                        prop.className "title-text"
                        prop.text "Artemis"
                    ]
                    Html.button [
                        prop.className "info-button"
                        prop.text "ⓘ"
                        prop.onClick (fun _ -> dispatch ToggleModal)
                    ]
                ]
            ]
        ]
    ]

let renderLogin model dispatch =
    // TODO additional email validation
    let canLogin =
        match model.loginEmail, model.loginPassword with
        | Some email, Some password when email <> "" && password <> "" -> true
        | _ -> false
    Html.div [
        prop.className "login-container"
        prop.children [
            Html.div [
                prop.className "panel login-panel"
                prop.children [
                    Html.div [
                        prop.className "panel-header"
                        prop.text "Login"
                    ]
                    Html.form [
                        prop.className "panel-content login-content"
                        prop.onSubmit (fun e ->
                            e.preventDefault()
                            match model.loginEmail, model.loginPassword with
                            | Some email, Some password -> dispatch (Login (email, password))
                            | _ -> Browser.Dom.window.alert "Please enter an email and password."
                        )
                        prop.children [
                            Html.input [
                                prop.placeholder "Email"
                                prop.value (defaultArg model.loginEmail "")
                                prop.onChange (fun e -> dispatch (SetLoginEmail e))
                            ]
                            Html.input [
                                prop.placeholder "Password"
                                prop.value (defaultArg model.loginPassword "")
                                prop.type' "password"
                                prop.onChange (fun e -> dispatch (SetLoginPassword e))
                            ]
                            Html.div [
                                prop.className "login-buttons"
                                prop.children [
                                    // TODO password reset
                                    Html.button [
                                        prop.text "Register"
                                        prop.disabled (not canLogin)
                                        prop.onClick (fun _ ->
                                            match model.loginEmail, model.loginPassword with
                                            | Some email, Some password -> dispatch (Register (email, password))
                                            | _ -> Browser.Dom.window.alert "Please enter an email and password.")
                                    ]
                                    Html.button [
                                        prop.text "Login"
                                        prop.disabled (not canLogin)
                                        prop.type' "submit"
                                    ]
                                ]
                            ]
                            match model.loginError with
                            | Some msg ->
                                Html.p [
                                    prop.className "login-error"
                                    prop.text msg
                                ]
                            | None -> ()
                        ]
                    ]
                ]
            ]
        ]
    ]

let view model dispatch =
    match model.auth with
    | LoggedOut | Unknown -> renderLogin model dispatch
    | LoggedIn ->
        React.fragment [
            if model.tutorialState <> Hidden then
                renderTutorial model dispatch
            renderUserPanel model dispatch
            // TODO shrink and/or remove title header
            // TODO move info button to user settings panel?
            renderHeader model dispatch
            Html.div [
                prop.className "main-layout"
                prop.children [
                    // Left column with two stacked panels, class depends on leftPanelState
                    Html.div [
                        prop.className (
                            match model.leftPanelState with
                            | Both -> "left-panel"
                            | TopExpanded -> "left-panel top-expanded"
                            | BottomExpanded -> "left-panel bottom-expanded"
                        )
                        prop.children [
                            // Top-left panel (Criteria)
                            renderCriteriaPanel model dispatch
                            // Bottom-left panel (Listings)
                            renderListingsPanel model dispatch
                        ]
                    ]
                    // Right panel (Map)
                    renderMapPanel model dispatch
                ]
            ]
        ]
