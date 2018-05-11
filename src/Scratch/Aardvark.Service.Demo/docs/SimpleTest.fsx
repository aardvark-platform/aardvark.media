(***hide***)
#I "../../../bin/Release"
#r "Aardvark.Base.dll" 
#r "Aardvark.Base.TypeProviders.dll"

#r "System.Reactive.Core.dll"
#r "System.Reactive.Interfaces.dll"
#r "System.Reactive.Linq.dll"
#r "DevILSharp.dll"

#r "System.Numerics.dll"
#r "System.Drawing.dll"
#r "System.Data.Linq.dll"
#r "System.Data.dll"
#r "System.Collections.Immutable.dll"
#r "System.ComponentModel.Composition.dll"

#r "Aardvark.Base.Essentials.dll"
#r "Aardvark.Base.FSharp.dll"
#r "Aardvark.Base.Incremental.dll"
#r "Aardvark.Base.Runtime.dll"

#r "Aardvark.Compiler.DomainTypes.dll"

#r "FShade.Core.dll"
#r "FShade.GLSL.dll"
#r "FShade.Imperative.dll"

#r "Aardvark.Base.Rendering.dll"

#r "Aardvark.SceneGraph.dll"
#r "Aardvark.Rendering.NanoVg.dll"
#r "Aardvark.Rendering.GL.dll"
#r "Aardvark.Application.dll"
#r "Aardvark.Application.WinForms.dll"
#r "Aardvark.Application.WinForms.GL.dll"

#r "Aardvark.UI.dll"
#r "Aardvark.UI.Primitives.dll"


(**


# Hello world from aardvark.media

*)

#load "SimpleTestModel.fs"
#load "SimpleTestModel.g.fs"
 
module SimpleTestApp =

    open SimpleTest 

    open Aardvark.Base
    open Aardvark.Base.Incremental


    open Aardvark.SceneGraph
    open Aardvark.Base.Rendering
    open Aardvark.UI

    type Action = 
        | Inc 
        | Dec

    let update (m : Model) (a : Action) =
        match a with
            | Inc -> { m with value = m.value + 1.0 }
            | Dec -> { m with value = m.value - 1.0 } 



    let cam = 
        Camera.create (CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI) (Frustum.perspective 60.0 0.1 10.0 1.0)


    let threeD (m : MModel) =

        let t =
            adaptive {
                let! t = m.value
                return Trafo3d.RotationZ(t * 0.1)
            }

        let sg =
            Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
            |> Sg.requirePicking
            |> Sg.noEvents
            //|> Sg.pickable (PickShape.Box Box3d.Unit)       
            |> Sg.trafo t
            |> Sg.withEvents [
                    Sg.onMouseDown (fun _ _ -> Inc)
              ]
            |> Sg.effect [
                        toEffect DefaultSurfaces.trafo
                        toEffect <| DefaultSurfaces.constantColor C4f.Red 
                    ]
            

        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        CameraController.controlledControl m.cameraModel CameraAction
            (Mod.constant frustum) 
            (AttributeMap.ofList [ attribute "style" "width:70%; height: 100%"]) sg

    let view (m : MModel) =
        let s =
            adaptive {
                let! v = m.value
                return string v
            }
        printfn "exectued some things..."
        div [] [
            text "constant text"
            br []
            Incremental.text s
            //text (Mod.force s)
            br []
            button [onMouseClick (fun _ -> Inc)] [text "inc"]
            button [onMouseClick (fun _ -> Dec)] [text "dec"]
            br []
            threeD m
        ]

    let app =
        {
            unpersist = Unpersist.instance
            threads = fun (model : Model) -> CameraController.threads model.cameraModel |> ThreadPool.map CameraAction
            initial = { value = 1.0; cameraModel = CameraController.initial }
            update = update
            view = view
        }

    let start() = App.start app
