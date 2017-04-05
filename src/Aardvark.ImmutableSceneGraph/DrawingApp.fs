namespace Scratch

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Scratch.DomainTypes

open Aardvark.ImmutableSceneGraph
open Aardvark.Elmish
open Primitives

open Fablish
open Fable.Helpers.Virtualdom
open Fable.Helpers.Virtualdom.Html

module Serialization =
    open MBrace.FsPickler
    open System.IO
    let binarySerializer = FsPickler.CreateBinarySerializer()
    
    let save (model : DrawingApp.Drawing) path = 
        let arr = binarySerializer.Pickle model
        File.WriteAllBytes(path, arr);

    let load path : DrawingApp.Drawing = 
        let arr = File.ReadAllBytes(path);
        let app = binarySerializer.UnPickle arr
        app

module Styles =
    let standard : List<DrawingApp.Style> = 
        [
           { color = new C4b(33,113,181) ; thickness = DrawingApp.Default.thickness' 0.03 }
           { color = new C4b(107,174,214); thickness = DrawingApp.Default.thickness' 0.02 }
           { color = new C4b(189,215,231); thickness = DrawingApp.Default.thickness' 0.01 }
           { color = new C4b(239,243,255); thickness = DrawingApp.Default.thickness' 0.005 }
        ]   

module AnnotationPropertiesApp =
    type Action =                      
        | Set_Thickness of Numeric.Action
        | Set_Style     of Choice.Action
        | Set_Projection     of Choice.Action

    let update (env: Env<Action>) (model : DrawingApp.Annotation) (action : Action) =
        match action with            
            | Set_Thickness a -> { model with style = {model.style with thickness = Numeric.update env model.style.thickness a }}
            | Set_Style s -> 
                let style' = Choice.update env model.styleType s
                let index = Choice.index style'
                let style' = Styles.standard.[index]

                { model with style = style'; styleType = Choice.update env model.styleType s }
            | Set_Projection s ->                 
                { model with projection = Choice.update env model.projection s}

    //let table' = table [clazz "ui celled striped table unstackable"]
       
    let view (m : DrawingApp.Annotation) =
        let c = sprintf "%A" (Html.ofC4b m.style.color)
        div[] [                                                                    
            Html.table [
                Html.row "Type:"      [ Text  m.annType ]
                Html.row "Style:"     [ Choice.view m.styleType |> Html.map Set_Style ]
                Html.row "Projection:"[ Choice.view m.projection |> Html.map Set_Projection ]
                Html.row "Color:"     [ div [Style ["color", c]] [ Text c ]]
                Html.row "Thickness:" [ Numeric.view m.style.thickness |> Html.map Set_Thickness ]
            ]
        ]                       

module DrawingApp =

    module Annotations = 
        let tryGet (m:DrawingApp.Drawing) sn = 
            m.finished |> Seq.tryFind(fun x -> x.seqNumber = sn)

        let update (m : DrawingApp.Drawing) (ann : DrawingApp.Annotation) = 
            m.finished.AsList 
                |> List.updateIf (fun x -> x.seqNumber = ann.seqNumber) (fun x -> ann) 
                |> pset.OfList
                                                                              
    open Aardvark.ImmutableSceneGraph
    open Aardvark.Elmish
    open Primitives

    open SimpleDrawingApp
    open DrawingApp    

    open Newtonsoft.Json
            
    let thickness v = { Default.thickness with value = v }

    let initial : Drawing = { 
        _id = null
        picking = None 
       // ViewerState = FreeFlyCameraApp.initial
        finished = PSet.empty
        working = None
        style = Styles.standard.[0]
        measureType = { choices = ["Point";"Line";"Polyline"; "Polygon"; "DipAndStrike" ]; selected = "Polyline" }
        styleType = { choices = ["#Fascies";"#Bed";"#Crossbed"; "#Grain"]; selected = "#Fascies" }
        projection = { choices = ["linear";"viewpoint";"top"]; selected = "viewpoint" }
        samples = Default.samples' 4.0
        history = EqualOf.toEqual None; future = EqualOf.toEqual None
        selected = PSet.empty
        selectedAnn= None
        filename = @"C:\Aardwork\wand.jpg"
        }

    type Action =
        | Click of int
        | Finish
        | AddPoint   of V3d
        | MoveCursor of V3d
        | ChangeStyle of int
        | Undo
        | Redo
        | PickStart  
        | PickStop   
        | Set_Type    of Choice.Action
        | Set_Style   of Choice.Action
        | Set_Projection of Choice.Action
        | Set_Samples of Numeric.Action
        | SetAnnotationProperties of AnnotationPropertiesApp.Action            
        | Save
        | Load
        | Clear
        | Send
//        | FreeFlyAction   of FreeFlyCameraApp.Action
//        | DragStart       of PixelPosition
//        | DragStop        of PixelPosition

    let send2Js = Communication.start()

    let stash (m : Drawing) =
        { m with history = EqualOf.toEqual (Some m); future = EqualOf.toEqual None }

    let clearUndoRedo (m : Drawing) =
        { m with history = EqualOf.toEqual None; future = EqualOf.toEqual None }  

    let finishPolygon (m : Drawing) = 
                match m.working with
                    | None -> m
                    | Some p -> 
                        let f = { geometry = p.finishedPoints
                                  style = m.style
                                  seqNumber = m.finished.AsList.Length
                                  annType = m.measureType.selected 
                                  styleType = m.styleType
                                  projection = m.projection
                                  segments = p.finishedSegments
                                }

                     //   f |> DrawingApp.Lite.ofAnnotation |> JsonConvert.SerializeObject |> send2Js.send |> ignore
                                                    
                        { m with 
                            working = None 
                            finished = PSet.add f m.finished }

    //let toEdges (points:seq<V3d>) = Polygon3d(points).EdgeLines

    let edgeLines (close : bool)  (p : list<V3d>) =        
        let head = p |> List.head        
        (if close then p @ [head] else p) 
            |> List.pairwise
            |> List.map (fun (a,b) -> new Line3d(a,b)) 
            |> List.toSeq

    let sampleAlongEdge (noOfSamples:int) (edges : seq<Line3d>) =
        edges |> Seq.map (fun e ->                     
                    let step = (e.P1 - e.P0) / (float noOfSamples + 1.0)
                    [1 .. noOfSamples] |> List.map (fun i -> e.P0 + step * (float i))) // for now only computes ray lookats
    
    let updateAddPoint (m : Drawing) (p : V3d) =         
        match m.working with            
            | Some v -> 
                                                
                let points = (p :: v.finishedPoints)
                let segments = []

                let segments = if m.projection.selected <> "linear" then
                                  points                                    
                                    |> edgeLines (m.measureType.selected = "Polygon")
                                    |> sampleAlongEdge (int m.samples.value) // resamples everything on every update, not smart ;)
                                    |> Seq.toList
                                else []

                let k = { m with working = Some { v with finishedSegments = segments; finishedPoints = points }}
                                
                match m.measureType.selected with
                    | "Point"        -> finishPolygon k
                    | "Line"         -> if k.working.Value.finishedPoints.Length = 2 then finishPolygon k else k
                    | "Polyline"     -> k
                    | "Polygon"      -> k
                    | "DipAndStrike" -> k
                    | _ -> failwith (sprintf "measure mode %A not recognized" m.measureType.selected)
            | None -> m                       
                    
    let update (picking : Option<int>) e m (cmd : Action) =
        let picking = m.picking
        match cmd, picking with
            | Click i, _ when i <> -1 ->
              let c = { m with selected = PSet.empty }
              (if Seq.contains i c.selected 
                            then { c with selected = PSet.remove i c.selected; selectedAnn = None }
                            else { c with selected = PSet.add i c.selected; selectedAnn = Annotations.tryGet m i })
              |> stash
            | Finish, _ ->                
                match m.working with
                    | Some x when x.finishedPoints.Length > 0 -> 
                        finishPolygon m |> stash
                    | _ -> m
            | AddPoint p, Some _ -> updateAddPoint m p |> stash
            | MoveCursor p, Some _ ->
                match m.working with
                    | None -> { m with working = Some { cursor = Some p; finishedPoints = []; finishedSegments = [] }}
                    | Some v -> { m with working = Some { v with cursor = Some p }}
            | ChangeStyle s, _ -> 
                { m with 
                    style = Styles.standard.[s];
                    styleType = { m.styleType with selected = m.styleType.choices.[s] }} |> stash
            | Set_Type a,       _ when m.working.IsNone -> { m with measureType = Choice.update e m.measureType a}
            | Set_Projection a, _ when m.working.IsNone -> { m with projection = Choice.update e m.projection a}
            | Set_Samples a, _ -> { m with samples = Numeric.update e m.samples a}
            | Set_Style a, _ -> 
                let style = Choice.update e m.styleType a
                let index = Choice.index style
                
                { m with styleType = style; style = Styles.standard.[index] } |> stash
                    
            | Undo, _ -> match !m.history with
                                | None -> m
                                | Some k -> { k with future = EqualOf.toEqual <| Some m }
            | Redo, _ -> match !m.future with
                                | None -> m
                                | Some k -> k
            | PickStart, _   -> { m with picking = Some 0 }
            | PickStop, _    -> { m with picking = None }
            | SetAnnotationProperties a, _ -> 
                            match m.selectedAnn with
                                | Some x -> 
                                    let ann' = AnnotationPropertiesApp.update (e |> Env.map SetAnnotationProperties) x a
                                    let m = {m with selectedAnn = Some ann'} // update selected annotation                                    
                                    { m with finished = Annotations.update m ann' }
                                | None -> m           
                            |> stash          
            | Save, _ -> let m = m  |> clearUndoRedo
                         //Communication.app(
             
                         Serialization.save m "./drawing"
                         m
            | Load, _ -> Serialization.load "./drawing" |> clearUndoRedo
            | Clear, _ -> initial
            | Send, _ ->
                m |> DrawingApp.Lite.ofDrawing |> JsonConvert.SerializeObject |> send2Js.send |> ignore                
                m
//            | FreeFlyAction a, None -> { m with ViewerState =  FreeFlyCameraApp.update (e |> Env.map FreeFlyAction) m.ViewerState a }
//            | DragStart p, None->  { m with ViewerState = { m.ViewerState with lookingAround = Some p }}
//            | DragStop _, None  -> { m with ViewerState = { m.ViewerState with lookingAround = None }}
            | _,_ -> m    

    
           
    // view annotations in 3D
    let viewPolygon (p : list<V3d>) (segments: list<list<V3d>>) (r : float) (id : int) (close : bool) =        
        match p with
            | [] -> []
            | _  ->                
                
                let lines = p |> edgeLines close               

                [   //drawing leading sphere      
                    yield Sphere3d(List.rev p |> List.head, r) |> Sphere |> Scene.render Pick.ignore
          
                    //only polygons with valid ids are pickable
                    let pick = if id = -1 then Pick.ignore else [on Mouse.down (fun x -> Click id)] 
                                        
                    for s in segments do
                        for p' in s do
                            yield Sphere3d(p', r * 0.80) |> Sphere |> Scene.render Pick.ignore

                    for edge in lines do
                        let v = edge.P1 - edge.P0
                        yield Primitives.cylinder edge.P0 v.Normalized v.Length (r/2.5) |> Scene.render pick
                        yield Sphere3d(edge.P0, r) |> Sphere |> Scene.render Pick.ignore
                ]
        |> Scene.group                
    
    let selectionColor = C4b.Red
    let viewSelection (p : list<V3d>) (r : float) (close : bool) =
        let lines =  p |> edgeLines close
        [           
            yield Sphere3d(List.rev p |> List.head, r) |> Sphere |> Scene.render Pick.ignore
            for edge in lines do
                yield Sphere3d(edge.P0, r) |> Sphere |> Scene.render Pick.ignore
        ] |> Scene.group

    let closed (p : Annotation) = p.annType = "Polygon"

    let viewDrawingPolygons (m :  MDrawing) =
        let isSelected id = Seq.contains id m.mselected
        aset {
                    
            // draw all finished polygons       
            for p in m.mfinished :> aset<_> do                 
                let color = if isSelected p.seqNumber then selectionColor else p.style.color                
                yield [viewPolygon p.geometry p.segments p.style.thickness.value p.seqNumber (closed p)] 
                        |> Scene.colored (Mod.constant p.style.color)

            // draw selection geometry
            for id in m.mselected :> aset<_> do
                printfn "selected: %i" id
                match m.mfinished |> Seq.tryFind(fun x -> x.seqNumber = id) with
                    | Some k ->  yield [viewSelection k.geometry (k.style.thickness.value * 1.01) (closed k)] 
                                            |> Scene.colored (Mod.constant selectionColor)
                    | None -> ()

            // draw working polygon
            let! style = m.mstyle
            let! working = m.mworking
            let! picking = m.mpicking
            match working with
                | Some v when v.cursor.IsSome -> 
                    let line = if picking.IsSome then (v.cursor.Value :: v.finishedPoints) else v.finishedPoints                    

                    yield [viewPolygon (line) v.finishedSegments style.thickness.value -1 (m.mmeasureType.Value.selected = "Polygon")] 
                            |> Scene.colored (Mod.constant style.color) 
                    yield [ Sphere3d(V3d.OOO, style.thickness.value) |> Sphere |>  Scene.render Pick.ignore ] 
                            |> Scene.colored (Mod.constant C4b.Red)
                            |> Scene.transform' (Mod.constant <| Trafo3d.Translation(v.cursor.Value)) 
                | _ -> ()
        }
        
    let viewPlane = [ Quad (Quad3d [| V3d(-2,-2,0); V3d(2,-2,0); V3d(2,2,0); V3d(-2,2,0) |]) 
                            |>  Scene.render [ 
                                 on Mouse.move MoveCursor
                                 on (Mouse.down' MouseButtons.Left)  AddPoint 
                               //  on (Mouse.down' MouseButtons.Right) (constF ClosePolygon)
                               ] 
                      ] |>  Scene.colored (Mod.constant C4b.Gray)

    let viewDrawing (m : MDrawing) =         
        viewDrawingPolygons m 
            |> Scene.agroup 
            |> Scene.effect [
                    toEffect DefaultSurfaces.trafo;
                    toEffect DefaultSurfaces.vertexColor]
                 //   toEffect DefaultSurfaces.thickLine]
     //       |> Sg.uniform "LineWidth" (Mod.constant 5.0)       
            

    let viewQuad (m : MDrawing) =
        let texture = 
            m.mfilename |> Mod.map (fun path -> 
                let pi = PixTexture2d(PixImageMipMap([|PixImage.Create(path)|]),true)
                pi :> ITexture
            )

        Quad (Quad3d [| V3d(0,-2,-2); V3d(0,-2,2); V3d(0,2,2); V3d(0,2,-2) |]) 
            |> Scene.render [on Mouse.move MoveCursor; on (Mouse.down' MouseButtons.Left) AddPoint]
            |> (Scene.textured texture) :> ISg<_>
            |> Scene.effect [
                    toEffect DefaultSurfaces.trafo;
                    toEffect DefaultSurfaces.vertexColor;
                    toEffect DefaultSurfaces.diffuseTexture]
        
    let view3D (sizes : IMod<V2i>) (m : MDrawing) =     
        let cameraView = CameraView.lookAt (V3d.IOO * 5.0) V3d.OOO V3d.OOI |> Mod.constant
        let frustum = sizes |> Mod.map (fun (b : V2i) -> Frustum.perspective 60.0 0.1 10.0 (float b.X / float b.Y))       
        [viewDrawing m 
         viewQuad    m]
            |> Scene.group
            |> Scene.camera (Mod.map2 Camera.create cameraView frustum)    

    // view GUI Eleements
    let viewMeasurements (m : Drawing) = 
        let isSelected id = Seq.contains id m.selected
        div [clazz "ui divided list"] [
            yield h3 [] [Text "Annotations:"]
            for me in (m.finished |> Seq.sortBy (fun x -> x.seqNumber)) do
                let background, fontcolor = if isSelected me.seqNumber then "#969696","#f0f0f0" else "#d9d9d9", "#252525"
                
                yield div [clazz "item"; Style ["backgroundColor", background]; onMouseClick (fun o -> Click me.seqNumber)] [
                            i [clazz "medium File Outline middle aligned icon"; Style ["color", me.style.color |> Html.ofC4b]][]
                            div[clazz "content"] [
                                Html.Layout.horizontal[ 
                                    Html.Layout.boxH [ Text me.annType ] 
                                    Html.Layout.boxH [ Text (sprintf "%i" me.seqNumber) ]
                                    Html.Layout.boxH [ Text (sprintf "%A" me.projection.selected) ]
                                ]
//                                div [clazz "header"; Style ["color", fontcolor] ] [Text me.annType] 
//                                div [clazz "description"; Style ["color", fontcolor]] [Text (sprintf "%i" me.seqNumber)]
                            ]
                        ]
            ]

    let viewProperties (m : Drawing) =
        match m.selectedAnn with
            | None -> div[] [h3 [] [Text "Properties:"]]
            | Some ann -> div[] [ 
                            yield h3 [] [Text (sprintf "Properties of %A:" ann.seqNumber)]
                            yield AnnotationPropertiesApp.view ann |> Html.map SetAnnotationProperties    
                          ]                                    
     
    let viewUI (m : Drawing) =
        div [] [
             //Rendercontrol
             div [clazz "unselectable"; Style ["width", "75%"; "height", "100%"; "background-color", "transparent"; "float", "right"]; 
                  attribute "id" "renderControl"] [

                //Overlay
                Html.Layout.horizontal [
                    Html.Layout.boxH [ 
                        div [clazz "ui buttons"] [
                            button [clazz "ui icon button"; onMouseClick (fun _ -> Undo)] [i [clazz "arrow left icon"] []]
                            button [clazz "ui icon button"; onMouseClick (fun _ -> Redo)] [i [clazz "arrow right icon"] []]
                        ]
                    ]
                    Html.Layout.boxH [ Choice.view m.measureType |> Html.map Set_Type ]
                    Html.Layout.boxH [ Choice.view m.styleType |> Html.map Set_Style ]
                    Html.Layout.boxH [ Choice.view m.projection |> Html.map Set_Projection ]
                    Html.Layout.boxH [ Numeric.view m.samples |> Html.map Set_Samples ]                    
                    Html.Layout.boxH [
                        div [clazz "ui buttons"] [
                                button [clazz "ui icon button"; onMouseClick (fun _ -> Save)] [
                                    i [clazz "save icon"] [] ]
                                button [clazz "ui icon button"; onMouseClick (fun _ -> Load)] [
                                    i [clazz "folder outline icon"] [] ]
                                button [clazz "ui icon button"; onMouseClick (fun _ -> Clear)] [
                                    i [clazz "file outline icon"] [] ]
                                button [clazz "ui icon button"; onMouseClick (fun _ -> Send)] [
                                    i [clazz "external icon"] [] ]
                        ]
                    ]
                    Html.Layout.finish()
                ]
             ]

             //Measurement List
             div [Style ["width", "25%"; "height", "60%";
                         "overflowY", "auto"; "float", "left"; "backgroundColor", "#fff7fb"
                         "border-style", "solid"; "border-width", "1px 1px 0px 1px"]] [
                            viewMeasurements m
             ]
             
             //Measurement Properties
             div [Style ["width", "25%"; "height", "40%";
                         "overflowY", "auto"; "float", "left"; "backgroundColor", "#fff7fb"
                         "border-style", "solid"; "border-width", "1px 1px 1px 1px"]] [
                            viewProperties m
            ]
        ]

    // app setup
    let subscriptions (time : IMod<DateTime>) (m : Drawing) =
        Many [Input.key Down Keys.Enter (fun _ _-> Finish)
              Input.key Down Keys.Left  (fun _ _-> Undo)
              Input.key Down Keys.Right (fun _ _-> Redo)
              
              Input.toggleKey Keys.LeftCtrl (fun _ -> PickStart) (fun _ -> PickStop)

              Input.key Down Keys.D1  (fun _ _-> ChangeStyle 0)
              Input.key Down Keys.D2  (fun _ _-> ChangeStyle 1)
              Input.key Down Keys.D3  (fun _ _-> ChangeStyle 2)
              Input.key Down Keys.D4  (fun _ _-> ChangeStyle 3)

          //    FreeFlyCameraApp.subscriptions time m.ViewerState |> Sub.map FreeFlyAction   

            //  Input.toggleMouse Input.Mouse.left DragStart DragStop
            ]

    

    let app s time =
        {
            initial = initial
            update = update (None)
            view = view3D s
            ofPickMsg = fun _ _ -> []
            subscriptions = subscriptions time
        }

    let createApp f time keyboard mouse viewport camera =

        let initial = initial
        let composed = ComposedApp.ofUpdate initial (update f)

        let three3dApp  = {
            initial = initial
            update = update f
            view = view3D (viewport |> Mod.map (fun (a : Box2i) -> a.Size))
            ofPickMsg = fun _ _ -> []
            subscriptions = subscriptions time
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

module ComposeTestDrawing =

    open ComposeTest

    let app s time =
        {
            initial = DrawingApp.initial
            update = DrawingApp.update (None)
            view = DrawingApp.view3D s
            ofPickMsg = fun _ _ -> []
            subscriptions = DrawingApp.subscriptions time
        }

    let createApp f time keyboard mouse viewport camera =

        let initial = DrawingApp.initial
        let composed = ComposedApp.ofUpdate initial (DrawingApp.update f)

        let three3dApp  = {
            initial = initial
            update = DrawingApp.update f
            view = DrawingApp.view3D (viewport |> Mod.map (fun (a : Box2i) -> a.Size))
            ofPickMsg = fun _ _ -> []
            subscriptions = DrawingApp.subscriptions time
        }

        let viewApp = 
            {
                initial = initial 
                update = DrawingApp.update f
                view = DrawingApp.viewUI
                subscriptions = Fablish.CommonTypes.Subscriptions.none
                onRendered = OnRendered.ignore
            }

        let three3dInstance = ComposedApp.add3d composed keyboard mouse viewport camera three3dApp (fun m app -> m) id id
        let fablishInstance = ComposedApp.addUi composed Net.IPAddress.Loopback "8083" viewApp (fun m app -> m) id id

        three3dInstance, fablishInstance

//module ComposeTestIntegration =
//
//    open ComposeTest
//
//    let app s time =
//        {
//            initial = IntegrationApp.initial
//            update = IntegrationApp.update
//            view = IntegrationApp.view s
//            ofPickMsg = fun _ _ -> []
//            subscriptions = IntegrationApp.subscriptions time
//        }
//
//    let createApp f time keyboard mouse viewport camera =
//
//        let initial = IntegrationApp.initial
//        let composed = ComposedApp.ofUpdate initial (IntegrationApp.update)
//
//        let three3dApp  = {
//            initial = initial
//            update = IntegrationApp.update
//            view = IntegrationApp.view 
//            ofPickMsg = fun _ _ -> []
//            subscriptions = IntegrationApp.subscriptions time
//        }
//
//        let viewApp = 
//            {
//                initial = initial 
//                update = IntegrationApp.update
//                view = IntegrationApp.vi
//                subscriptions = Fablish.CommonTypes.Subscriptions.none
//                onRendered = OnRendered.ignore
//            }
//
//        let three3dInstance = ComposedApp.add3d composed keyboard mouse viewport camera three3dApp (fun m app -> m) id id
//        let fablishInstance = ComposedApp.addUi composed Net.IPAddress.Loopback "8083" viewApp (fun m app -> m) id id
//
//        three3dInstance, fablishInstance
