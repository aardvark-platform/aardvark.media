namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify
open System.IO

type Message = 
    | SetContent of string
    | SetPath of string

[<ModelType>]
type Model = 
    {
        FilePath : Option<string>
        Watcher : Option<FileSystemWatcher>
        Content : string
    }