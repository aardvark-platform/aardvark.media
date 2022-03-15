namespace Niobe

open System

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph

open Aardvark.Application
open Aardvark.UI
open Aardvark.UI.Primitives
open Niobe.Sketching

module StencilAreaMasking =

    let writeZFailFront, writeZFailBack =
        { StencilMode.None with DepthFail = StencilOperation.DecrementWrap },
        { StencilMode.None with DepthFail = StencilOperation.IncrementWrap }

      //let compare = new StencilFunction(StencilCompareFunction.Always, 0, 0xffu)
      //let front   = new StencilOperation(StencilOperationFunction.Keep, StencilOperationFunction.DecrementWrap, StencilOperationFunction.Keep)
      //let back    = new StencilOperation(StencilOperationFunction.Keep, StencilOperationFunction.IncrementWrap, StencilOperationFunction.Keep)
      //StencilMode(front, compare, back, compare)

  //let writeZPass =
  //    let compare = new StencilFunction(StencilCompareFunction.Always, 0, 0xffu)
  //    let front   = new StencilOperation(StencilOperationFunction.IncrementWrap, StencilOperationFunction.Keep, StencilOperationFunction.Keep)
  //    let back    = new StencilOperation(StencilOperationFunction.DecrementWrap, StencilOperationFunction.Keep, StencilOperationFunction.Keep)
  //    StencilMode(front, compare, back, compare)

    let readMaskAndReset =
        { StencilMode.None with
            Pass = StencilOperation.Zero
            Fail = StencilOperation.Zero
            DepthFail = StencilOperation.Zero
            Comparison = ComparisonFunction.NotEqual }
      //let compare = new StencilFunction(StencilCompareFunction.NotEqual, 0, 0xffu)
      //let operation = new StencilOperation(StencilOperationFunction.Zero, StencilOperationFunction.Zero, StencilOperationFunction.Zero)
      //StencilMode(operation, compare)

  // Front = CounterClockwise
  // Back = Clockwise

    let maskPass = RenderPass.after "mask" RenderPassOrder.Arbitrary RenderPass.main
    let areaPass = RenderPass.after "area" RenderPassOrder.Arbitrary maskPass

    let maskSG sg = 
        sg
            |> Sg.pass maskPass
            |> Sg.stencilModes' writeZFailFront writeZFailBack
            |> Sg.cullMode (AVal.constant (CullMode.None))
            |> Sg.writeBuffers' (Set.ofList [WriteBuffer.Stencil])

    let fillSG sg =
        sg
        |> Sg.pass areaPass
        |> Sg.stencilMode (AVal.constant (readMaskAndReset))
        //|> Sg.cullMode (AVal.constant CullMode.CounterClockwise)  // for zpass -> backface-culling
        //|> Sg.depthTest (AVal.constant DepthTestMode.Less)        // for zpass -> active depth-test
        |> Sg.cullMode (AVal.constant CullMode.None)
        |> Sg.depthTest (AVal.constant DepthTest.None)
        |> Sg.blendMode (AVal.constant BlendMode.Blend)
        |> Sg.writeBuffers' (Set.ofList [WriteBuffer.Color DefaultSemantic.Colors; WriteBuffer.Stencil])

    let stencilAreaSG sg =
        [
            maskSG sg   // one pass by using EXT_stencil_two_side :)
            fillSG sg
        ] |> Sg.ofList

