module Logger

open System

let private sw = Diagnostics.Stopwatch.StartNew()

let log fmt =
    Printf.kprintf (fun msg -> printfn "[%s] %s" (sw.Elapsed.ToString "mm\:ss\.ff") msg) fmt
