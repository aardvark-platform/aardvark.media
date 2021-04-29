module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Model
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.SceneGraph

let initialCamera = { 
    FreeFlyController.initial' 3.0 with 
        view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
}

let rnd = System.Random()

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }
        | Tick t -> 
            { model with trafo = Trafo3d.RotationZ(t * 0.1) }
        | ChangeThickness t -> 
          { model with thickness = Numeric.update model.thickness t }
        | ToggleAnimation -> 
          { model with animationEnabled = not model.animationEnabled }


module Shader =
    open FShade
   
    type SuperVertex = 
        {
            [<Position>] pos :  V4d
            [<SourceVertexIndex>] i : int
        }

    let lines (t : Triangle<SuperVertex>) =
        line {
            yield t.P0
            yield t.P1
            restartStrip()
            
            yield t.P1
            yield t.P2
            restartStrip()

            yield t.P2
            yield t.P0
            restartStrip()
        }

let viewScene (model : AdaptiveModel) =

    let read reference =
        { StencilMode.None with
            Comparison = ComparisonFunction.Greater
            Reference = reference }

    let write reference =
        { StencilMode.None with
            Pass = StencilOperation.Replace
            DepthFail = StencilOperation.Replace
            Reference = reference
            Comparison = ComparisonFunction.Greater }

    let geom1 =
        [
            Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit) |> Sg.trafo (AVal.constant (Trafo3d.Translation(V3d(0.0, 2.0, 0.0))))
            Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit) |> Sg.trafo (AVal.constant (Trafo3d.Translation(V3d(0.0, 0.0, 1.0))))
        ] |> Sg.ofList

    let geom2 =
        [
            IndexedGeometryPrimitives.Torus.solidTorus (Torus3d(V3d(0.0, 0.0, 0.0), V3d.OOI, 1.0, 0.25)) C4b.Blue 20 20 |> Sg.ofIndexedGeometry
            IndexedGeometryPrimitives.Torus.solidTorus (Torus3d(V3d(0.0, 1.0, 2.0), V3d.OOI, 1.0, 0.25)) C4b.Blue 20 20 |> Sg.ofIndexedGeometry
        ] |> Sg.ofList
    
    let testLines = [|Line3d(V3d(1.0, 0.0, 0.0), V3d(2.0, 0.0, 0.0)); Line3d(V3d(2.0, 0.0, 0.0), V3d(3.0, 2.0, 0.0))|] |> AVal.constant

    //let sgLine = Sg.lines (C4b.DarkMagenta |> AVal.constant) testLines 
   

    let pass0 = RenderPass.main
    let pass1 = RenderPass.after "outlineRed" RenderPassOrder.Arbitrary pass0
    let pass2 = RenderPass.after "outlineYellow" RenderPassOrder.Arbitrary pass1

    let regular sg = 
        sg
         |> Sg.pass pass0
         |> Sg.trafo model.trafo
         |> Sg.cullMode' CullMode.Back
         |> Sg.depthTest' DepthTest.Less
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }

    let mask sg v = 
        sg
         |> Sg.pass pass0
         |> Sg.trafo model.trafo
         |> Sg.stencilMode' (write v)
         |> Sg.writeBuffers' (Set.ofList [DefaultSemantic.Stencil])
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }

    let outline sg v pass colour = 
        sg
         |> Sg.trafo model.trafo
         |> Sg.stencilMode' (read v)
         |> Sg.depthTest' DepthTest.None
         |> Sg.writeBuffers' (Set.ofList [DefaultSemantic.Colors])
         |> Sg.pass pass
         |> Sg.uniform "LineWidth" model.thickness.value
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! Shader.lines
                do! DefaultSurfaces.thickLine
                do! DefaultSurfaces.thickLineRoundCaps
                do! DefaultSurfaces.constantColor colour
            }

    let outLine sg v pass colour = 
        sg
         |> Sg.trafo model.trafo
         |> Sg.stencilMode' (read v)
         |> Sg.depthTest' DepthTest.None
         |> Sg.writeBuffers' (Set.ofList [DefaultSemantic.Colors])
         |> Sg.pass pass
         |> Sg.uniform "LineWidth" (AVal.constant 5.0)
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! Shader.lines
                do! DefaultSurfaces.thickLine
                do! DefaultSurfaces.thickLineRoundCaps
                do! DefaultSurfaces.constantColor colour
            }

    let regular = regular ([geom1; geom2] |> Sg.ofList)
    let redMask = mask geom1 1
    let yellowMask = mask geom2 2

    

    let redOutline = outline geom1 1 pass1 C4f.Red
    let yellowOutline = outline geom2 2 pass2 C4f.Yellow

    Sg.ofSeq [regular; redMask; yellowMask; redMask; yellowMask; redOutline; yellowOutline] |> Sg.noEvents 

let mymap (f : 'a -> 'b) (ui : DomNode<'a>) : DomNode<'b> =
    let app =
        {
            initial = ()
            update = fun () _ -> ()
            view = fun () -> ui
            unpersist = { create = id; update = constF ignore }
            threads = fun () -> ThreadPool.empty
        }

    subApp' (fun _ msg -> Seq.singleton (f msg)) (fun _ _ -> Seq.empty) [] app

// variant with html5 grid layouting (currently not working in our cef)
let view (model : AdaptiveModel) =
    let renderControl =
      FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
        (AttributeMap.ofList [ 
            attribute "showFPS" "true"; 
            attribute "data-renderalways" "1"; 
            attribute "data-samples" "8"; 
            style "width: 100%; height:80%"])
        (viewScene model)

    body [] [
      require Html.semui (
        div [clazz "ui inverted"] [
          div [] [
            text "Hello 3D Contour"
            br []
            button [onClick (fun _ -> CenterScene)] [text "Center Scene.."]
          ]
          renderControl
          div[style "width:400px"][
            Html.table [
              Html.row "Animate:"   [Html.SemUi.iconCheckBox model.animationEnabled ToggleAnimation]
              Html.row "Thickness:" [Numeric.view' [NumericInputType.Slider] model.thickness |> UI.map ChangeThickness ]                  
            ]
          ]
        ]
      )
    ]

let totalTime = System.Diagnostics.Stopwatch.StartNew()
let rec time() =
    proclist {
        do! Proc.Sleep 10
        yield Tick totalTime.Elapsed.TotalSeconds
        yield! time()
    }

let threads (model : Model) = 
    let cameraController = FreeFlyController.threads model.cameraState |> ThreadPool.map Camera
    if model.animationEnabled then
        ThreadPool.union cameraController (ThreadPool.add "timeroida" (time()) ThreadPool.empty)
    else cameraController

let initLineThickness = 
  {
    min    = 0.0
    max    = 100.0
    step   = 1.0
    format = "{0:0.0}"
    value  = 2.0
  }

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState      = initialCamera
               trafo            = Trafo3d.Identity
               animationEnabled = true
               thickness        = initLineThickness
            }
        update = update 
        view = view
    }
