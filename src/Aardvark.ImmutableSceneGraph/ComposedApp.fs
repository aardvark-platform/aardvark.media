namespace Scratch

open System
open System.Net
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application

open Fablish
open Fable.Helpers.Virtualdom
open Fable.Helpers.Virtualdom.Html

open Aardvark.ImmutableSceneGraph
open Aardvark.Elmish

type ComposedApp<'model,'msg>(initial : 'model, f : Env<'msg> -> 'model -> 'msg -> 'model) as this =
    let mutable model = initial
    let innerApps = System.Collections.Generic.HashSet<'model -> unit>()

    let emitEnv cmd = 
        match cmd with
            | NoCmd -> ()
            | Cmd cmd -> 
                async {
                    let! msg = cmd
                    this.Update msg |> ignore
                } |> Async.Start

    let env = { run = emitEnv }

    member x.Update(msg : 'msg) =
        lock x (fun _ -> 
            model <- f env model msg
            model
        )
        
    member x.AddUi (address : IPAddress) (port : string)  (app : Fablish.App<'innerModel,'innerMsg,DomNode<'innerMsg>>) (buildModel : 'innerModel -> 'model -> 'model) (project : 'model -> 'innerModel) (buildAction : 'innerMsg -> 'msg) =
        let doUpdate : Callback<'innerModel,'innerMsg> =
            fun (m : 'innerModel) (msg : 'innerMsg) ->
                lock x (fun _ -> 
                    let bigModel = buildModel m model
                    let bigMsg = buildAction msg
                    let newBigModel = x.Update bigMsg
                    for a in innerApps do a newBigModel
                    project newBigModel
                )
        let r : Fablish.FablishResult<'innerModel,'innerMsg> = Fablish.Fablish.Serve<'innerModel,'innerMsg>(app, address, port, doUpdate)
        innerApps.Add(fun m -> r.instance.EmitModel (project m) |> ignore) |> ignore
        r

    member x.Register(f : 'model -> unit) = 
        innerApps.Add(f) |> ignore

    member x.Model = model

    member x.InnerApps = innerApps

module ComposedApp =
    
    let ofUpdate initial update = ComposedApp<_,_>(initial, update)

    let inline add3d (comp : ComposedApp<'model,'msg>) (keyboard : IKeyboard) (mouse : IMouse) (viewport : IMod<Box2i>) (camera : IMod<Camera>) (app : App<_,_,_,_>)  (buildModel : 'innerModel -> 'model -> 'model) (project : 'model -> 'innerModel) (buildAction : 'innerMsg -> 'msg) =
        let doUpdate (m : 'innerModel) (msg : 'innerMsg) : 'innerModel =
            lock comp (fun _ -> 
                let bigModel = buildModel m comp.Model
                let bigMsg = buildAction msg
                let newBigModel = comp.Update bigMsg
                for a in comp.InnerApps do a newBigModel
                project newBigModel
            )
        let instance = Elmish.createAppAdaptiveD keyboard mouse viewport camera (Some doUpdate) app
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

    let update e (model : Model) (msg : AppMsg) =
        let model =
            match msg with
                | AppMsg.SceneMsg (TranslateController.Action.Hover(x,p)) -> 
                    { model with ui = TestApp.update (Env.map UiMsg e) model.ui ( TestApp.Action.SetInfo (sprintf "hover: %A" (x,p)) ) }
                | UiMsg (TestApp.Action.Reset) -> 
                    { model with scene = TranslateController.update (Env.map SceneMsg e) model.scene TranslateController.Action.ResetTrafo } 
                | _ -> model

        match msg with
            | AppMsg.SceneMsg msg -> { model with scene = TranslateController.update (Env.map SceneMsg e) model.scene msg }
            | AppMsg.UiMsg msg -> { model with ui = TestApp.update (Env.map UiMsg e) model.ui msg }


module SingleMultiView =

    open Scratch.DomainTypes
    open SharedModel
    open Aardvark.ImmutableSceneGraph
    open Aardvark.Elmish

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

    let update (e : Env<Action>) (m : Model) (a : Action) =
        let m =
            match a with
            | Translate (TranslateController.Action.Hover(x,p)) -> 
                { m with ui = { m.ui with info = sprintf "pos: %A" p }}
            | _ -> m
        match a with
         | Translate t -> { m with scene = TranslateController.update (e |> Env.map Translate) m.scene t }
         | UiOnly a -> { m with ui = TestApp.update (Env.map UiOnly e) m.ui a }
         | Reset -> 
            let s = { m.scene with scene = { m.scene.scene with trafo = Trafo3d.Identity }}
            { m with ui = { m.ui with cnt = 0 }; scene = s }

    let view3D (sizes : IMod<V2i>) (m : MModel) =
        m.mscene  |> TranslateController.viewScene sizes |> Scene.map Translate

    let ofPickMsg (m : Model) (noPick) = TranslateController.ofPickMsg m.scene noPick |> List.map Translate


    let createApp keyboard mouse viewport camera =

        let initial = { ui = TestApp.initial; scene = TranslateController.initial; _id = null } 
        let composed = ComposedApp.ofUpdate initial update 

        let three3dApp : App<Model,MModel,Action,ISg<Action>> = {
            initial = initial
            update = update
            view = view3D (viewport |> Mod.map (fun (a : Box2i) -> a.Size))
            ofPickMsg = ofPickMsg
            subscriptions = Subscriptions.none
        }

        let viewApp : Fablish.App<Model,Action,DomNode<Action>> = 
            {
                initial = initial 
                update = update
                view = viewUI
                subscriptions = Fablish.CommonTypes.Subscriptions.none
                onRendered = OnRendered.ignore
            }

        let three3dInstance : Elmish.Running<Model,Action> = ComposedApp.add3d composed keyboard mouse viewport camera three3dApp (fun m app -> m) id id
        let fablishInstance = ComposedApp.addUi composed Net.IPAddress.Loopback "8083" viewApp (fun m app -> m) id id

        three3dInstance, fablishInstance


module NiceCompose =

    type Component<'model,'mmodel,'msg> = 
        | Fablish of ('model -> DomNode<'msg>)
        | ThreeD  of ('mmodel -> ISg<'msg>)
        | Group of Component<'model,'mmodel,'msg> list

    module Component =
        let map (f : 'a -> 'b) (c : Component<'model,'mmodel,'a>) : Component<'model,'mmodel,'b> =
            match c with
                | Fablish view -> Fablish (fun m -> Html.map f (view m))
                | ThreeD view -> ThreeD (fun m -> Scene.map f (view m))

    type ElmishApp<'model,'mmodel,'msg> = { 
        initial : 'model
        view : 'model -> Component<'model,'mmodel,'msg>
        update : Env<'msg> -> 'model -> 'msg -> 'model
    }


module NiceComposeExample =

    open Scratch.DomainTypes
    open SharedModel
    open Aardvark.ImmutableSceneGraph
    open Aardvark.Elmish
    open NiceCompose

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

    let update (e : Env<Action>) (m : Model) (a : Action) =
        let m =
            match a with
            | Translate (TranslateController.Action.Hover(x,p)) -> 
                { m with ui = { m.ui with info = sprintf "pos: %A" p }}
            | _ -> m
        match a with
         | Translate t -> { m with scene = TranslateController.update (e |> Env.map Translate) m.scene t }
         | UiOnly a -> { m with ui = TestApp.update (Env.map UiOnly e) m.ui a }
         | Reset -> 
            let s = { m.scene with scene = { m.scene.scene with trafo = Trafo3d.Identity }}
            { m with ui = { m.ui with cnt = 0 }; scene = s }

    let view3D (sizes : IMod<V2i>) (m : MModel) =
        m.mscene  |> TranslateController.viewScene sizes |> Scene.map Translate

    let ofPickMsg (m : Model) (noPick) = TranslateController.ofPickMsg m.scene noPick |> List.map Translate

    let view sizes (m : Model) : Component<Model,MModel,Action> =
        Group [
            Fablish viewUI
            ThreeD (view3D sizes)
        ]

    let elmishApp sizes = {
        initial = { ui = TestApp.initial; scene = TranslateController.initial; _id = null }
        update = update
        view = view sizes
    }
