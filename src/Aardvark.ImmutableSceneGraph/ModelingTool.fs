namespace Scratch


open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application


open Aardvark.ImmutableSceneGraph
open Aardvark.Elmish
open Primitives

module Models =

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    type TranslateModel = Scratch.DomainTypes.Generated.TranslateController.TModel
    type MTranslateModel = Scratch.DomainTypes.Generated.TranslateController.MTModel

    type GeometryImport = string

    type Mode = 
        | Selecting
        | Translating of TranslateModel
        with
            member x.ToMod(cache : ReuseCache) : MMode =
                match x with
                    | Selecting -> { _id = null; tag = Mod.init MSelecting }
                    | Translating t -> { _id = null; tag = t.ToMod(cache) |> MTranslating |> Mod.init }

    and MModeTagged =
        | MSelecting | MTranslating of MTranslateModel

    and MMode = 
        { _id : Id; tag : ModRef<MModeTagged> }
        with
            member x.Apply(m : Mode, cache : ReuseCache) =
                match x.tag.Value,m with
                    | MSelecting, Selecting  -> ()
                    | MTranslating m', Translating m -> m'.Apply(m,cache)
                    | MSelecting, Translating m ->  x.tag.Value <- MTranslating ( m.ToMod(cache) )
                    | MTranslating _, Selecting -> x.tag.Value <- MSelecting

    type Model = 
        { fileName : string
          sceneGraph : ISg
          bounds : Box3d }
    
    [<DomainType>]
    type Object = 
        { mutable _id : Id
          name : string
          trafo : Trafo3d
          model : Model }
        
        member x.ToMod(reuseCache : ReuseCache) = 
            { _original = x
              mname = Mod.init (x.name)
              mtrafo = Mod.init (x.trafo)
              mmodel = Mod.init (x.model) }
        
        interface IUnique with
            
            member x.Id 
                with get () = x._id
                and set v = x._id <- v
    
    and [<DomainType>] MObject = 
        { mutable _original : Object
          mname : ModRef<string>
          mtrafo : ModRef<Trafo3d>
          mmodel : ModRef<Model> }
        member x.Apply(arg0 : Object, reuseCache : ReuseCache) = 
            if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                x._original <- arg0
                x.mname.Value <- arg0.name
                x.mtrafo.Value <- arg0.trafo
                x.mmodel.Value <- arg0.model
    
    [<DomainType>]
    type State = 
        { mutable _id : Id
          primary : Option<Object>
          cameraModel : Scratch.DomainTypes2.Generated.CameraTest.Model
          objects : pset<Object>
          mode : Mode
          geometryImport : GeometryImport
          interactionState : Scratch.DomainTypes.Generated.TranslateController.TModel }
        
        member x.ToMod(reuseCache : ReuseCache) = 
            { _original = x
              mprimary =
                match x.primary with
                    | None -> Mod.init None
                    | Some v -> Mod.init (v.ToMod(reuseCache) |> Some)
              mcameraModel = x.cameraModel.ToMod(reuseCache)
              mobjects = 
                  MapSet
                      ((reuseCache.GetCache()), x.objects, 
                       (fun (a : Object) -> a.ToMod(reuseCache)), 
                       (fun (m : MObject, a : Object) -> m.Apply(a, reuseCache)))
              mmode = x.mode.ToMod(reuseCache)
              mgeometryImport = Mod.init x.geometryImport
              minteractionState = x.interactionState.ToMod(reuseCache)
            }
        
        interface IUnique with
            
            member x.Id 
                with get () = x._id
                and set v = x._id <- v
    
    and [<DomainType>] MState = 
        { mutable _original : State
          mprimary : ModRef<Option<MObject>>
          mcameraModel : Scratch.DomainTypes2.Generated.CameraTest.MModel
          mobjects : MapSet<Object, MObject>
          mmode : MMode 
          mgeometryImport : ModRef<GeometryImport>
          minteractionState : Scratch.DomainTypes.Generated.TranslateController.MTModel }
        member x.Apply(arg0 : State, reuseCache : ReuseCache) = 
            if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                x._original <- arg0
                match x.mprimary.Value,arg0.primary with
                    | Some _, None -> x.mprimary.Value <- None
                    | None, Some v -> x.mprimary.Value <- Some (v.ToMod(reuseCache))
                    | None, None -> ()
                    | Some a, Some b -> a.Apply(b,reuseCache)
                x.mcameraModel.Apply(arg0.cameraModel,reuseCache)
                x.mobjects.Update(arg0.objects)
                x.mmode.Apply(arg0.mode, reuseCache)
                x.mgeometryImport.Value <- arg0.geometryImport
                x.minteractionState.Apply(arg0.interactionState,reuseCache)


