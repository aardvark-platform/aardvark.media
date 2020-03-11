module PlaceTransformObjects

open Aardvark.Base
open FSharp.Data.Adaptive
    
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Base.Geometry
open Model
open Aardvark.UI.Trafos
open Aether.Operators

type Action = 
    | PlaceBox 
    | Select        of string
    | SetKind       of TrafoKind
    | SetMode       of TrafoMode
    | Translate     of string * TrafoController.Action
    | Rotate        of string * TrafoController.Action
    | Scale         of string * TrafoController.Action
    | CameraMessage of FreeFlyController.Message
    | KeyDown       of key : Aardvark.Application.Keys
    | Unselect
    | Nop

let isGrabbed (world : World) =
    world.selectedObjects |> HashSet.exists (fun name -> 
        match HashMap.tryFind name world.objects with
            | Some o -> o.transformation.grabbed.IsSome
            | None -> false
    )

let updateMode mode m =        

    let objs = 
        m.world.objects 
            |> HashMap.map(fun _ x -> {x with transformation = { x.transformation with mode = mode;}})

    { m with mode = mode; world = { m.world with objects = objs;}}

let _selected name = 
    let hm k  =
        (fun m -> HashMap.tryFind k m), (fun v m -> HashMap.add k v m)
    Scene.world_ >-> World.objects_ >-> hm name >?> Object.transformation_

let update (m : Scene) (a : Action) =
    match a with
        | CameraMessage a -> 
            if isGrabbed m.world then m 
            else { m with camera = FreeFlyController.update m.camera a }
        | PlaceBox -> 
            let name = System.Guid.NewGuid() |> string
            let newObject = { name = name; objectType = ObjectType.Box Box3d.Unit; transformation = TrafoController.initial }
            let world = { m.world with objects = HashMap.add name newObject m.world.objects }
            { m with world = world }
        | Select n -> 
            let world = { m.world with selectedObjects = HashSet.add n HashSet.empty }

            { m with world = world }

            //_selected(n).Update(m, fun t -> { t with pivotTrafo = t.trafo })
                
        | Translate(name, a) ->
            m |> (flip TranslateController.updateController a) ^% (_selected name)
        | Rotate(name, a) ->    
            m |> (flip RotationController.updateController a) ^% (_selected name)     
        | Scale(name,a) ->   
            m |> (flip ScaleController.updateController a) ^% (_selected name)     
        | Unselect -> 
            if isGrabbed m.world then m // hack
            else  { m with world = { m.world with selectedObjects = HashSet.empty } }
        | KeyDown k ->
            match k with 
                | Aardvark.Application.Keys.D1    -> { m with kind = TrafoKind.Translate }
                | Aardvark.Application.Keys.D2    -> { m with kind = TrafoKind.Rotate }
                | Aardvark.Application.Keys.D3    -> { m with kind = TrafoKind.Scale }
                | Aardvark.Application.Keys.Space ->
                let mode = 
                    match m.mode with
                        | TrafoMode.Global -> TrafoMode.Local
                        | TrafoMode.Local  -> TrafoMode.Global
                        | _ -> m.mode

                m |> updateMode mode
                | _ -> m                
        | SetKind k -> { m with kind = k}
        | SetMode mode -> m |> updateMode mode
        | Nop -> m


    

