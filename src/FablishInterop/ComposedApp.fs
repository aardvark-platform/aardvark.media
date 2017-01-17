namespace Scratch

open System
open System.Net
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application

open Fablish
open Fable.Helpers.Virtualdom
open Fable.Helpers.Virtualdom.Html

type ComposedApp<'model,'msg>(initial : 'model, f : 'model -> 'msg -> 'model) =
    let mutable model = initial
    let innerApps = System.Collections.Generic.HashSet<'model -> unit>()

    member x.Update(msg : 'msg) =
        model <- f model msg
        model
        
    member x.AddUi (address : IPAddress) (port : string)  (app : Fablish.App<'innerModel,'innerMsg,DomNode<'innerMsg>>) (buildModel : 'innerModel -> 'model -> 'model) (project : 'model -> 'innerModel) (buildAction : 'innerMsg -> 'msg) =
        let doUpdate : Callback<'innerModel,'innerMsg> =
            fun (m : 'innerModel) (msg : 'innerMsg) ->
                let bigModel = buildModel m model
                let bigMsg = buildAction msg
                let newBigModel = x.Update bigMsg
                for a in innerApps do a newBigModel
                project newBigModel
        let r : Fablish.FablishResult<'innerModel,'innerMsg> = Fablish.Fablish.Serve<'innerModel,'innerMsg>(app, address, port, doUpdate)
        innerApps.Add(fun m -> r.instance.EmitModel (project m) |> ignore) |> ignore
        r

    member x.Register(f : 'model -> unit) = 
        innerApps.Add(f) |> ignore

    member x.Model = model

    member x.InnerApps = innerApps

module ComposedApp =
    
    let ofUpdate initial update = ComposedApp<_,_>(initial, update)

    let inline add3d (comp : ComposedApp<'model,'msg>) (keyboard : IKeyboard) (mouse : IMouse) (viewport : IMod<Box2i>) (camera : IMod<Camera>) (app : Elmish3DADaptive.App<_,_,_,_>)  (buildModel : 'innerModel -> 'model -> 'model) (project : 'model -> 'innerModel) (buildAction : 'innerMsg -> 'msg) =
        let doUpdate (m : 'innerModel) (msg : 'innerMsg) : 'innerModel =
            let bigModel = buildModel m comp.Model
            let bigMsg = buildAction msg
            let newBigModel = comp.Update bigMsg
            for a in comp.InnerApps do a newBigModel
            project newBigModel
        let instance = Elmish3DADaptive.createAppAdaptiveD keyboard mouse viewport camera doUpdate app
        comp.Register(fun m -> instance.emitModel (project m)) 
        instance

    let addUi (comp : ComposedApp<'model,'msg>)  (address : IPAddress) (port : string)  (app : Fablish.App<'innerModel,'innerMsg,DomNode<'innerMsg>>) (buildModel : 'innerModel -> 'model -> 'model) (project : 'model -> 'innerModel) (buildAction : 'innerMsg -> 'msg) =
        comp.AddUi address port app buildModel project buildAction

module Explicit =

    open Scratch.DomainTypes
    open TranslateController

    type AppMsg = SceneMsg of TranslateController.Action 
                | UiMsg of TestApp.Action

    type Model = {  
        ui    : TestApp.Model
        scene : TranslateController.Scene
    }

    let update (model : Model) (msg : AppMsg) =
        let model =
            match msg with
                | AppMsg.SceneMsg (TranslateController.Action.Hover(x,p)) -> 
                    { model with ui = TestApp.update 1 model.ui ( TestApp.Action.SetInfo (sprintf "hover: %A" (x,p)) ) }
                | UiMsg (TestApp.Action.Reset) -> 
                    { model with scene = TranslateController.update model.scene TranslateController.Action.ResetTrafo } 
                | _ -> model

        match msg with
            | AppMsg.SceneMsg msg -> { model with scene = TranslateController.update model.scene msg }
            | AppMsg.UiMsg msg -> { model with ui = TestApp.update "a"  model.ui msg }


module SingleMultiView =

    open Scratch.DomainTypes
    open SharedModel
    open AnotherSceneGraph

    type Action = 
        | Translate of TranslateController.Action
        | UiOnly    of TestApp.Action
        | Reset


    let viewUI (m : Model) =
        div [] [
            div [Style ["width", "100%"; "height", "100%"; "background-color", "transparent"]; attribute "id" "renderControl"] [
                text (sprintf "current content: %d" m.ui.cnt)
                br []
                button [onMouseClick (fun dontCare -> TestApp.Inc); attribute "class" "ui button"] [text "increment"] |> Html.map UiOnly
                button [onMouseClick (fun dontCare -> TestApp.Inc)] [text "decrement"]  |> Html.map UiOnly
                button [onMouseClick (fun dontCare -> Reset)] [text "reset"]
                br []
                text (sprintf "ray: %s" m.ui.info)
            ]
        ]

    let update e (m : Model) (a : Action) =
        let m =
            match a with
            | Translate (TranslateController.Action.Hover(x,p)) -> 
                { m with ui = { m.ui with info = sprintf "pos: %A" p }}
            | _ -> m
        match a with
         | Translate t -> { m with scene = TranslateController.update m.scene t }
         | UiOnly a -> { m with ui = TestApp.update e m.ui a }
         | Reset -> 
            let s = { m.scene with scene = { m.scene.scene with trafo = Trafo3d.Identity }}
            { m with ui = { m.ui with cnt = 0 }; scene = s }

    let view3D (cam : IMod<Camera>) (m : MModel) =
        // cannot implement doe to mini bug
        failwith ""

    open Elmish3DADaptive

    let createApp keyboard mouse viewport camera =

        let initial = { ui = TestApp.initial; scene = TranslateController.initial; _id = null } 
        let composed = ComposedApp.ofUpdate initial (update 1)

        let three3dApp = {
            initial = initial
            update = (update 1)
            view = view3D camera
            ofPickMsg = failwith ""
        }

        let viewApp = 
            {
                initial = initial 
                update = update
                view = viewUI
                subscriptions = Subscriptions.none
                onRendered = OnRendered.ignore
            }

        let three3dInstance : Running<Model,Action> = ComposedApp.add3d composed keyboard mouse viewport camera three3dApp (fun m app -> m) id id
        let fablishInstance = ComposedApp.addUi composed Net.IPAddress.Loopback "8083" viewApp (fun m app -> m) id id

        composed