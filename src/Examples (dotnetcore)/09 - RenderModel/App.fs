module App

open RenderModel // open the namespace which holds our domain types (both, the original and the generated ones)

open Aardvark.Base             // math stuff such as V3d, Trafo3d
open Aardvark.Base.Incremental // the incremental system (Mod et al) including domain type functionality
open Aardvark.Base.Rendering   // basic rendering datastructures (such as CullMode)

open Aardvark.UI            // the base infrastructure for elm style aardvark applications
open Aardvark.UI.Primitives // for gui elements such as div, text, button but also camera controlers

// the actions (might be nested for composability) of our application
type Action = 
    | SetObject     of Object 
    | LoadModel     of string
    | SetCullMode   of CullMode
    | CameraAction  of FreeFlyController.Message // a nested message, handled by camera controller app

// given the current immutable state and an action, compute a new immutable model
let update (m : Model) (a : Action) =
    match a with
        | SetObject obj      -> { m with currentModel = Some obj }
        | CameraAction a     -> // compute a new state by reusing camera update logic implemented in CameraController app.
            { m with cameraState = FreeFlyController.update m.cameraState a }
        | LoadModel file     -> { m with currentModel = Some (FileModel file) }
        | SetCullMode mode -> { m with appearance = { cullMode = mode } }

// map objects to their rendering representation (adaptively)
let renderModel (model : IMod<MObject>) =
    adaptive {
        let! currentModel = model // type could change, read adaptively
        match currentModel with
            | MFileModel fileName -> 
                let! file = fileName // read current filename (if model stays the same but filename changes)
                if System.IO.File.Exists file then  // check if good
                    return 
                        file 
                        |> Sg.Assimp.loadFromFile true 
                        |> Sg.trafo (Trafo3d.Scale(1.0,1.0,-1.0) |> Mod.constant)
                        |> Sg.normalize // create scenegraph
                else 
                    Log.warn "file not found"
                    return Sg.empty 
            | MSphereModel(center,radius) ->
                let sphere = Sg.sphere 6 (Mod.constant C4b.White) radius 
                // create unit sphere of given mod radius and translate adaptively
                return Sg.translate' center sphere
            | MBoxModel b -> 
                return Sg.box (Mod.constant C4b.White) b //adaptively create box
    }

// map the adaptive model to a rendering representation.
let view3D (m : MModel) =
    let model =
        adaptive {
            let! model = m.currentModel // peel of level of change: the model could have changed
            match model with
                | None ->  // model switched to no model
                    return Sg.empty // no model specified, render nothing
                | Some model -> // model switched to Some model
                    return! renderModel model // render the model
        }

    let sg =
        model
         |> Sg.dynamic
         |> Sg.trafo m.trafo
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.White
                do! DefaultSurfaces.simpleLighting
            }
         |> Sg.cullMode m.appearance.cullMode 
            // nested domain type values can be accessed naturally
            // (nested changeability Mod<Mod<..>> is flattened out)          
            

    let frustum = Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant
    let attributes = AttributeMap.ofList [ attribute "style" "width:100%; height: 100%"; attribute "data-samples" "8"]
    FreeFlyController.controlledControl m.cameraState CameraAction frustum attributes sg

let aadvarkModel = FileModel @"..\..\..\data\aardvark\aardvark.obj"
let defaultSphere = SphereModel(V3d.OOO,1.0)
let defaultBox = BoxModel Box3d.Unit
// create camera which looks down from (2,2,2) to (0,0,0) while z is up
let initialView = CameraView.lookAt (V3d.III * 6.0) V3d.OOO V3d.OOI

let view (m : MModel) =
    require Html.semui ( // we use semantic ui for our gui. the require function loads semui stuff such as stylesheets and scripts
        body [] (        // explit html body for our app (adorner menus need to be immediate children of body). if there is no explicit body the we would automatically generate a body for you.
            Html.SemUi.adornerMenu [ 
                "Set Scene", [ 
                    button [clazz "ui button"; onClick (fun _ -> SetObject aadvarkModel)]  [text "The aardvark model"]
                    button [clazz "ui button"; onClick (fun _ -> SetObject defaultSphere)] [text "Sphere"] 
                    button [clazz "ui button"; onClick (fun _ -> SetObject defaultBox)]    [text "Box"] 
                    button (clazz "ui button" :: Html.IO.fileDialog LoadModel)             [text "Load from File"]
                ] 
                "Appearance", [
                    Html.SemUi.dropDown m.appearance.cullMode SetCullMode 
                ]
            ] [view3D m]
        )
    )

// in order to provide camera animations, we need to compute a set of  
// background operations (we call threads). The app maintains (just like each other state)
// a set of threads which will be executed as long as they exist (no manual subscription stuff required).
let threads (model : Model) = 
    FreeFlyController.threads model.cameraState |> ThreadPool.map CameraAction // compute threads for camera controller and map its outputs with our CameraAction

let app =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial = { 
                    currentModel = None; 
                    cameraState  = { FreeFlyController.initial with view = initialView }
                    trafo        = Trafo3d.Identity 
                    appearance   = { cullMode = CullMode.None }
                  }
        update = update
        view = view
    }

let start() = App.start app