module ModelingTool =

    open Aardvark.ImmutableSceneGraph
    open Aardvark.Elmish

    open Fablish
    open Fable.Helpers.Virtualdom
    open Fable.Helpers.Virtualdom.Html

    open Models
    open Input

    module GeometryImport =

        type Model = string
        type Action = SetPath of string | OpenDialog | Accept of string | Deny

        let openDialog (e : Env<Action>) (w : Form) =
            w.BeginInvoke(Action(fun _ -> 
                let dialog = new OpenFileDialog()
                if dialog.ShowDialog() = DialogResult.OK then
                    async { return SetPath dialog.FileName } |> Cmd.Cmd |> e.run
                else ()
            ))

        let runModal m innerview title accept deny=
            div [clazz "ui modal"] [
                i [clazz "close icon"] []
                div [clazz "header"] [text title]
                div[clazz "content"] [
                    innerview
                ]
                div [clazz "actions"] [
                    div [clazz "ui button deny"; onMouseClick (fun _ -> deny)] [text "nope"]
                    div [clazz "ui button positive"; onMouseClick (fun _ -> accept m)] [text "yes"]
                ]
            ]

        let importModel m =
            let view =
                div [] [
                    text "Path:    "
                    
                    text (if m = "" then "<select a path>" else sprintf "%s" (m.Replace("\\","\\\\"))) 
                    br []
                    br []
                    button [clazz "ui button"; Style ["align","right"]; onMouseClick (fun _ -> OpenDialog)] [text "Browse"]
                ]
            runModal m view "Import Model" Accept Deny
            

        let update (f : Form) e model msg =
            match msg with
                | SetPath s -> s
                | OpenDialog -> 
                    openDialog e f |> ignore
                    model
                | Deny -> model 
                | Accept s -> s
                    
    type Action = 
        | Translate of TranslateController.Action
        | Importer  of GeometryImport.Action
        | CameraAction of FreeFlyCameraApp.Action
        | AddObjects of list<Object>
        | SelectObject of Aardvark.Base.Incremental.Id 
        | Interact of TranslateController.Action 
        | Unselect

    let viewUI (m : State) =
        div [] [
            div [Style ["width", "100%"; "height", "100%"; "background-color", "transparent"]; attribute "id" "renderControl"] [
                br []
                button [Callback("onClick","$('.ui.modal').modal('show');"); clazz "ui button"] [text "Load Model"] 
                br []
                text (sprintf "mode: %A" m.mode)
            ]
            GeometryImport.importModel m.geometryImport |> Html.map Importer 
        ]


    let update (f : Form) (e : Env<Action>) (m : State) (a : Action) =
        match a with
            | Importer (GeometryImport.Accept path) -> 
                // add model
                printfn "import model: %s" path
                async {
                    if System.IO.File.Exists path then
                        let scene = Aardvark.SceneGraph.IO.Loader.Assimp.load path
                        let sg = 
                            scene 
                             |> Sg.AdapterNode 
                             //|> Sg.normalizeTo (Box3d(-V3d.III, V3d.III)) 
                        return [ { _id = Id.New; name = path; trafo = Trafo3d.Identity; model = { fileName = path; sceneGraph = sg; bounds = scene.bounds }} ] |> AddObjects
                    else return AddObjects []
                } |> Cmd.Cmd |> e.run

                m
            | Importer a -> { m with geometryImport = GeometryImport.update f (Env.map Importer e) m.geometryImport a }
            | CameraAction a when m.primary.IsNone -> { m with cameraModel = FreeFlyCameraApp.update (e |> Env.map CameraAction) m.cameraModel a }
            | AddObjects [] -> m
            | AddObjects xs -> 
                let objs = List.fold (flip PSet.add) m.objects xs
                printfn "added objets: %A" objs
                { m with objects = objs }
            | SelectObject o -> 
                printfn "selected obj"
                let obj =  m.objects |> PSet.toList |> List.find (fun a -> a._id = o)
                { m with primary = obj |> Some; interactionState = { TranslateController.initalModel with editTrafo =  obj.trafo }  }
            | Interact(a) when m.primary.IsSome -> 
                let interaction = TranslateController.updateModel (Env.map Interact e) m.interactionState a
                let setTrafo (obj : Object) : Object = { obj with trafo = interaction.trafo * interaction.editTrafo }
                let objects = m.objects |> PSet.toList |> List.map (fun obj -> if obj._id = m.primary.Value._id then setTrafo obj else obj) |> PSet.ofList
                { m with interactionState = interaction; objects = objects; primary = Option.map setTrafo m.primary }
            | Unselect -> { m with primary = None; cameraModel =  FreeFlyCameraApp.groundIt m.cameraModel; interactionState = TranslateController.initalModel  }
            | _ -> m

    let viewModels (state : MState) =
        aset {
            for o in state.mobjects do
                let! m = o.mmodel
                yield 
                    Scene.group [
                        Scene.pick' [on (Mouse.down' MouseButtons.Left) (fun _ -> SelectObject o._original._id)] (Primitives.Box(m.bounds, false, false))
                             |> Scene.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect] 
                        m.sceneGraph |> Scene.ofSg
                    ]  |> Scene.transform' o.mtrafo
            let! primary = state.mprimary
            match primary with
                | None -> ()
                | Some o -> 
                    let! obj = o.mmodel
                    yield
                        Scene.pick' [] (Primitives.Box(obj.bounds, false, true))
                                 |> Scene.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect] 
                                 |> Scene.transform' o.mtrafo
                    yield TranslateController.viewModel state.minteractionState |> Scene.map  Interact 
                            |> Scene.effect [toEffect DefaultSurfaces.trafo; toEffect DefaultSurfaces.vertexColor; toEffect DefaultSurfaces.simpleLighting]
        } |> Scene.agroup

    let view3D (sizes : IMod<V2i>) (m : MState) =
        let frustum = sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))            
        viewModels m
         |> Scene.camera (Mod.map2 Camera.create m.mcameraModel.mcamera frustum)
         |> Scene.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]

    let initial = { _id = null; primary = None; cameraModel = Scratch.FreeFlyCameraApp.initial; objects = PSet.empty; mode = Mode.Selecting; geometryImport = ""; interactionState = TranslateController.initalModel }

    let subscriptions (time : IMod<DateTime>) (m : State) =
        Aardvark.Elmish.Sub.Many [
            if m.primary.IsNone then yield FreeFlyCameraApp.subscriptions time m.cameraModel |> Aardvark.Elmish.Sub.map CameraAction
            yield Input.key Direction.Up Keys.Escape (fun _ _ -> Unselect) 
        ]

    let fablishApp f =
        {
            initial = initial
            update = update f
            view = viewUI
            onRendered = OnRendered.ignore
            subscriptions = Subscriptions.none
        }

   
    let ofPickMsg (m : State) (noPick) = 
        TranslateController.ofPickMsgModel m.interactionState noPick |> List.map Interact


    let createApp f time keyboard mouse viewport camera =

        let initial = initial
        let composed = ComposedApp.ofUpdate initial (update f)

        let three3dApp  = {
            initial = initial
            update = update f
            view = view3D (viewport |> Mod.map (fun (a : Box2i) -> a.Size))
            ofPickMsg = ofPickMsg
            subscriptions = subscriptions time
        }

        let viewApp = 
            {
                initial = initial 
                update = update f
                view = viewUI
                subscriptions = Fablish.CommonTypes.Subscriptions.none
                onRendered = OnRendered.ignore
            }

        let three3dInstance = ComposedApp.add3d composed keyboard mouse viewport camera three3dApp (fun m app -> m) id id
        let fablishInstance = ComposedApp.addUi composed Net.IPAddress.Loopback "8083" viewApp (fun m app -> m) id id

        three3dInstance, fablishInstance