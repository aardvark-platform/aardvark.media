namespace UI.Composed

module Drawing =
    open Newtonsoft.Json
    open PRo3DModels
    open Aardvark.Base
    open Aardvark.Application
    open Aardvark.UI

    open System
    
    open Aardvark.Base.Geometry
    open Aardvark.Base.Incremental
    open Aardvark.Base.Incremental.Operators
    open Aardvark.Base.Rendering
    open Aardvark.Application
    open Aardvark.SceneGraph
    open Aardvark.UI
    open Aardvark.UI.Primitives
    open Aardvark.Rendering.Text

    open PRo3DModels

    open Aardvark.SceneGraph.SgPrimitives
    open Aardvark.SceneGraph.FShadeSceneGraph

    type Action =
        | SetSemantic      of Semantic
        | SetGeometry      of Geometry
        | SetProjection    of Projection
        | SetExportPath    of string
        | Move             of V3d
        | Exit    
        | AddPoint         of V3d
        | KeyDown of key : Keys
        | KeyUp of key : Keys      
        | Export

    let finishAndAppend (model : DrawingModel) = 
        let anns = match model.working with
                            | Some w -> model.annotations |> PList.append w
                            | None -> model.annotations
        { model with working = None; annotations = anns }

    let update (model : DrawingModel) (act : Action) =
        match (act, model.draw) with
            | KeyDown Keys.LeftCtrl, _ ->                     
                    { model with draw = true }
            | KeyUp Keys.LeftCtrl, _ -> 
                    {model with draw = false; hoverPosition = None }
            | Move p, true -> 
                    { model with hoverPosition = Some (Trafo3d.Translation p) }
            | AddPoint p, true -> 
                    let working = 
                        match model.working with
                                | Some w ->                                     
                                    { w with points = w.points |> PList.append p }
                                | None ->                                     
                                    { (Annotation.make model.projection model.geometry model.semantic) with points = PList.ofList [p]}//add annotation states

                    let model = { model with working = Some working }

                    let model = match (working.geometry, (working.points |> PList.count)) with
                                    | Geometry.Point, 1 -> model |> finishAndAppend
                                    | Geometry.Line, 2 -> model |> finishAndAppend
                                    | _ -> model

                    model                 
                    
            | KeyDown Keys.Enter, _ -> 
                    model |> finishAndAppend
            | Exit, _ -> 
                    { model with hoverPosition = None }
            | SetSemantic mode, _ ->
                    let model =
                        match mode with
                            | Semantic.GrainSize -> { model with geometry = Geometry.Line }
                            | _ -> model

                    {model with semantic = mode }
            | SetGeometry mode, _ ->
                    { model with geometry = mode }
            | SetProjection mode, _ ->
                    { model with projection = mode }
            | KeyDown Keys.D0, _ -> 
                    {model with semantic = Semantic.Horizon0 }               
            | KeyDown Keys.D1, _ -> 
                    {model with semantic = Semantic.Horizon1 }               
            | KeyDown Keys.D2, _ -> 
                    {model with semantic = Semantic.Horizon2 }               
            | KeyDown Keys.D3, _ -> 
                    {model with semantic = Semantic.Horizon3 }               
            | KeyDown Keys.D4, _ -> 
                    {model with semantic = Semantic.Horizon4 }               
            | SetExportPath s, _ ->
                    { model with exportPath = s }
            | Export, _ ->
                    //let path = Path.combine([model.exportPath; "drawing.json"])
                    //printf "Writing %i annotations to %s" (model.annotations |> PList.count) path
                    //let json = model.annotations |> PList.map JsonTypes.ofAnnotation |> JsonConvert.SerializeObject
                    //Serialization.writeToFile path json 
                    
                    model
            | _ -> model

    module UI =
        open Aardvark.Base.Incremental

        let viewAnnotationTools (model:MDrawingModel)=  
            Html.SemUi.accordion "Annotation Tools" "Write" true [
                Html.table [                            
                    Html.row "Text:"        [Html.SemUi.textBox  model.exportPath SetExportPath ]
                    Html.row "Geometry:"    [Html.SemUi.dropDown model.geometry   SetGeometry]
                    Html.row "Projections:" [Html.SemUi.dropDown model.projection SetProjection]
                    Html.row "Semantic:"    [Html.SemUi.dropDown model.semantic   SetSemantic]
                ]                    
            ]

        let viewAnnotations (model:MDrawingModel) = 
            Html.SemUi.accordion "Annotations" "File Outline" true [
                Incremental.div 
                    (AttributeMap.ofList [clazz "ui divided list"]) (
                            
                        alist {                                                                     
                            for a in model.annotations do 
                                        
                                let! sem = a.semantic
                                let c = Annotation.color.[int sem]

                                let bgc = sprintf "background: %s" (Html.ofC4b c)
                                    
                                yield div [clazz "item"; style bgc] [
                                        i [clazz "medium File Outline middle aligned icon"][]
                                        text (a.geometry.ToString())
                                ]                                                                    
                        }     
                )
            ]
    module Sg =        

        let computeScale (view : IMod<CameraView>)(p:IMod<V3d>)(size:float) =
            adaptive {
                let! p = p
                let! v = view
                let distV = p - v.Location
                let distF = V3d.Dot(v.Forward, distV)
                return distF * size / 800.0 //needs hfov at this point
            }

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
            Sg.sphere' 8 (new C4b(247,127,90)) 20.0
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.requirePicking
                |> Sg.noEvents 
                    |> Sg.withEvents [
                        Sg.onMouseMove (fun p -> (Action.Move p))
                        Sg.onClick(fun p -> Action.AddPoint p)
                        Sg.onLeave (fun _ -> Action.Exit)
                    ]  
                |> Sg.onOff (Mod.constant true)
              //  |> Sg.map DrawingMessage

        let edgeLines (close : bool) (points : alist<V3d>) =
            
            points |> AList.toMod |> Mod.map (fun l ->
                let list = PList.toList l
                let head = list |> List.tryHead
                    
                match head with
                    | Some h -> if close then list @ [h] else list
                                    |> List.pairwise
                                    |> List.map (fun (a,b) -> new Line3d(a,b))
                                    |> List.toArray
                    | None -> [||]                         
            )
            
        let brush (hovered : IMod<Trafo3d option>) = 
            let trafo =
                hovered |> Mod.map (function o -> match o with 
                                                    | Some t-> t
                                                    | None -> Trafo3d.Scale(V3d.Zero))

            mkISg (Mod.constant C4b.Red) (Mod.constant 0.05) trafo
       
        let dots (points : alist<V3d>) (color : IMod<C4b>) (view : IMod<CameraView>) =            
            
            aset {
                for p in points |> ASet.ofAList do
                    yield mkISg color (computeScale view (Mod.constant p) 5.0) (Mod.constant (Trafo3d.Translation(p)))
            } 
            |> Sg.set
           
        let lines (points : alist<V3d>) (color : IMod<C4b>) (width : IMod<float>) = 
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
   
        let annotation (anno : IMod<Option<MAnnotation>>)(view : IMod<CameraView>) = 
            //alist builder?
            let points = 
                anno |> AList.bind (fun o -> 
                    match o with
                        | Some a -> a.points
                        | None -> AList.empty
                )    
                
            let withDefault (m : IMod<Option<'a>>) (f : 'a -> IMod<'b>) (defaultValue : 'b) = 
                let defaultValue = defaultValue |> Mod.constant
                m |> Mod.bind (function | None -> defaultValue | Some v -> f v)

            let color = 
                withDefault anno (fun a -> a.color) C4b.VRVisGreen

            let thickness = 
                anno |> Mod.bind (function o -> match o with
                                                | Some a -> a.thickness.value
                                                | None -> Mod.constant 1.0)

            [lines points color thickness; dots points color view]

        let annotation' (anno : MAnnotation)(view : IMod<CameraView>) =             
            [lines anno.points anno.color anno.thickness.value; 
             dots anno.points anno.color view] 
            |> ASet.ofList

        let view (model:MDrawingModel) (cam:IMod<CameraView>) =        
            // order is irrelevant for rendering. change list to set,
            // since set provides more degrees of freedom for the compiler
            let annoSet = ASet.ofAList model.annotations 

            let annotations =
                aset {
                    for a in annoSet do
                        yield! annotation' a cam
                } |> Sg.set
                                

            [canvas; brush model.hoverPosition; annotations] @ annotation model.working cam
            |> Sg.ofList