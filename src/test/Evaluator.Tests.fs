module Evaluator.Tests

open Xunit
open Evaluator
open DomainTypes

// ------------ add and max tests ------------

[<Fact>]
let ``add returns sum`` () =
    let result = add 3.0 4.5
    Assert.Equal(7.5, result, 3)  // allow tiny floating-point tolerance

[<Fact>]
let ``max returns larger value`` () =
    let result = max 10.0 7.0
    Assert.Equal(10.0, result, 3)

[<Fact>]
let ``max returns second when larger`` () =
    let result = max 1.0 9.0
    Assert.Equal(9.0, result, 3)

// ------------ isWithin tests ------------

[<Fact>]
let ``isWithin returns true for very close points`` () =
    let lat, lon = 51.5007, -0.1246    // London
    let lat2, lon2 = 51.5010, -0.1250  // ~50 meters away
    isWithin lat lon lat2 lon2 0.2
    |> Assert.True

[<Fact>]
let ``isWithin returns false for far points`` () =
    let londonLat, londonLon = 51.5007, -0.1246
    let nyLat, nyLon = 40.7128, -74.0060
    isWithin londonLat londonLon nyLat nyLon 1000.0
    |> Assert.False

// ------------ normalizeScores tests ------------

[<Fact>]
let ``normalizeScores does nothing when no scores`` () =
    let items = 
        [ { id = 1; address = "A"; lat=0.0; lng=0.0; price=100; score=None }
          { id = 2; address = "B"; lat=0.0; lng=0.0; price=200; score=None } ]
    let result = normalizeScores items
    Assert.Equal<obj list>(items |> List.map box, result |> List.map box)

[<Fact>]
let ``normalizeScores sets all to midpoint when scores equal`` () =
    let items = 
        [ { id=1; address="A"; lat=0.0; lng=0.0; price=100; score=Some 5.0 }
          { id=2; address="B"; lat=0.0; lng=0.0; price=200; score=Some 5.0 } ]
    let result = normalizeScores items
    result |> List.iter (fun i -> Assert.Equal(5.0, i.score.Value, 3))

[<Fact>]
let ``normalizeScores rescales scores into 0 to 10 range`` () =
    let items = 
        [ { id=1; address="A"; lat=0.0; lng=0.0; price=100; score=Some 20.0 }
          { id=2; address="B"; lat=0.0; lng=0.0; price=200; score=Some 30.0 } ]
    let result = normalizeScores items
    let scores = result |> List.choose (fun i -> i.score)
    Assert.Equal(0.0, List.min scores, 3)
    Assert.Equal(10.0, List.max scores, 3)

// ------------ score tests ------------

let mkFlatTerm id cat radius =
    { id=id; parent_id=None; lft=0; rgt=0; nodeType=NodeType.TERM; 
      operator=None; category=Some cat; radius=Some radius }

let mkFlatGroup id op =
    { id=id; parent_id=None; lft=0; rgt=0; nodeType=NodeType.GROUP;
      operator=Some op; category=None; radius=None }

let mkListing id lat lng = 
    { id=id; address="X"; lat=lat; lng=lng; price=100; score=None }

let mkPOI cat lat lng =
    { category=cat; source="src"; source_xref="xref"; latitude=lat; longitude=lng }

[<Fact>]
let ``score TERM counts POIs within radius`` () =
    let listing = mkListing 1 51.5 -0.1
    let poiList = [ mkPOI CoffeeShop 51.5001 -0.1001; mkPOI Airport 40.0 10.0 ]
    let termNode = { flat = mkFlatTerm 1 CoffeeShop 1.0; children = []; isExpanded = true }
    let result = score termNode poiList listing
    Assert.Equal(1.0, result, 3)

[<Fact>]
let ``score GROUP AND sums child scores`` () =
    let listing = mkListing 1 51.5 -0.1
    let poiList = [ mkPOI CoffeeShop 51.5 -0.1 ]
    let child1 = { flat = mkFlatTerm 2 CoffeeShop 1.0; children=[]; isExpanded=true }
    let child2 = { flat = mkFlatTerm 3 CoffeeShop 1.0; children=[]; isExpanded=true }
    let groupNode = { flat = mkFlatGroup 1 "AND"; children=[child1;child2]; isExpanded=true }
    let result = score groupNode poiList listing
    Assert.True(result >= 1.0)   // each child will return at least 1

[<Fact>]
let ``score GROUP OR takes max child score`` () =
    let listing = mkListing 1 51.5 -0.1
    let poiList = [ mkPOI CoffeeShop 51.5 -0.1 ]
    let child1 = { flat = mkFlatTerm 2 CoffeeShop 1.0; children=[]; isExpanded=true }
    let child2 = { flat = mkFlatTerm 3 Airport 1.0; children=[]; isExpanded=true }
    let groupNode = { flat = mkFlatGroup 1 "OR"; children=[child1;child2]; isExpanded=true }
    let result = score groupNode poiList listing
    Assert.Equal(1.0, result, 3)
