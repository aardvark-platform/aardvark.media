namespace UI.Composed

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Rendering.Text

open PRo3DModels

 open Aardvark.SceneGraph.SgPrimitives
 open Aardvark.SceneGraph.FShadeSceneGraph

 module Serialization =
    open MBrace.FsPickler
    open System.IO
    let binarySerializer = FsPickler.CreateBinarySerializer()
    
    let save (model : DrawingAppModel) path = 
        let arr = binarySerializer.Pickle model
        File.WriteAllBytes(path, arr);

    let load path : DrawingAppModel = 
        let arr = File.ReadAllBytes(path);
        let app = binarySerializer.UnPickle arr
        app

    let writeToFile path (contents : string) =
        System.IO.File.WriteAllText(path, contents)

module DrawingApp = 
    open Newtonsoft.Json
            
    type Action =
        | CameraMessage    of ArcBallController.Message
        | SetSemantic      of Semantic
        | SetGeometry      of Geometry
        | SetProjection    of Projection
        | SetExportPath    of string
        | Move of V3d
        | Exit      
        | AddPoint of V3d
        | KeyDown of key : Keys
        | KeyUp of key : Keys
        | Export
        | Save
        | Load
        | Clear
        
    
    let finishAndAppend (model : DrawingAppModel) = 
        let anns = match model.working with
                            | Some w -> model.annotations |> List.append [w]
                            | None -> model.annotations

        { model with working = None; annotations = anns }
        

    let update (model : DrawingAppModel) (act : Action) =
        match act, model.draw with
            | CameraMessage m, false -> 
                    { model with camera = ArcBallController.update model.camera m }                    
            | KeyDown Keys.LeftCtrl, _ -> 
                    { model with draw = true }
            | KeyUp Keys.LeftCtrl, _ -> 
                    { model with draw = false; hoverPosition = None }
            | Move p, true -> 
                    { model with hoverPosition = Some (Trafo3d.Translation p) }
            | AddPoint p, true -> 
                    let working = 
                        match model.working with
                                | Some w ->                                     
                                    { w with points = w.points |> List.append [p] }
                                | None -> 
                                    { (Annotation.make model.projection model.geometry model.semantic) with points = [p] }//add annotation states

                    let model = { model with working = Some working }

                    match (working.geometry, working.points.Length) with
                        | Geometry.Point, 1 -> model |> finishAndAppend
                        | Geometry.Line, 2 -> model |> finishAndAppend
                        | _ -> model
                    
            | KeyDown Keys.Enter, _ -> 
                    model |> finishAndAppend
            | Exit, _ -> 
                    { model with hoverPosition = None }

            | SetSemantic mode, _ ->
                    let model =
                        match mode with
                            | Semantic.GrainSize -> { model with geometry = Geometry.Line }
                            | _ -> model

                    { model with semantic = mode }
            | SetGeometry mode, _ ->
                    { model with geometry = mode }
            | SetProjection mode, _ ->
                    { model with projection = mode }
            | KeyDown Keys.D0, _ -> 
                    { model with semantic = Semantic.Horizon0 }
            | KeyDown Keys.D1, _ -> 
                    { model with semantic = Semantic.Horizon1 }
            | KeyDown Keys.D2, _ -> 
                    { model with semantic = Semantic.Horizon2 }
            | KeyDown Keys.D3, _ -> 
                    { model with semantic = Semantic.Horizon3 }
            | KeyDown Keys.D4, _ -> 
                    { model with semantic = Semantic.Horizon4 }
            | Export, _ ->
                    let path = Path.combine([model.exportPath; "drawing.json"])
                    printf "Writing %i annotations to %s" model.annotations.Length path
                    let json = model.annotations |> List.map JsonTypes.ofAnnotation |> JsonConvert.SerializeObject
                    Serialization.writeToFile path json 
                    
                    model
            | Save, _ -> 
                    Serialization.save model ".\drawing"
                    model
            | Load, _ -> 
                    Serialization.load ".\drawing"
            | Clear,_ ->
                    { model with annotations = [] }
            | SetExportPath s, _ ->
                    { model with exportPath = s }
            | _ -> model
            
            
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }
    
    module Draw =

        let computeScale (view : IMod<CameraView>)(p:V3d)(size:float) =        
            view 
                |> Mod.map (function v -> 
                                        let distV = p - v.Location
                                        let distF = V3d.Dot(v.Forward, distV)
                                        distF * size / 800.0) //needs hfov at this point

        let mkISg color size trafo =         
            Sg.sphere 5 color size 
                    |> Sg.shader {
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.vertexColor
                        do! DefaultSurfaces.simpleLighting
                    }
                    |> Sg.noEvents
                    |> Sg.trafo(trafo) 
        
        let canvas =  
            let b = new Box3d( V3d(-4.0,-1.0,-4.0), V3d(4.0,1.0,4.0) )          
            Sg.box (Mod.constant (C4b(252,132,95))) (Mod.constant b)
            //Sg.sphere' 8 (new C4b(254,217,118)) 2.0
            //IndexedGeometryPrimitives.quad' V3d.OOO V3d.IOO V3d.IOI V3d.OOI (C4b(254,217,118)) 
              //  |> Aardvark.SceneGraph.SgFSharp.Sg.ofIndexedGeometry                          
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                  //  do! DefaultSurfaces.simpleLighting
                }
                |> Sg.requirePicking
                |> Sg.noEvents 
                    |> Sg.withEvents [
                        Sg.onMouseMove (fun p -> Move p)
                        Sg.onClick(fun p -> AddPoint p)
                        Sg.onLeave (fun _ -> Exit)
                    ]  
                |> Sg.onOff (Mod.constant true)

        let edgeLines (close : bool) (points : IMod<list<V3d>>) =        
            points 
                |> Mod.map (
                    function k -> 
                                let head = k |> List.tryHead
                                match head with
                                        | Some h -> if close then k @ [h] else k
                                                        |> List.pairwise
                                                        |> List.map (fun (a,b) -> new Line3d(a,b)) 
                                                        |> Array.ofList                                                        
                                        | None -> [||])

        let brush (hovered : IMod<Trafo3d option>) = 
            let trafo =
                hovered |> Mod.map (function o -> match o with 
                                                    | Some t-> t
                                                    | None -> Trafo3d.Scale(V3d.Zero))

            mkISg (Mod.constant C4b.Red) (Mod.constant 0.05) trafo

        let dots (points : IMod<Points>) (color : IMod<C4b>) (view : IMod<CameraView>) =         
            points
                |> Mod.map(function ps -> ps |> List.map (function p -> mkISg color
                                                                              (computeScale view p 5.0)
                                                                              (Mod.constant (Trafo3d.Translation(p)))) 
                                             |> Sg.ofList)                                
                |> Sg.dynamic  
               // |> Sg.depthTest (Mod.constant DepthTestMode.None)
    
        let lines (points : IMod<Points>) (color : IMod<C4b>) (width : IMod<float>) = 
            edgeLines false points
                |> Sg.lines color
                |> Sg.effect [
                    toEffect DefaultSurfaces.trafo
                    toEffect DefaultSurfaces.vertexColor
                    toEffect DefaultSurfaces.thickLine                                
                    ] 
                |> Sg.noEvents
                |> Sg.uniform "LineWidth" width
                |> Sg.pass (RenderPass.after "lines" RenderPassOrder.Arbitrary RenderPass.main)
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
   
        let annotation (anno : IMod<Annotation option>)(view : IMod<CameraView>) = 
            let points = 
                anno |> Mod.map (function o -> match o with
                                                | Some a -> a.points
                                                | None -> [])
            let color = 
                anno |> Mod.map (function o -> match o with
                                                | Some a -> a.color
                                                | None -> C4b.VRVisGreen)

            let thickness = 
                anno |> Mod.map (function o -> match o with
                                                | Some a -> a.thickness.value
                                                | None -> 1.0)

            [lines points color thickness; dots points color view]

        let annotation' (anno : Annotation)(view : IMod<CameraView>) = 
            let points = Mod.constant anno.points            
            let color = Mod.constant anno.color
            let thickness = Mod.constant anno.thickness.value

            [lines points color thickness; dots points color view]           

    let view (model : MDrawingAppModel) =
                    
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                ArcBallController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                                onKeyDown (KeyDown)
                                onKeyUp (KeyUp)
                                attribute "style" "width:65%; height: 100%; float: left;"]
                    )
                    (
                        let view = model.camera.view

                        let annotations =
                            model.annotations 
                                |> Mod.map(function xs -> xs |> List.map(function a -> Draw.annotation' a view) 
                                                             |> List.concat 
                                                             |> Sg.ofList) 
                                |> Sg.dynamic

                        [Draw.canvas; Draw.brush model.hoverPosition; annotations] @
                         Draw.annotation model.working view
                            |> Sg.ofList
                            |> Sg.fillMode model.rendering.fillMode
                            |> Sg.cullMode model.rendering.cullMode                                                                                           
                )

                 //Html.Layout.horizontal [
                        //    Html.Layout.boxH [ 
                div [style "width:35%; height: 100%; float:right;"] [
                    
                    div [clazz "ui buttons inverted"] [
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Save)] [
                                    i [clazz "save icon"] [] ]
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Load)] [
                                    i [clazz "folder outline icon"] [] ]
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Clear)] [
                                    i [clazz "file outline icon"] [] ]
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Export)] [
                                    i [clazz "external icon"] [] ]
                    ]

                    Html.SemUi.accordion "Annotation Tools" "Write" true [
                        Html.table [                            
                            Html.row "Text:" [Html.SemUi.textBox model.exportPath SetExportPath ]
                            Html.row "Geometry:" [Html.SemUi.dropDown model.geometry SetGeometry]
                            Html.row "Projections:" [Html.SemUi.dropDown model.projection SetProjection]
                            Html.row "Semantic:" [Html.SemUi.dropDown model.semantic SetSemantic]
                        ]                    
                    ]

                   // div [style "overflow-Y: scroll" ] [
                    Html.SemUi.accordion "Annotations" "File Outline" true [
                        Incremental.div 
                            (AttributeMap.ofList [clazz "ui divided list"]) (
                            
                                alist {   
                                    let! annotations = model.annotations
                                
                                    for a in (annotations |> AList.ofList) do                                    
                                        let c = Annotation.color.[int a.semantic]

                                        let bgc = sprintf "background: %s" (Html.ofC4b c)
                                    
                                        yield div [clazz "item"; style bgc] [
                                                i [clazz "medium File Outline middle aligned icon"][]
                                                text (a.geometry.ToString())
                                        ]                                                                    
                                }     
                        )
                    ]
                   // ]
                ]

                
                        
            ]
        )

    let initial : DrawingAppModel =
        {
            camera           = { ArcBallController.initial with view = CameraView.lookAt (6.0 * V3d.OIO) V3d.Zero V3d.OOI}
            rendering        = InitValues.rendering
            hoverPosition = None
            draw = false            

            working = None
            projection = Projection.Viewpoint
            geometry = Geometry.Polyline
            semantic = Semantic.Horizon3

            annotations = []
            exportPath = @"."
        }

    let app : App<DrawingAppModel,MDrawingAppModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app

