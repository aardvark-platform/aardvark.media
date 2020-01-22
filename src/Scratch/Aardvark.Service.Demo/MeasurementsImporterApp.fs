namespace UI.Composed

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Rendering.Text

open PRo3DModels

 open Aardvark.SceneGraph.SgPrimitives
 open Aardvark.SceneGraph.FShadeSceneGraph

module DialogUtils =
    open System.Windows.Forms
    open Microsoft.WindowsAPICodePack.Dialogs
  

    let openFileDialog (w : Form) =
        let mutable r = Unchecked.defaultof<_>
        w.Invoke(Action(fun _ -> 
            let dialog = new OpenFileDialog()
            if dialog.ShowDialog() = DialogResult.OK then
                r <- dialog.FileName
            else r <- ""
        )) |> ignore
        r

module MeasurementsImporterApp =
    open System.Xml.Linq
    open System.Xml
    open PRo3DModels.Mutable.MeasurementsImporterAppModel.Lens
    
    let xname s = XName.Get(s)

    let thickness (a:XElement) = {
        value = (float)(a.Element(xname "object").Element(xname "LineThickness").Value)
        min = 0.5
        max = 10.0
        step = 0.1
        format = ""
        }

    let getStyle (a:XElement) : Style = {
        color = C4b.White
        thickness = thickness a
        }

    let parsePointsLevelOne (points:XElement) = points.Value.NestedBracketSplitLevelOne()
                                                    |> Seq.map (fun x -> V3d.Parse(x))
                                                    |> IndexList.ofSeq
      

    let parsePointsLevelZero (points:XElement) = points.Value.NestedBracketSplit(0) 
                                                    |> Seq.map (fun x -> V3d.Parse(x))
                                                    |> IndexList.ofSeq  
       
    
    let getPoints (points:XElement, aType:string) =
        let l = match points with
                    | null -> IndexList.empty 
                    | _-> match aType with
                            | "Exomars.Base.Geology.Point" ->  IndexList.ofList [V3d.Parse(points.Value)]
                            | "Exomars.Base.Geology.Line"  ->  parsePointsLevelOne points
                            | _ -> parsePointsLevelZero points
        l
      
                        
    let parseSegments (segments:XElement) = segments.Elements(xname "V3d_Array")
                                                |> Seq.map (fun x -> parsePointsLevelZero x)
                                                |> IndexList.ofSeq

    let parseSegment (seg:XElement) =  IndexList.ofList [parsePointsLevelZero seg]
        

    let getSegments (m:XElement, aType:string) = 
        let segments = match aType with
                        |"Exomars.Base.Geology.Line"            -> let segment = m.Element(xname "Segment")
                                                                   match segment.FirstAttribute with
                                                                    | null -> IndexList.empty
                                                                    | _ -> parseSegment segment
                        |"Exomars.Base.Geology.Polyline"   
                        |"Exomars.Base.Geology.DipAndStrike"    -> let segments = m.Element(xname "Segments")
                                                                   match segments with
                                                                    | null -> IndexList.empty
                                                                    | _ -> parseSegments segments
                        |_ -> IndexList.empty
        segments

    let getGeometry (aType:string, closed:bool) = 
        let geometry = if closed then Geometry.Polygon
                        else match aType with
                                |"Exomars.Base.Geology.Point"           -> Geometry.Point
                                |"Exomars.Base.Geology.Line"            -> Geometry.Line
                                |"Exomars.Base.Geology.Polyline"        -> Geometry.Polyline
                                |"Exomars.Base.Geology.DipAndStrike"    -> Geometry.DnS
                                |_ -> Geometry.Undefined
        geometry

    let getAnnotation (m:XElement) = 
        let anType = (m.Attribute(xname "type").Value.ToString().Split ',').[0]
        let closed = match m.Element(xname "Closed") with
                        | null -> false
                        | _ -> m.Element(xname "Closed").Value.ToBool()
        let style = (getStyle (m.Element(xname "LineAttributes")))
        let an = {
                geometry = (getGeometry (anType, closed)) 
                projection = Projection.Linear
                semantic = Semantic.Horizon0
                points = (getPoints (m.Element(xname "Points"), anType))
                segments = (getSegments (m, anType))
                color = C4b.White ///style.color
                thickness = style.thickness
                visible = true 
                text = ""
        }
        an
        

    type XmlReader with
    /// Returns a lazy sequence of XElements matching a given name.
        member reader.StreamElements(name, ?namespaceURI) =
            let readOp =
                match namespaceURI with
                | None    -> fun () -> reader.ReadToFollowing(name)
                | Some ns -> fun () -> reader.ReadToFollowing(name, ns)
            seq {
                while readOp() do
                    match XElement.ReadFrom reader with
                    | :? XElement as el -> yield el
                    | _ -> ()
            }

    let startImporter (path:string) =
        let reader = XmlReader.Create path
        let measurements = reader.StreamElements("Measurements").Elements(xname "object")
        let annotations = measurements 
                            |> Seq.map (fun x -> getAnnotation x)
                            |> IndexList.ofSeq  
        annotations


    type Action =
        | CameraMessage    of ArcBallController.Message
        | Move of V3d
        | Exit      
        | SetPath   of string
        | Import    of string
        | OpenFolder
        
    let update (f : System.Windows.Forms.Form) (model : MeasurementsImporterAppModel) (act : Action) =
        match act with
            | CameraMessage m  -> { model with measurementsCamera = ArcBallController.update model.measurementsCamera m }  
            | Move p           -> { model with measurementsHoverPosition = Some (Trafo3d.Translation p) }
            | SetPath s        -> { model with scenePath =  s }
            | Import s         -> { model with annotations = startImporter s }
            | OpenFolder       -> { model with scenePath = DialogUtils.openFileDialog f}
            | Exit             -> { model with measurementsHoverPosition = None }
  

    module Draw =

        let computeScale (view : aval<CameraView>)(p:aval<V3d>)(size:float) =        
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
        
        let sphereCanvas =             
            Sg.sphere' 8 (new C4b(247,127,90)) 20.0
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.requirePicking
                |> Sg.noEvents 
                    |> Sg.withEvents [
                        Sg.onMouseMove (fun p -> Move p)
                        Sg.onLeave (fun _ -> Exit)
                    ]  
                |> Sg.onOff (AVal.constant true)

        let boxCanvas =  
            let b = new Box3d( V3d(-2.0,-0.5,-2.0), V3d(2.0,0.5,2.0) )                                               
            Sg.box (AVal.constant C4b.White) (AVal.constant b)
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.requirePicking
                |> Sg.noEvents 
                    |> Sg.withEvents [
                        Sg.onMouseMove (fun p -> Move p)
                        Sg.onLeave (fun _ -> Exit)
                    ]    


        let edgeLines (close : bool) (points : alist<V3d>) =
            
            points |> AList.toMod |> AVal.map (fun l ->
                let list = IndexList.toList l
                let head = list |> List.tryHead
                    
                match head with
                    | Some h -> if close then list @ [h] else list
                                    |> List.pairwise
                                    |> List.map (fun (a,b) -> new Line3d(a,b))
                                    |> List.toArray
                    | None -> [||]                         
            )
            
        //let brush (hovered : aval<Trafo3d option>) = 
        //    let trafo =
        //        hovered |> AVal.map (function o -> match o with 
        //                                            | Some t-> t
        //                                            | None -> Trafo3d.Scale(V3d.Zero))

        //    mkISg (AVal.constant C4b.Red) (AVal.constant 0.05) trafo
       
        let dots (points : alist<V3d>) (color : aval<C4b>) (view : aval<CameraView>) =            
            
            aset {
                for p in points |> ASet.ofAList do
                    yield mkISg color (computeScale view (AVal.constant p) 5.0) (AVal.constant (Trafo3d.Translation(p)))
            } 
            |> Sg.set
           
        let lines (points : alist<V3d>) (color : aval<C4b>) (width : aval<float>) = 
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
                |> Sg.depthTest (AVal.constant DepthTestMode.None)
   
        //let annotation (anno : aval<Option<MAnnotation>>)(view : aval<CameraView>) = 
        //    //alist builder?
        //    let points = 
        //        anno |> AList.bind (fun o -> 
        //            match o with
        //                | Some a -> a.points
        //                | None -> AList.empty
        //        )    
                
            //let withDefault (m : aval<Option<'a>>) (f : 'a -> aval<'b>) (defaultValue : 'b) = 
            //    let defaultValue = defaultValue |> AVal.constant
            //    m |> AVal.bind (function | None -> defaultValue | Some v -> f v)

            //let color = 
            //    withDefault anno (fun a -> a.color) C4b.VRVisGreen

            //let thickness = 
            //    anno |> AVal.bind (function o -> match o with
            //                                    | Some a -> a.thickness.value
            //                                    | None -> AVal.constant 1.0)

            //[lines points color thickness; dots points color view]

        let isEmpty l = l |> AList.toMod |> AVal.map (fun a -> (IndexList.count a) = 0)
        let IAdaptiveValueTrue = AVal.constant true
        let IAdaptiveValueFalse = AVal.constant false

        let annotation' (anno : MAnnotation)(view : aval<CameraView>) = 
            let count = anno.segments |> AList.toMod |> (AVal.map IndexList.count)
            let c = AVal.force count
            let points = match c with       
                            | 0 -> anno.points
                            | _ -> anno.segments |> AList.concat
                               
            [lines points anno.color anno.thickness.value; 
                dots anno.points anno.color view] 
            |> ASet.ofList
            

    let view (model : MMeasurementsImporterAppModel) =
                    
        let frustum =
            AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
        require (Html.semui) (
            body [clazz "ui"; style "background: #1B1C1E"] [
                div [] [
                    ArcBallController.controlledControl model.measurementsCamera CameraMessage frustum
                        (AttributeMap.ofList [
                                    attribute "style" "width:65%; height: 100%; float: left;"]
                        )
                        (
                            let view = model.measurementsCamera.view
                        
                            // order is irrelevant for rendering. change list to set,
                            // since set provides more degrees of freedom for the compiler
                            let annoSet = ASet.ofAList model.annotations 

                            let annotations =
                                aset {
                                    for a in annoSet do
                                        yield! Draw.annotation' a view
                                } |> Sg.set
                                

                            [Draw.boxCanvas; annotations] //@ DrawingApp.Draw.annotation model.working view
                                |> Sg.ofList
                                |> Sg.fillMode model.measurementsRendering.fillMode
                                |> Sg.cullMode model.measurementsRendering.cullMode                                                                                           
                        )                                        
                ]

                 //Html.Layout.horizontal [
                        //    Html.Layout.boxH [ 

                div [style "width:35%; height: 100%; float:right;"] [

                    div [clazz "ui list"] [
                        // surface path
                        div [clazz "item"] [label [] [text "Scene Path:"]]
                        div [clazz "item"] [Html.SemUi.textBox model.scenePath SetPath]
                        div [clazz "ui button"; onMouseClick (fun _ -> OpenFolder )] [text "..."]
                        div [clazz "ui button"; onMouseClick (fun _ -> Import (AVal.force model.scenePath))] [text "import"]
                    
                    ]
                    
                   // div [style "overflow-Y: scroll" ] [
                    Html.SemUi.accordion "Annotations" "File Outline" true [
                        Incremental.div 
                            (AttributeMap.ofList [clazz "ui divided list"]) (
                            
                                alist {                                                                     
                                    for a in model.annotations do 
                                        
                                        let c = a.color // Annotation.color.[int sem]

                                        let bgc = sprintf "background: %s" (Html.ofC4b (AVal.force c))
                                    
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

    let initial : MeasurementsImporterAppModel =
        {
        measurementsCamera           = { ArcBallController.initial with view = CameraView.lookAt (23.0 * V3d.OIO) V3d.Zero V3d.OOI}
        measurementsRendering        = InitValues.rendering

        measurementsHoverPosition = None

        scenePath = @"."
        annotations = IndexList.empty
        }
    
    let app (f : System.Windows.Forms.Form) : App<MeasurementsImporterAppModel,MMeasurementsImporterAppModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.measurementsCamera |> ThreadPool.map CameraMessage
            initial = initial
            update = update f
            view = view
        }
    
    let start (f : System.Windows.Forms.Form) = App.start (app f)
