module Data

open Thoth.Json
open Fable.Core
open Fable.Core.JsInterop
open DomainTypes

let [<Literal>] dbName = "artemis-db"
let [<Literal>] storeName = "tree"

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
    // abstract Sync : unit -> JS.Promise<StorageResult<unit>>

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
