module PlaceTransformObjects

open Aardvark.Base
open Aardvark.Base.Incremental
    
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Base.Geometry
open Model
open Aardvark.UI.Trafos

type Action = 
    | PlaceBox 
    | Select        of string
    | SetKind       of TrafoKind
    | SetMode       of TrafoMode
    | Translate     of string * TrafoController.Action
    | Rotate        of string * TrafoController.Action
    | Scale         of string * TrafoController.Action
    | CameraMessage of CameraController.Message
    | KeyDown       of key : Aardvark.Application.Keys
    | Unselect
    | Nop

let isGrabbed (world : World) =
    world.selectedObjects |> HSet.exists (fun name -> 
        match HMap.tryFind name world.objects with
            | Some o -> o.transformation.grabbed.IsSome
            | None -> false
    )

let updateMode mode m =        

    let objs = 
        m.world.objects 
            |> HMap.map(fun _ x -> {x with transformation = { x.transformation with mode = mode;}})

    { m with mode = mode; world = { m.world with objects = objs;}}

let _selected name = 
    Scene.Lens.world |. World.Lens.objects |. HMap.Lens.item name |? Unchecked.defaultof<_> |. Object.Lens.transformation

let update (m : Scene) (a : Action) =
    match a with
        | CameraMessage a -> 
            if isGrabbed m.world then m 
            else { m with camera = CameraController.update m.camera a }
        | PlaceBox -> 
            let name = System.Guid.NewGuid() |> string
            let newObject = { name = name; objectType = ObjectType.Box Box3d.Unit; transformation = TrafoController.initial }
            let world = { m.world with objects = HMap.add name newObject m.world.objects }
            { m with world = world }
        | Select n -> 
            let world = { m.world with selectedObjects = HSet.add n HSet.empty }

            { m with world = world }

            //_selected(n).Update(m, fun t -> { t with pivotTrafo = t.trafo })
                
        | Translate(name, a) ->
            _selected(name).Update(m, fun t -> TranslateController.updateController t a)
        | Rotate(name, a) ->                 
            _selected(name).Update(m, fun t -> RotationController.updateController t a)
        | Scale(name,a) ->                 
            _selected(name).Update(m, fun t -> ScaleController.updateController t a)
        | Unselect -> 
            if isGrabbed m.world then m // hack
            else  { m with world = { m.world with selectedObjects = HSet.empty } }
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


    

let viewScene (m : MScene) =
        
    let objects =
        aset {
            for (name,obj) in m.world.objects |> AMap.toASet do
                let selected = ASet.contains name m.world.selectedObjects
                let color = selected |> Mod.map (function | true -> C4b.Red | false -> C4b.Gray)
                                                                                  
                let controller =
                    adaptive {

                        let! sel  = selected
                        let! kind = m.kind                                              
                            
                        let sg =
                            match sel with 
                                | true ->
                                    match kind with
                                        | TrafoKind.Translate -> 
                                            TranslateController.viewController (fun t -> Translate(obj.name |> Mod.force, t)) m.camera.view obj.transformation
                                        | TrafoKind.Rotate    -> 
                                            RotationController.viewController (fun r -> Rotate(obj.name |> Mod.force, r)) m.camera.view obj.transformation
                                        | TrafoKind.Scale     -> 
                                            ScaleController.viewController (fun s -> Scale(obj.name |> Mod.force, s)) m.camera.view obj.transformation
                                        | _ -> Sg.empty
                                    
                                | false -> Sg.ofList []

                        return sg
                    } |> Sg.dynamic 
                        
                let box = 
                    obj.objectType 
                        |> Mod.map(fun x -> 
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
                    |> Sg.trafo (obj.objectType |> Mod.map(fun x -> 
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


let view (m : MScene) =
    body [ style "background: #1B1C1E"] [
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl m.camera CameraMessage (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
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
    HMap.ofList [
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
    [ name, newObject ] |>  HMap.ofList

let app =
    {
        unpersist = Unpersist.instance
        threads = fun (model : Scene) -> CameraController.threads model.camera |> ThreadPool.map CameraMessage
        initial = 
            { 
                world = { objects = many; selectedObjects = HSet.empty }
                camera = CameraController.initial' 20.0
                kind = TrafoKind.Rotate 
                mode = TrafoMode.Local
            } |> updateMode TrafoMode.Local
        update = update
        view = view
    }

let start() = App.start app