module App = 
  open Niobe.Utilities
     
  let update (model : Model) (msg : Message) =
      match msg with        
        | ToggleShadowVolumeVis -> {model with shadowVolumeVis = not model.shadowVolumeVis }
        | ToggleLineVis -> {model with showLines = not model.showLines }
        | Camera m when model.picking = false ->
          { model with cameraState = FreeFlyController.update model.cameraState m; }
        | UpdateDockConfig cfg ->
          { model with dockConfig = cfg }
        | SketchingMessage a ->
          { model with sketching = SketchingApp.update model.sketching a }
        | HitSurface p when model.picking = true ->
          Log.line "hit %A" p
          { model with sketching = SketchingApp.update model.sketching (AddPoint p) }          
        | Message.KeyDown k -> 
          match k with
          | Keys.LeftCtrl -> 
            Log.line "start picking"
            { model with picking = true }
          | Keys.Enter -> { model with sketching = SketchingApp.update model.sketching (ClosePolygon) }
          | Keys.Space ->    
            Log.line "[App] saving camstate"
            model.cameraState.view |> Camera.toCameraStateLean |> Serialization.save ".\camstate" |> ignore
            model.sketching.working |> Serialization.save ".\working" |> ignore
            model
          | _ -> model
        | Message.KeyUp k ->
          match k with
          | Keys.LeftCtrl -> 
            Log.line "stop picking"
            { model with picking = false }
          | _ -> model
        | _ -> model
  
  let view (model : AdaptiveModel) =

    let sceneSG = 
      //Sg.box (AVal.constant C4b.Red) (AVal.constant Box3d.Unit)
      [
          Sg.sphere' 8 C4b.DarkBlue 0.70 |> Sg.translate 0.5 0.5 0.1
          Sg.sphere' 8 C4b.DarkCyan 0.65 |> Sg.translate -0.5 0.5 0.0
          Sg.sphere' 8 C4b.DarkGreen 0.75 |> Sg.translate 0.5 -0.5 -0.1
          Sg.sphere' 8 C4b.DarkMagenta 0.55 |> Sg.translate -0.5 -0.5 0.0
          //Sg.box' C4b.DarkMagenta Box3d.Unit |> Sg.noEvents
      ] |> Sg.ofSeq
        |> Sg.shader {
              do! DefaultSurfaces.trafo
              do! DefaultSurfaces.simpleLighting
          }  
        |> Sg.depthTest (AVal.constant DepthTest.Less)   // depth test active
        |> Sg.pass RenderPass.main
        |> Sg.requirePicking
            |> Sg.noEvents
            |> Sg.withEvents [
                Sg.onClick HitSurface
            ]

    let currentBrush = 
      SketchingApp.currentBrushSg model.sketching 
        |> Sg.map SketchingMessage 
        |> Sg.onOff model.showLines

    let finishedBrushes = 
      SketchingApp.finishedBrushSg model.sketching 
        |> Sg.map SketchingMessage 

    let shadowVolumeDebugVis brushes =
      brushes
        |> Sg.depthTest (AVal.constant DepthTest.Less)
        |> Sg.cullMode (AVal.constant (CullMode.Back)) // only for testing
        |> Sg.onOff model.shadowVolumeVis
        |> Sg.pass RenderPass.main

    let viewSg = 
      [
        sceneSG
        currentBrush 
        finishedBrushes |> StencilAreaMasking.stencilAreaSG 
        finishedBrushes |> shadowVolumeDebugVis
      ] |> Sg.ofList
    
    let renderControl =
     FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.01 1000.0 1.0 |> AVal.constant) 
       (AttributeMap.ofList [ 
         style "width: 100%; height:100%"; 
         attribute "showFPS" "true";       // optional, default is false
         attribute "useMapping" "true"
         attribute "data-renderalways" "false"
         attribute "data-samples" "4"
         onKeyDown (Message.KeyDown)
         onKeyUp (Message.KeyUp)
         //onBlur (fun _ -> Camera FreeFlyController.Message.Blur)
       ]) 
       (viewSg)
                        
    let dependencies = 
      Html.semui @ [        
        { name = "spectrum.js";  url = "spectrum.js";  kind = Script     }
        { name = "spectrum.css";  url = "spectrum.css";  kind = Stylesheet     }
      ] 

    page (fun request ->
      match Map.tryFind "page" request.queryParams with
      | Some "render" ->
        require Html.semui ( // we use semantic ui for our gui. the require function loads semui stuff such as stylesheets and scripts
            div [clazz "ui"; style "background: #1B1C1E"] [renderControl]
        )
      | Some "controls" -> 
        require dependencies (
          body [style "width: 100%; height:100%; background: transparent; overflow-y:visible"] [
             div[style "color:white; margin: 5px 15px 5px 5px"] [
               h3 [] [text "NIOBE"]
               p [] [text "unified sketching"]
               p [] [text "Hold Ctrl-Left to add Point"]
               p [] [text "Press Enter to close Polygon"]
               p [] [text "ShadowVolumeVisualization "; Html.SemUi.iconCheckBox model.shadowVolumeVis ToggleShadowVolumeVis]
               p [] [text "Show Lines "; Html.SemUi.iconCheckBox model.showLines ToggleLineVis]
               button[onClick (fun _ -> SketchingAction.Undo); style "background:white; color:black"][text "Undo"] |> UI.map SketchingMessage
               button[onClick (fun _ -> SketchingAction.Redo); style "background:white; color:black"][text "Redo"] |> UI.map SketchingMessage
               //button[onClick (fun _ -> SketchingAction.CreateShadowPolygon); style "background:white; color:black"][text "Shadow Volume"] |> UI.map SketchingMessage
               SketchingApp.viewGui model.sketching |> UI.map SketchingMessage
             ]
          ]
        )
      | Some other -> 
        let msg = sprintf "Unknown page: %A" other
        body [] [
            div [style "color: white; font-size: large; background-color: red; width: 100%; height: 100%"] [text msg]
        ]  
      | None -> 
        model.dockConfig
          |> docking [
            style "width:100%; height:100%; background:#F00"
            onLayoutChanged UpdateDockConfig ]
      )
   
  let threads (model : Model) = 
      ThreadPool.empty
  
  let restoreCamState : CameraControllerState =
    if System.IO.File.Exists ".\camstate" then          
      Log.warn "[App] restoring camstate"
      let csLight : CameraStateLean = Serialization.loadAs ".\camstate"
      { FreeFlyController.initial with view = csLight |> Camera.fromCameraStateLean }
    else 
      { FreeFlyController.initial with view = CameraView.lookAt (V3d.III * 2.0) V3d.OOO V3d.OOI; }      
  
  let cam = restoreCamState

  let sketching = 
    if System.IO.File.Exists ".\working" then          
      Log.warn "[App] restoring working"
      let working : option<Brush> = Serialization.loadAs ".\working"
      { Sketching.Initial.sketchingModel with working = working }
    else 
      Sketching.Initial.sketchingModel    

  let initialModel : Model = 
    { 
      cameraState        = cam
      threads            = FreeFlyController.threads cam |> ThreadPool.map Camera        
      dockConfig         =
        config {
            content (
                horizontal 10.0 [
                    element { id "render"; title "Render View"; weight 7.0 }
                    element { id "controls"; title "Controls"; weight 3.0 }                                                    
                ]
            )
            appName "OpcSelectionViewer"
            useCachedConfig true
        }
      picking = false
      sketching = sketching
      shadowVolumeVis = false
      showLines = true
    }
  
  let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
      {
          unpersist = Unpersist.instance     
          threads = threads 
          initial = initialModel
          update = update 
          view = view
      }
