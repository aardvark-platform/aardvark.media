namespace DragNDrop

module App =

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    open Aardvark.SceneGraph
    open Aardvark.Base.Rendering
    open Aardvark.UI

    type Action = 
        | StartDrag of V3d
        | StopDrag
        | Move      of V3d
        | CameraAction of Demo.CameraController.Message

    let update (m : Model) (a : Action) =
        match a with
            | CameraAction a when Option.isNone m.dragging -> { m with camera = Demo.CameraController.update m.camera a }
            | Move p         when Option.isSome m.dragging -> { m with trafo = Trafo3d.Translation p }
            
            | StartDrag p -> { m with dragging = Some p }
            | StopDrag    -> { m with dragging = None   }
            | _ -> m


    let scene (m : MModel) =

        let box =
            Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
            |> Sg.requirePicking
            |> Sg.noEvents    
            |> Sg.trafo m.trafo
            |> Sg.Incremental.withEvents (
                  amap {
                     let! dragging = m.dragging
                     match dragging with 
                        | Some _ -> ()
                        | _ -> 
                            yield Sg.onMouseDown (fun _ p -> StartDrag p )
                            yield Sg.onMouseUp   (fun p   -> StopDrag    )
                   }
               )

        let plane = 
            Sg.box' C4b.White (Box3d.FromCenterAndSize(V3d.OOO,V3d(10.0,10.0,0.1)))
            |> Sg.requirePicking
            |> Sg.noEvents
            |> Sg.withEvents [
                 Sg.onMouseMove (fun p   -> Move p      )
               ]

        let scene = 
            Sg.ofSeq [box; plane]
            |> Sg.effect [
                    toEffect <| DefaultSurfaces.trafo
                    toEffect <| DefaultSurfaces.vertexColor
                    toEffect <| DefaultSurfaces.simpleLighting
               ]

            

        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        Demo.CameraController.controlledControl m.camera CameraAction
            (Mod.constant frustum) 
            (AttributeMap.ofList [ attribute "style" "width:100%; height: 100%"]) scene

    let view (m : MModel) =
        div [] [
            scene m
        ]

    let app =
        {
            unpersist = Unpersist.instance
            threads = fun (model : Model) -> Demo.CameraController.threads model.camera |> ThreadPool.map CameraAction
            initial = { trafo = Trafo3d.Identity; dragging = None; camera = Demo.CameraController.initial }
            update = update
            view = view
        }

    let start() = App.start app