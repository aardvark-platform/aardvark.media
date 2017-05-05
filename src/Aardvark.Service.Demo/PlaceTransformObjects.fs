namespace PlaceTransformObjects


module App =

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    open DragNDrop
    open Aardvark.SceneGraph
    open Aardvark.Base.Rendering
    open Aardvark.UI
    open Aardvark.Base.Geometry
    open PlaceTransformObjects

    type Action = 
        | MovePlane of V3d   
        | PlaceBox 
        | Select of string
        | Translate of TranslateController.ControllerAction
        | CameraMessage of CameraController.Message
        | Unselect


    let update (m : Scene) (a : Action) =
        match a with
            | CameraMessage a -> 
                match m.selectedObject with
                    | None -> { m with camera = CameraController.update m.camera a }
                    | Some s ->
                        if s.transformation.grabbed.IsNone then { m with camera = CameraController.update m.camera a }
                        else m
            | MovePlane t -> m
            | PlaceBox -> 
                let name = System.Guid.NewGuid() |> string
                let newObject = { name = name; objectType = ObjectType.Box; selected = false; transformation = TranslateController.initial }
                let world = { m.world with objects = HMap.add name newObject m.world.objects }
                { m with world = world }
            | Select n -> 
                match m.world.objects |> HMap.tryFind n with
                    | None -> m
                    | Some obj -> 
                        { m with world = { m.world with objects = m.world.objects |> HMap.remove n }; selectedObject = Some obj }
            | Translate a -> 
                match m.selectedObject with
                    | None -> m
                    | Some obj -> 
                        { m with 
                            selectedObject = Some { obj with transformation = TranslateController.updateController obj.transformation a }
                        }
            | Unselect -> 
                match m.selectedObject with 
                    | None -> m
                    | Some o -> 
                        { m with selectedObject = None; world =  { m.world with objects = m.world.objects |> HMap.add o.name o } }


    let viewScene (m : MScene) =


        let plane = 
            Sg.box' C4b.White (Box3d.FromCenterAndSize(V3d.OOO,V3d(10.0,10.0,-0.1)))
            |> Sg.requirePicking
            |> Sg.noEvents
            |> Sg.withEvents [
                    Sg.onMouseMove MovePlane
               ]

        let objects =
            aset {
                for (name,obj) in m.world.objects |> AMap.toASet do
                    yield 
                        Sg.box (C4b.Blue |> Mod.constant) (Box3d.FromCenterAndSize(V3d.OOO,V3d.III*0.5) |> Mod.constant) 
                        |> Sg.requirePicking 
                        |> Sg.noEvents
                        |> Sg.withEvents [ Sg.onMouseDown (fun _ _ -> Select name)]
                        |> Sg.trafo obj.transformation.trafo 
            } |> Sg.set

        
        let selectedObj = 
            aset {
                let! selected = m.selectedObject
                match selected with
                    | None -> ()
                    | Some obj -> 
                        let controller = DragNDrop.TranslateController.viewController Translate obj.transformation
                        let sg = Sg.box (C4b.Red |> Mod.constant) (Box3d.FromCenterAndSize(V3d.OOO,V3d.III*0.5) |> Mod.constant) |> Sg.requirePicking |> Sg.noEvents
                        yield controller
                        yield sg |> Sg.trafo obj.transformation.trafo |> Sg.fillMode (Mod.constant FillMode.Line)
             } |> Sg.set


        Sg.ofSeq [plane; objects; selectedObj]
        |> Sg.effect [
                toEffect <| DefaultSurfaces.trafo
                toEffect <| DefaultSurfaces.vertexColor
                toEffect <| DefaultSurfaces.simpleLighting
            ]


    let view (m : MScene) =
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl m.camera CameraMessage (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [
                        yield attribute "style" "width:85%; height: 100%; float: left;"
                        yield! TranslateController.controlSubscriptions Translate
                    ]) (viewScene m)

                div [style "width:15%; height: 100%; float:right"] [
                    Html.SemUi.stuffStack [
                        button [clazz "ui button"; onClick (fun _ ->  PlaceBox )] [text "Add Box"]
                        button [clazz "ui button"; onClick (fun _ ->  Unselect )] [text "Unselect"]
                    ]
                ]
            ]
        )

    let app =
        {
            unpersist = Unpersist.instance
            threads = fun (model : Scene) -> CameraController.threads model.camera |> ThreadPool.map CameraMessage
            initial = { world = { objects = HMap.empty }; camera = CameraController.initial; selectedObject = None }
            update = update
            view = view
        }

    let start() = App.start app