namespace Tree

open DomainTypes

module Criteria =
    let buildTree (rows: CriterionRow list) : CriteriaNode =
        let rec buildFrom rows parentRgt =
            match rows with
            | [] -> ([], [])
            | row :: rest when row.Lft >= parentRgt -> ([], rows)
            | row :: rest ->
                match row.Operator with
                | None ->
                    let node = TermNode(row.Id, enum<Category>(row.CategoryId.Value), row.DistAmt.Value)
                    let (siblings, remaining) = buildFrom rest parentRgt
                    (node :: siblings, remaining)
                | Some op ->
                    let (children, afterChildren) = buildFrom rest row.Rgt
                    let node = GroupNode(row.Id, enum<OperatorType>(op), children)
                    let (siblings, remaining) = buildFrom afterChildren parentRgt
                    (node :: siblings, remaining)
        match rows with
        | [] -> failwith "No rows"
        | root :: rest ->
            match root.Operator with
            | None -> failwith "Root must be a group node"
            | Some op ->
                let (children, _) = buildFrom rest root.Rgt
                GroupNode(root.Id, enum<OperatorType>(op), children)

    let rec printTree indent node =
        match node with
        | GroupNode(id, op, children) ->
            printfn "%s%s (id: %d)" indent (if op = OperatorType.And then "AND" else "OR") id
            children |> List.iter (printTree (indent + "  "))
        | TermNode(id, cat, dist) ->
            printfn "%s%A < %.3f (id: %d)" indent cat dist id

// TODO move this to a more appropriate namespace?
module Score =
    let toRows (evalMode: EvalOption) (locationId: int) (root: Score) : ScoreRow list =
        let rec loop (node: Score) =
            let id =
                match node.Node with
                | GroupNode(id,_,_)
                | TermNode(id,_,_) -> id
            let keyPoiId =
                node.KeyPoi |> Option.map (fun p -> p.Id) |> Option.defaultValue 0
            let row = {
                Id = 0
                EvalMode = EvalOption.toStr evalMode
                LocationId = locationId
                CriterionId = id
                KeyPoiId = keyPoiId
                Raw = node.Raw
                Normalized = node.Normalized
            }
            row :: (node.Children |> List.collect loop)
        loop root