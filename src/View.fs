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
                map?setView(createObj [ "lat" ==> 51.505; "lng" ==> -0.09; ], 13)
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
                                leaflet?marker(createObj [ "lat" ==> listing.lat; "lng" ==> listing.lng; ])
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
                                prop.text $"({listing.lat}, {listing.lng})"
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

let view model dispatch =
    React.fragment [
        Html.span [
            prop.className "app-version-badge"
            prop.text appVersion
        ]
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
        if not model.modalHidden then
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
                            Html.p "Welcome to Artemis, your partner in house hunting. With this platform you are able to precisely specify your personal preferences to easily identify the perfect home for you."
                            Html.p "The app is separated into 3 segments: 1) criteria input in the top left, 2) listings in the bottom left, and 3) a map on the right. You customize your preferences by adding individual categories as “TERMS” which are combined into any number of “GROUPS” with “AND” or “OR” operators. Terms define the individual requirements for your personal criteria, such as proximity to a library, park, or school. Once your criteria have been saved, points of interest (POI) are loaded corresponding to the categories you entered, and a score is calculated to determine how many POI are within the specified radius. This score is normalized to a 0-10 range, so the listing with the absolute best score will always be 10. The list of homes is automatically sorted by score, but you can toggle other sort options as desired."
                        ]
                    ]
                ]
            ]
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
                        // Bottom-left panel (Listings)
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
                    ]
                ]
                // Right panel (Map)
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
            ]
        ]
    ]
