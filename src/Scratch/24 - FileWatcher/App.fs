module Inc.App
open System
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Inc.Model
open System.IO

let readFileNonCrashing path =
    use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    use r = new StreamReader(fs)
    r.ReadToEnd()

let update (model : Model) (msg : Message) =
    match msg with
        | SetPath p -> { model with FilePath = Some p; Content = readFileNonCrashing p }
        | SetContent c -> { model with Content = c }

let view (model : AdaptiveModel) =
    div [] [
        div [] [
            openDialogButton 
                    ({
                        mode            = OpenDialogMode.File
                        title           = "Choose watched text file"
                        startPath       = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        filters         = [| "*.*" |]
                        allowMultiple   = false
                    }) [onChooseFile SetPath] [text "choose file"]
        ]
        div [] [Incremental.text (model.FilePath |> AVal.map (sprintf "%A"))]
        div [] [Incremental.text model.Content]
    ]

let watcher (path : string) cb =
    let dir = Path.GetDirectoryName path
    let file = Path.GetFileName path
    let w = new FileSystemWatcher(dir)
    w.NotifyFilter <- NotifyFilters.LastWrite
    w.Filter <- file
    w.EnableRaisingEvents <- true
    w.Changed.Add cb
    w

let watcherThread path =
    let mvar = MVar.create ""
    let cb evt = 
        let filecontent = readFileNonCrashing path
        MVar.put mvar filecontent

    // schaut iwie sheiße aus
    ignore (watcher path cb)

    let rec puller() =
        proclist {
            do! Async.SwitchToThreadPool() // das muss hier stehen sonst blockt alles

            yield SetContent (MVar.take mvar)
            yield! puller()
        }

    puller()

let threads (model : Model) = 
    match model.FilePath with
    | Some path -> 
        ThreadPool.empty
        |> ThreadPool.add "puller" (watcherThread path)
    | None -> ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            {   
                FilePath = None
                Content = "content"
            }
        update = update 
        view = view
    }
