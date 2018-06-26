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
                                                    |> PList.ofSeq
      

    let parsePointsLevelZero (points:XElement) = points.Value.NestedBracketSplit(0) 
                                                    |> Seq.map (fun x -> V3d.Parse(x))
                                                    |> PList.ofSeq  
       
    
    let getPoints (points:XElement, aType:string) =
        let l = match points with
                    | null -> PList.empty 
                    | _-> match aType with
                            | "Exomars.Base.Geology.Point" ->  PList.ofList [V3d.Parse(points.Value)]
                            | "Exomars.Base.Geology.Line"  ->  parsePointsLevelOne points
                            | _ -> parsePointsLevelZero points
        l
      
                        
    let parseSegments (segments:XElement) = segments.Elements(xname "V3d_Array")
                                                |> Seq.map (fun x -> parsePointsLevelZero x)
                                                |> PList.ofSeq

    let parseSegment (seg:XElement) =  PList.ofList [parsePointsLevelZero seg]
        

    let getSegments (m:XElement, aType:string) = 
        let segments = match aType with
                        |"Exomars.Base.Geology.Line"            -> let segment = m.Element(xname "Segment")
                                                                   match segment.FirstAttribute with
                                                                    | null -> PList.empty
                                                                    | _ -> parseSegment segment
                        |"Exomars.Base.Geology.Polyline"   
                        |"Exomars.Base.Geology.DipAndStrike"    -> let segments = m.Element(xname "Segments")
                                                                   match segments with
                                                                    | null -> PList.empty
                                                                    | _ -> parseSegments segments
                        |_ -> PList.empty
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
                            |> PList.ofSeq  
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
                |> Sg.onOff (Mod.constant true)

        let boxCanvas =  
            let b = new Box3d( V3d(-2.0,-0.5,-2.0), V3d(2.0,0.5,2.0) )                                               
            Sg.box (Mod.constant C4b.White) (Mod.constant b)
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
            
        //let brush (hovered : IMod<Trafo3d option>) = 
        //    let trafo =
        //        hovered |> Mod.map (function o -> match o with 
        //                                            | Some t-> t
        //                                            | None -> Trafo3d.Scale(V3d.Zero))

        //    mkISg (Mod.constant C4b.Red) (Mod.constant 0.05) trafo
       
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
   
        //let annotation (anno : IMod<Option<MAnnotation>>)(view : IMod<CameraView>) = 
        //    //alist builder?
        //    let points = 
        //        anno |> AList.bind (fun o -> 
        //            match o with
        //                | Some a -> a.points
        //                | None -> AList.empty
        //        )    
                
            //let withDefault (m : IMod<Option<'a>>) (f : 'a -> IMod<'b>) (defaultValue : 'b) = 
            //    let defaultValue = defaultValue |> Mod.constant
            //    m |> Mod.bind (function | None -> defaultValue | Some v -> f v)

            //let color = 
            //    withDefault anno (fun a -> a.color) C4b.VRVisGreen

            //let thickness = 
            //    anno |> Mod.bind (function o -> match o with
            //                                    | Some a -> a.thickness.value
            //                                    | None -> Mod.constant 1.0)

            //[lines points color thickness; dots points color view]

        let isEmpty l = l |> AList.toMod |> Mod.map (fun a -> (PList.count a) = 0)
        let IModTrue = Mod.constant true
        let IModFalse = Mod.constant false

        let annotation' (anno : MAnnotation)(view : IMod<CameraView>) = 
            let count = anno.segments |> AList.toMod |> (Mod.map PList.count)
            let c = Mod.force count
            let points = match c with       
                            | 0 -> anno.points
                            | _ -> anno.segments |> AList.concat
                               
            [lines points anno.color anno.thickness.value; 
                dots anno.points anno.color view] 
            |> ASet.ofList
            

    let view (model : MMeasurementsImporterAppModel) =
                    
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
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
                        div [clazz "ui button"; onMouseClick (fun _ -> Import (Mod.force model.scenePath))] [text "import"]
                    
                    ]
                    
                   // div [style "overflow-Y: scroll" ] [
                    Html.SemUi.accordion "Annotations" "File Outline" true [
                        Incremental.div 
                            (AttributeMap.ofList [clazz "ui divided list"]) (
                            
                                alist {                                                                     
                                    for a in model.annotations do 
                                        
                                        let c = a.color // Annotation.color.[int sem]

                                        let bgc = sprintf "background: %s" (Html.ofC4b (Mod.force c))
                                    
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
        annotations = PList.empty
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
