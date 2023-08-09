open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open VirtualTreeExample
open VirtualTree.Utilities
open FSharp.Data.Adaptive

open Suave
open Suave.WebPart

[<EntryPoint; STAThread>]
let main argv =

    //let children =
    //    HashMap.ofList [
    //        1, [2; 3]
    //        2, [4; 5]
    //        4, [8; 9; 10]
    //        9, [6; 7]
    //    ]

    //let getChildren (n : int) =
    //    match children.TryFindV n with
    //    | ValueSome c -> c
    //    | _ -> []

    //let tree = FlatTree.ofHierarchy getChildren 1
    //Log.line "%A" tree
    //Log.line "Count: %A" (FlatTree.count tree)
    //Log.line "Path from 10: %A" (tree |> FlatTree.rootPath 10)
    //Log.line "Parent of 10: %A" (tree |> FlatTree.parent 10)

    //let children =
    //    HashMap.ofList [
    //        11, [12; 13; 15]
    //        13, [14]
    //        15, [16]
    //    ]

    //let getChildren (n : int) =
    //    match children.TryFindV n with
    //    | ValueSome c -> c
    //    | _ -> []

    //let repl =
    //    FlatTree.ofHierarchy getChildren 11
    //    |> FlatTree.delete 15

    //Log.line "\n\nReplace 9 with %A" repl
    //let tree = tree |> FlatTree.replace 9 repl

    //Log.line "%A" tree
    //Log.line "Count: %A" (FlatTree.count tree)
    //Log.line "Path from 14: %A" (tree |> FlatTree.rootPath 14)
    //Log.line "Parent of 13: %A" (tree |> FlatTree.parent 13)


    ////Log.line "Delete 9"

    ////let cut = tree |> FlatTree.delete 9
    ////Log.line "Deleted: %A" cut
    ////Log.line "%A" (cut |> FlatTree.rootPath 10)
    ////Log.line "%A" (cut |> FlatTree.rootPath 8)

    //Environment.Exit 0

    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()
    let instance = App.app |> App.start

    // use can use whatever suave server to start you mutable app.
    // startServerLocalhost is one of the convinience functions which sets up
    // a server without much boilerplate.
    // there is also WebPart.startServer and WebPart.runServer.
    // look at their implementation here: https://github.com/aardvark-platform/aardvark.media/blob/master/src/Aardvark.Service/Suave.fs#L10
    // if you are unhappy with them, you can always use your own server config.
    // the localhost variant does not require to allow the port through your firewall.
    // the non localhost variant runs in 127.0.0.1 which enables remote acces (e.g. via your mobile phone)
    WebPart.startServerLocalhost 4321 [
        MutableApp.toWebPart' app.Runtime false instance
        Reflection.assemblyWebPart <| Reflection.Assembly.GetEntryAssembly()
        Suave.Files.browseHome
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }

    //use ctrl = new AardvarkCefBrowser()
    //ctrl.Dock <- DockStyle.Fill
    //form.Controls.Add ctrl
    //ctrl.StartUrl <- "http://localhost:4321/"
    //ctrl.ShowDevTools()
    //form.Text <- "Examples"
    //form.Icon <- Icons.aardvark

    //Application.Run form
    0
