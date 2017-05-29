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
  

    //let openFolderDialog (w : Form) =
    //    let mutable r = Unchecked.defaultof<_>
    //    w.Invoke(Action(fun _ -> 
    //        let dialog = new CommonOpenFileDialog()
    //      //  dialog.InitialDirectory <- "C:\\Users"
    //        dialog.IsFolderPicker <- true
    //        if dialog.ShowDialog() = CommonFileDialogResult.Ok then
    //            r <- dialog.FileName
    //        else r <- ""
    //    )) |> ignore
    //    r

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
    
    let xname s = XName.Get(s)

    let thickness (a:XElement) = {
        value = (float)(a.Element(xname "object").Element(xname "LineThickness").Value)
        min = 0.5
        max = 10.0
        step = 0.1
        format = ""
        }

    let getStyle (a:XElement) = {
        color = C4b.Parse(a.Element(xname "object").Element(xname "Color").Value)
        thickness = thickness a
        }

    let parsePointsLevelOne (points:XElement) =
        let l = PList.empty
        let s = points.Value.NestedBracketSplitLevelOne()
        for p in s do
            l |> PList.append (V3d.Parse(p)) |> ignore
        l

    let parsePointsLevelZero (points:XElement) =   
        let l = PList.empty
        let s = points.Value.NestedBracketSplit(0)
        for p in s do
             l |> PList.append (V3d.Parse(p)) |> ignore
        l 
    
    let getPoints (points:XElement, aType:string) =
        let l = match points with
                    | null -> PList.empty 
                    | _-> match aType with
                            | "Exomars.Base.Geology.Point" ->  PList.ofList [V3d.Parse(points.Value)]
                            | "Exomars.Base.Geology.Line"  ->  parsePointsLevelOne points
                            | _ -> parsePointsLevelZero points
        l
      
                        
    let parseSegments (segments:XElement) =
        let segs = segments.Elements(xname "V3d_Array")
        let segments = PList.empty //new List<List<V3d>>()
        for s in segs do
            let p = parsePointsLevelZero s
            segments|> PList.append p |> ignore
        segments

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
                color = style.color
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
        let annotations = PList.empty
        for m in measurements do
            annotations |> PList.append (getAnnotation m)
            |> ignore
        annotations


    type Action =
        | CameraMessage    of ArcBallController.Message
        | SetPath   of string
        | Import    of string
        //| OpenFolder
        
    let update (model : MeasurementsImporterAppModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                    { model with measurementsCamera = ArcBallController.update model.measurementsCamera m }       
            | SetPath s             -> { model with scenePath =  s }
            | Import s              -> { model with annotations = startImporter s }
           // | OpenFolder            -> { model with scenePath = DialogUtils.openFileDialog f}
  

    let view (model : MMeasurementsImporterAppModel) =
        div [clazz "ui container"] [
            div [clazz "ui list"] [
                // surface path
                div [clazz "item"] [label [] [text "Scene Path:"]]
                div [clazz "item"] [Html.SemUi.textBox model.scenePath SetPath]
                //div [clazz "ui button"; onMouseClick (fun _ -> OpenFolder )] [text "..."]
                div [clazz "ui button"; onMouseClick (fun _ -> Import (Mod.force model.scenePath))] [text "import"]
                    
            ]
            
          
            
        ]

    let initial : MeasurementsImporterAppModel =
        {
        measurementsCamera           = { ArcBallController.initial with view = CameraView.lookAt (23.0 * V3d.OIO) V3d.Zero V3d.OOI}
        rendering        = InitValues.rendering

        scenePath = @"."
        annotations = PList.empty
        }
    
    let app : App<MeasurementsImporterAppModel,MMeasurementsImporterAppModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.measurementsCamera |> ThreadPool.map CameraMessage
            initial = initial
            update = update //(new  Windows.Forms.Form(Width = 1024, Height = 768))
            view = view
        }
    
    let start () = App.start app
