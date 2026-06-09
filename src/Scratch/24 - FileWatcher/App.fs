module Inc.App
open System
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Inc.Model
open System.IO

let readFileNonCrashing path =
    use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    use r = new StreamReader(fs)
    r.ReadToEnd()

let watcher (path : string) cb =
    let dir = Path.GetDirectoryName path
    let file = Path.GetFileName path
    let w = new FileSystemWatcher(dir)
    w.NotifyFilter <- NotifyFilters.LastWrite
    w.Filter <- file
    w.EnableRaisingEvents <- true
    w.Changed.Add cb
    w

let update (send : Message -> unit) (model : Model) (msg : Message) =
    match msg with
        | SetPath p -> 
            match model.Watcher with | Some w -> w.Dispose() | None -> ()
            let watcher = watcher p (fun _ -> readFileNonCrashing p |> SetContent |> send)
            let content = readFileNonCrashing p
            { model with 
                FilePath = Some p
                Content = content
                Watcher = Some watcher
            }
        | SetContent c -> { model with Content = c }

let view (model : AdaptiveModel) =
    div [] [
        div [] [
            Dialog.openFileButton SetPath
                { DialogConfig.Default with
                    Title           = "Choose watched text file"
                    Filters         = [ "Text files", ["txt"; "md"; "fs"; "cs"; "cpp"] ]
                    DefaultPath     = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }
                [] [text "choose file"]
        ]
        div [] [Incremental.text (model.FilePath |> AVal.map (sprintf "%A"))]
        div [] [Incremental.text model.Content]
    ]


let app (send : Message -> unit) =
    {
        unpersist = Unpersist.instance     
        threads = fun _ -> ThreadPool.empty 
        initial = 
            {   
                FilePath = None
                Watcher = None
                Content = "content"
            }
        update = update send
        view = view
    }
