module Supabase

open Fable.Core.JsInterop

let supabaseLib: obj = importAll "@supabase/supabase-js"

let createClient (url: string) (key: string): obj =
    supabaseLib?createClient(url, key)

// TODO move to .env
// Note: these are anonymous keys meant to be embedded client-side
let supabase: obj =
    createClient
        "https://tzvrfpebyxlbyekiffak.supabase.co"
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InR6dnJmcGVieXhsYnlla2lmZmFrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTYxMjQ1MzEsImV4cCI6MjA3MTcwMDUzMX0.XcA7QJibH_doAQAxugdm-Eo66KiXkulwvLopfM51pKU"
