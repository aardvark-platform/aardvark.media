namespace Test

open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Application.Slim
open FSharp.Data.Adaptive
open Test.UI
open Test.Dashboard

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ServerApp =

    let update win (m : ServerApp) (msg : ServerAction) =
        match msg with
        | DashboardMessage (guid, msg) ->
            m
    let view  win inServerMode dataFolder glApp  (model : AdaptiveServerApp)  =

        div [] [
            subApp' 
                (fun _model _innermsg -> 
                    Seq.singleton (DashboardMessage (_model.clientId, _innermsg))
                ) 
                (fun _model msg ->
                    Log.line "[ServerApp] Message: %s" (string msg)
                    //Seq.empty
                    match msg with
                        | DashboardMessage (guid, msg) -> 
                        match msg with
                            | DashboardAction.SetClientSession str -> 
                                Seq.empty
                            | DashboardAction.IncMessage (viewId, viewMsg) -> 
                                Log.warn "Subapp %s View %s: %s" guid viewId (string msg)
                                Seq.empty
                                //Log.warn "%s = %s" guid _model.clientId
                                //if (guid = _model.clientId) then
                                //    Log.warn "Subapp %s View %s: %s" guid viewId (string msg) 
                                //    Seq.singleton msg
                                //else Seq.empty
                            | _ ->
                                Seq.empty
                ) 
                [] 
                (Dashboard.app win inServerMode dataFolder glApp (System.Guid.NewGuid () |> string))
        ]

    let app (win : Aardvark.Glfw.Window) inServerMode
            dataFolder (glApp : OpenGlApplication) : App<ServerApp, AdaptiveServerApp, ServerAction> =   

        {
            unpersist = Unpersist.instance
            threads = (fun _ -> ThreadPool.empty)
            initial  = 
                { 
                   testCount     = 0
                }
            update = update win
            view = view win inServerMode dataFolder glApp 
        }