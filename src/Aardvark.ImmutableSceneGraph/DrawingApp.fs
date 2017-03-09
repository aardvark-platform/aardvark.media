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

module List =
    let rec updateIf (p : 'a -> bool) (f : 'a -> 'a) (xs : list<'a>) = 
        match xs with
            | x :: xs ->
                if(p x) then (f x) :: updateIf p f xs
                else x :: updateIf p f xs
            | [] -> []

module AnnotationPropertiesApp =
    type Action =                      
        | Set_Thickness of Numeric.Action

    let update (env: Env<Action>) (model : DrawingApp.Annotation) (action : Action) =
        match action with            
            | Set_Thickness a -> { model with style = {model.style with thickness = Numeric.update env model.style.thickness a }}

    let view (m : DrawingApp.Annotation) =
        div[] [                                                        
            Text m.annType
            br []
            Text (sprintf "Color: %A" m.style.color)
            br []
            Numeric.view m.style.thickness |> Html.map Set_Thickness                            
        ]           
    

module DrawingApp =

    module Selection =
        let select () = failwith ""
        let deselect () = failwith ""
        let isSelected () =  failwith ""

    open Aardvark.ImmutableSceneGraph
    open Aardvark.Elmish
    open Primitives

    open SimpleDrawingApp
          
    let initThickness = {
        value   = 0.02
        min     = 0.005
        max     = 0.03
        step    = 0.005
        format  = "{0:0.000}"
    }

    let thickness v = { initThickness with value = v }

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
        | SetAnnotationProperties of AnnotationPropertiesApp.Action

    let styles : List<DrawingApp.Style> = 
        [
           { color = new C4b(33,113,181) ; thickness = thickness 0.03 }
           { color = new C4b(107,174,214); thickness = thickness 0.02 }
           { color = new C4b(189,215,231); thickness = thickness 0.01 }
           { color = new C4b(239,243,255); thickness = thickness 0.005 }
        ]

    let choiceIndex (c : Choice.Model) =
        match List.tryFindIndex (fun x -> x = c.selected) c.choices with
            | Some i -> i
            | None -> failwith "selected not found in choice list"

    let stash (m : DrawingApp.Drawing) =
        { m with history = EqualOf.toEqual (Some m); future = EqualOf.toEqual None }

    let finishPolygon (m : DrawingApp.Drawing) = 
                match m.working with
                    | None -> m
                    | Some p -> 
                        { m with 
                            working = None 
                            finished = PSet.add { geometry = p.finishedPoints
                                                  style = m.style
                                                  seqNumber = m.finished.AsList.Length
                                                  annType = m.measureType.selected } m.finished }

    //["Point";"Line";"Polyline"; "Polygon"; "DipAndStrike" ]
    let updateAddPoint (m : DrawingApp.Drawing) (p : V3d) =         
        match m.working with
            | None -> 
                { m with working = Some { finishedPoints = [ p ]; cursor = None; }}
            | Some v -> 
                let k = { m with working = Some { v with finishedPoints = p :: v.finishedPoints }}
                match m.measureType.selected with
                    | "Point"        -> if k.working.Value.finishedPoints.Length = 1 then finishPolygon k else k
                    | "Line"         -> if k.working.Value.finishedPoints.Length = 2 then finishPolygon k else k
                    | "Polyline"     -> k
                    | "Polygon"      -> k
                    | "DipAndStrike" -> k
                    | _ -> failwith (sprintf "measure mode %A not recognized" m.measureType.selected)
        
    let update (picking : Option<int>) e (m : DrawingApp.Drawing) (cmd : Action) =
        let picking = m.picking
        match cmd, picking with
            | Click i, _ when i <> -1 ->
               let m = if Seq.contains i m.selected 
                            then { m with selected = PSet.remove i m.selected }
                            else { m with selected = PSet.add i m.selected }
               { m with selectedAnn = m.finished |> Seq.tryFind(fun x -> x.seqNumber = i)} |> stash
            | Finish, _ -> finishPolygon m |> stash
            | AddPoint p, Some _ -> updateAddPoint m p |> stash
            | MoveCursor p, Some _ ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = []; cursor = Some p }}
                    | Some v -> { m with working = Some { v with cursor = Some p }}
            | ChangeStyle s, _ -> 
                { m with 
                    style = styles.[s];
                    styleType = { m.styleType with selected = m.styleType.choices.[s] }} |> stash
            | Set_Type a, _ when m.working.IsNone -> { m with measureType = Choice.update e m.measureType a}
            | Set_Style a, _ -> 
                let style = Choice.update e m.styleType a
                let index = choiceIndex style
                
                { m with 
                    styleType = style
                    style = styles.[index]
                    history = EqualOf.toEqual (Some m); future = EqualOf.toEqual None
                    }
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
                                    let annotations' = List.updateIf (
                                                            fun (x : DrawingApp.Annotation) -> x.seqNumber = ann'.seqNumber) 
                                                            (fun x -> ann') 
                                                            m.finished.AsList

                                    {m with finished = annotations' |> PSet.ofList }
                                | None -> m                                                      
            | _,_ -> m    
           

    let viewPolygon (p : list<V3d>) (r : float) (id : int) (close : bool) =        
        match p with
            | [] -> []
            | _  ->                
                let lines =  Polygon3d(p |> List.toSeq).EdgeLines
                [     
                    //drawing leading sphere      
                    yield Sphere3d(List.rev p |> List.head, r) |> Sphere |> Scene.render Pick.ignore
          
                    let pick = if id = -1 then Pick.ignore else [on Mouse.down (fun x -> Click id)]
                    let take = if close then Seq.length lines else Seq.length lines - 1
                    for edge in lines |> Seq.take take do
                        let v = edge.P1 - edge.P0
                        yield Primitives.cylinder edge.P0 v.Normalized v.Length (r/2.0) |> Scene.render pick
                        yield Sphere3d(edge.P0, r) |> Sphere |> Scene.render Pick.ignore
                ]
        |> Scene.group
                
    let c4bToHtml (c : C4b) = 
        sprintf "rgb(%i,%i,%i)" c.R c.G c.B
    
    let selectionColor = C4b.Red//new C4b(150,150, 150)
    let viewSelection (p : list<V3d>) (r : float) =
        let lines =  Polygon3d(p |> List.toSeq).EdgeLines
        [           
            yield Sphere3d(List.rev p |> List.head, r) |> Sphere |> Scene.render Pick.ignore
            for edge in lines |> Seq.take (Seq.length lines - 1)  do
                yield Sphere3d(edge.P0, r) |> Sphere |> Scene.render Pick.ignore
        ] |> Scene.group

    let closed (p : DrawingApp.Annotation) =
        p.annType = "Polygon"

    let viewDrawingPolygons (m :  DrawingApp.MDrawing) =
        let isSelected id = Seq.contains id m.mselected
        aset {
                    
            // draw all finished polygons       
            for p in m.mfinished :> aset<_> do                 
                let color = if isSelected p.seqNumber then selectionColor else p.style.color                
                yield [viewPolygon p.geometry p.style.thickness.value p.seqNumber (closed p)] |> Scene.colored (Mod.constant p.style.color)

            // draw selection geometry
            for id in m.mselected :> aset<_> do
                printfn "selected: %i" id
                match m.mfinished |> Seq.tryFind(fun x -> x.seqNumber = id) with
                    | Some k ->  yield [viewSelection k.geometry (k.style.thickness.value * 1.01)] |> Scene.colored (Mod.constant selectionColor)
                    | None -> ()

            // draw working polygon
            let! style = m.mstyle
            let! working = m.mworking
            let! picking = m.mpicking
            match working with
                | Some v when v.cursor.IsSome -> 
                    let line = if picking.IsSome then (v.cursor.Value :: v.finishedPoints) else v.finishedPoints
                    yield 
                        [viewPolygon (line) style.thickness.value -1 (m.mmeasureType.Value.selected = "Polygon")] |> Scene.colored (Mod.constant style.color)
                    yield 
                        [ Sphere3d(V3d.OOO, style.thickness.value) |> Sphere |>  Scene.render Pick.ignore ] 
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

    let viewDrawing (m : DrawingApp.MDrawing) =         
        viewDrawingPolygons m 
            |> Scene.agroup 
            |> Scene.effect [
                    toEffect DefaultSurfaces.trafo;
                    toEffect DefaultSurfaces.vertexColor;]
                   // toEffect DefaultSurfaces.simpleLighting]

    let viewQuad (m : DrawingApp.MDrawing) =
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
        
    let view3D (sizes : IMod<V2i>) (m : DrawingApp.MDrawing) =        
        let cameraView = CameraView.lookAt (V3d.IOO * 5.0) V3d.OOO V3d.OOI |> Mod.constant
        let frustum = sizes |> Mod.map (fun (b : V2i) -> Frustum.perspective 60.0 0.1 10.0 (float b.X / float b.Y))        
        [viewDrawing m 
         viewQuad    m]
            |> Scene.group
            |> Scene.camera (Mod.map2 Camera.create cameraView frustum)

    let colorToHTML (c:C4b) =
        sprintf "rgb(%i,%i,%i)" c.R c.G c.B        

    let viewMeasurements (m : DrawingApp.Drawing) = 
        let isSelected id = Seq.contains id m.selected
        div [clazz "ui relaxed divided list"] [
            yield h3 [] [Text "Annotations:"]
            for me in (m.finished |> Seq.sortBy (fun x -> x.seqNumber)) do
                let background, fontcolor = if isSelected me.seqNumber then "#969696","#f0f0f0" else "#d9d9d9", "#252525"
                
                yield div [clazz "item"; Style ["backgroundColor", background]; onMouseClick (fun o -> Click me.seqNumber)] [
                            i [clazz "large File Outline middle aligned icon"; Style ["color", me.style.color |> c4bToHtml]][]
                            div[clazz "content"] [
                                div [clazz "header"; Style ["color", fontcolor] ] [Text me.annType] 
                                div [clazz "description"; Style ["color", fontcolor]] [Text (sprintf "%i" me.seqNumber)]
                            ]
                        ]
            ]

    let viewProperties (m : DrawingApp.Drawing) =
        match m.selected |> Seq.tryHead with
            | None -> div[] [h3 [] [Text "Properties:"]]
            | Some id -> div[] [ 
                            yield h3 [] [Text (sprintf "Properties of %A:" id)]
                            match (m.finished |> Seq.tryFind(fun x -> x.seqNumber = id)) with
                                        | None -> yield Text "No Properties found"
                                        | Some k -> yield AnnotationPropertiesApp.view m.selectedAnn.Value |> Html.map SetAnnotationProperties ]

    
    
    let viewUI (m : DrawingApp.Drawing) =
        div [] [
             div [Style ["width", "80%"; "height", "100%"; "background-color", "transparent"; "float", "right"]; 
                  attribute "id" "renderControl"] [
                button [clazz "ui icon button"; onMouseClick (fun _ -> Undo)] [i [clazz "arrow left icon"] []]
                button [clazz "ui icon button"; onMouseClick (fun _ -> Redo)] [i [clazz "arrow right icon"] []]
                Choice.view m.measureType |> Html.map Set_Type 
                Choice.view m.styleType |> Html.map Set_Style]
             div [Style ["width", "20%"; "height", "60%";
                         "overflowY", "auto"; "float", "left"; "backgroundColor", "#fff7fb"
                         "border-style", "solid"; "border-width", "1px 1px 0px 1px"]] [viewMeasurements m]
             
             div [Style ["width", "20%"; "height", "40%";
                         "overflowY", "auto"; "float", "left"; "backgroundColor", "#fff7fb"
                         "border-style", "solid"; "border-width", "1px 1px 1px 1px"]] [
                            viewProperties m
            ]
        ]

    let subscriptions (m : DrawingApp.Drawing) =
        Many [Input.key Down Keys.Enter (fun _ _-> Finish)
              Input.key Down Keys.Left  (fun _ _-> Undo)
              Input.key Down Keys.Right (fun _ _-> Redo)
              
              Input.toggleKey Keys.LeftCtrl (fun _ -> PickStart) (fun _ -> PickStop)

              Input.key Down Keys.D1  (fun _ _-> ChangeStyle 0)
              Input.key Down Keys.D2  (fun _ _-> ChangeStyle 1)
              Input.key Down Keys.D3  (fun _ _-> ChangeStyle 2)
              Input.key Down Keys.D4  (fun _ _-> ChangeStyle 3)
              ]

    let (initial : DrawingApp.Drawing) = { 
            finished = PSet.empty
            working = None
            _id = null
            history = EqualOf.toEqual None; future = EqualOf.toEqual None
            picking = None 
            filename = @"C:\Aardwork\wand.jpg"
            style = styles.[0]
            measureType = { choices = ["Point";"Line";"Polyline"; "Polygon"; "DipAndStrike" ]; selected = "Polyline" }
            styleType = { choices = ["#1";"#2";"#3"; "#4"]; selected = "#1" }
            selected = PSet.empty
            selectedAnn= None
            }

    let app s =
        {
            initial = initial
            update = update (None)
            view = view3D s
            ofPickMsg = fun _ _ -> []
            subscriptions = subscriptions
        }

    let createApp f time keyboard mouse viewport camera =

        let initial = initial
        let composed = ComposedApp.ofUpdate initial (update f)

        let three3dApp  = {
            initial = initial
            update = update f
            view = view3D (viewport |> Mod.map (fun (a : Box2i) -> a.Size))
            ofPickMsg = fun _ _ -> []
            subscriptions = subscriptions
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