let viewScene (m : AdaptiveScene) =
        
    let objects =
        aset {
            for (name,obj) in m.world.objects |> AMap.toASet do
                let selected = m.world.selectedObjects |> ASet.toAVal |> AVal.map (fun s -> HashSet.contains name s)
                let color = selected |> AVal.map (function | true -> C4b.Red | false -> C4b.Gray)
                                                                                  
                let controller =
                    adaptive {

                        let! sel  = selected
                        let! kind = m.kind                                              
                            
                        let sg =
                            match sel with 
                                | true ->
                                    match kind with
                                        | TrafoKind.Translate -> 
                                            TranslateController.viewController (fun t -> Translate(obj.name, t)) m.camera.view obj.transformation
                                        | TrafoKind.Rotate    -> 
                                            RotationController.viewController (fun r -> Rotate(obj.name, r)) m.camera.view obj.transformation
                                        | TrafoKind.Scale     -> 
                                            ScaleController.viewController (fun s -> Scale(obj.name, s)) m.camera.view obj.transformation
                                        | _ -> Sg.empty
                                    
                                | false -> Sg.ofList []

                        return sg
                    } |> Sg.dynamic 
                        
                let box = 
                    obj.objectType 
                        |> AVal.map(fun x -> 
                        match x with
                            | ObjectType.Box b -> b
                            | _ -> failwith "object type not supported"                            
                        )
                yield 
                    Sg.box color box
                    |> Sg.requirePicking 
                    |> Sg.noEvents
                    |> Sg.Incremental.withEvents (
                        amap {
                            let! selected = selected
                            if not selected then
                                yield Sg.onDoubleClick (fun _ -> Select name)
                        } )                                                       
                    |> Sg.trafo (obj.objectType |> AVal.map(fun x -> 
                        match x with 
                            | ObjectType.Box b -> Trafo3d.Translation -b.Center
                            | _ -> failwith "object type not supported"))
                    |> Sg.trafo obj.transformation.previewTrafo
                    |> Sg.andAlso controller                        
        } |> Sg.set

    Sg.ofSeq [ objects; ]
    |> Sg.Incremental.withGlobalEvents (
            amap {
                let! selected = m.world.selectedObjects |> ASet.count
                if selected > 1 then 
                    yield Global.onMouseUp (fun _ -> Unselect) // hack
                    
            }
        )
    |> Sg.effect [
            toEffect <| DefaultSurfaces.trafo
            toEffect <| DefaultSurfaces.vertexColor
            toEffect <| DefaultSurfaces.simpleLighting
        ]


let view (m : AdaptiveScene) =
    body [ style "background: #1B1C1E"] [
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                FreeFlyController.controlledControl m.camera CameraMessage (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
                    (AttributeMap.ofList [onKeyDown KeyDown; attribute "style" "width:85%; height: 100%; float: left;"; attribute "data-samples" "8"]) (viewScene m)

                div [style "width:15%; height: 100%; float:right"] [
                    Html.SemUi.stuffStack [
                        button [clazz "ui button"; onClick (fun _ ->  PlaceBox )] [text "Add Box"]
                        button [clazz "ui button"; onClick (fun _ ->  Unselect )] [text "Unselect"]
                        Html.SemUi.dropDown m.kind SetKind
                        Html.SemUi.dropDown m.mode SetMode
                    ]
                ]
            ]
        )
    ]

let many = 
    HashMap.ofList [
        for i in 0 .. 2 do 
            for j in 0 .. 2 do
                for z in 0 .. 2 do
                    let name = System.Guid.NewGuid() |> string
                    let pos = V3d(float i, float j, float z)
                    let box = Box3d.FromCenterAndSize(pos, V3d.One * 0.5)

                    let pose = Pose.translate pos
                    let newObject = 
                        { 
                            name           = name
                            objectType     = ObjectType.Box box
                            transformation = { TrafoController.initial with pose = pose; previewTrafo = Pose.toTrafo pose }
                        }
                    yield name,newObject
    ]

let singleGuid = System.Guid.NewGuid() |> string

let one =
    let pos = V3d.OII
    let box = Box3d.FromCenterAndSize(pos, V3d.One * 0.5)
    let pose = Pose.translate pos
    let name = singleGuid
//        let pose = { Pose.identity with position = pos; rotation = Rot3d(V3d.III,0.4) }
    let newObject = { 
        name           = name
        objectType     = ObjectType.Box box
        transformation = 
        { 
            TrafoController.initial with 
                pose         = pose
                previewTrafo = Pose.toTrafo pose
        } 
    }
    [ name, newObject ] |>  HashMap.ofList

let app =
    {
        unpersist = Unpersist.instance
        threads = fun (model : Scene) -> FreeFlyController.threads model.camera |> ThreadPool.map CameraMessage
        initial = 
            { 
                world = { objects = many; selectedObjects = HashSet.empty }
                camera = FreeFlyController.initial' 20.0
                kind = TrafoKind.Rotate 
                mode = TrafoMode.Local
            } |> updateMode TrafoMode.Local
        update = update
        view = view
    }

let start() = App.start app