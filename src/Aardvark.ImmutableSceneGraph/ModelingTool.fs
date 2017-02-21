namespace Scratch

open System
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
          cameraView : CameraView
          objects : pset<Object>
          mode : Mode
          geometryImport : GeometryImport }
        
        member x.ToMod(reuseCache : ReuseCache) = 
            { _original = x
              mprimary =
                match x.primary with
                    | None -> Mod.init None
                    | Some v -> Mod.init (v.ToMod(reuseCache) |> Some)
              mviewTrafo = Mod.init (x.cameraView)
              mobjects = 
                  MapSet
                      ((reuseCache.GetCache()), x.objects, 
                       (fun (a : Object) -> a.ToMod(reuseCache)), 
                       (fun (m : MObject, a : Object) -> m.Apply(a, reuseCache)))
              mmode = x.mode.ToMod(reuseCache)
              mgeometryImport = Mod.init x.geometryImport
            }
        
        interface IUnique with
            
            member x.Id 
                with get () = x._id
                and set v = x._id <- v
    
    and [<DomainType>] MState = 
        { mutable _original : State
          mprimary : ModRef<Option<MObject>>
          mviewTrafo : ModRef<CameraView>
          mobjects : MapSet<Object, MObject>
          mmode : MMode 
          mgeometryImport : ModRef<GeometryImport> }
        member x.Apply(arg0 : State, reuseCache : ReuseCache) = 
            if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                x._original <- arg0
                match x.mprimary.Value,arg0.primary with
                    | Some _, None -> x.mprimary.Value <- None
                    | None, Some v -> x.mprimary.Value <- Some (v.ToMod(reuseCache))
                    | None, None -> ()
                    | Some a, Some b -> a.Apply(b,reuseCache)
                x.mviewTrafo.Value <- arg0.cameraView
                x.mobjects.Update(arg0.objects)
                x.mmode.Apply(arg0.mode, reuseCache)
                x.mgeometryImport.Value <- arg0.geometryImport


module ModelingTool =

    open System.Windows.Forms

    open Aardvark.ImmutableSceneGraph
    open Aardvark.Elmish

    open Fablish
    open Fable.Helpers.Virtualdom
    open Fable.Helpers.Virtualdom.Html

    open Models

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
                    text "Path: "
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
            | Importer (GeometryImport.Accept s) -> 
                // add model
                printfn "import model: %s" s
                m
            | Importer a -> { m with geometryImport = GeometryImport.update f (Env.map Importer e) m.geometryImport a }
            | _ -> m


    let view3D (sizes : IMod<V2i>) (m : MState) =
        let frustum = sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))
        let model = 
            m.mgeometryImport |> Mod.map (fun path -> 
                if System.IO.File.Exists path then
                    Aardvark.SceneGraph.IO.Loader.Assimp.load path |> Sg.AdapterNode |> Sg.normalizeTo (Box3d(-V3d.III, V3d.III))
                else 
                    printfn "file does not exist"
                    Sg.ofSeq []
            ) |> Sg.dynamic |> Scene.ofSg
            
        model
         |> Scene.camera (Mod.map2 Camera.create m.mviewTrafo frustum)
         |> Scene.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]

    let initial = { _id = null; primary = None; cameraView = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI; objects = PSet.empty; mode = Mode.Selecting; geometryImport = "" }

    let fablishApp f =
        {
            initial = initial
            update = update f
            view = viewUI
            onRendered = OnRendered.ignore
            subscriptions = Subscriptions.none
        }

   
    let ofPickMsg (m : State) (noPick) = []


    let createApp f keyboard mouse viewport camera =

        let initial = initial
        let composed = ComposedApp.ofUpdate initial (update f)

        let three3dApp  = {
            initial = initial
            update = update f
            view = view3D (viewport |> Mod.map (fun (a : Box2i) -> a.Size))
            ofPickMsg = ofPickMsg
            subscriptions = Aardvark.Elmish.Subscriptions.none// Subscriptions.none
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