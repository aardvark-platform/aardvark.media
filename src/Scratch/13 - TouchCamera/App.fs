namespace TouchCamera

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.TouchStick

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

module TouchStickApp =
    open Aardvark.Application
    open Aardvark.Base

        //let rec update (m : TouchStickModel) (msg : TouchStickMessage) =
        //match msg with
        //   | RiteTouchStart s 
        //   | RiteTouchUpdate s -> 
        //        let pos = V2d(s.distance * cos(s.angle * Constant.RadiansPerDegree), s.distance * sin(s.angle * Constant.RadiansPerDegree))

        //        { m with ritestick = Some s; cameraState = (FreeFlyController.startAnimation { m.cameraState with rotateVec = V3d(-pos.Y,-pos.X,0.0) * 0.01 }) }
        //   | RiteTouchEnd -> 
        //        { m with ritestick = None; cameraState = (FreeFlyController.startAnimation { m.cameraState with rotateVec = V3d.Zero }) }


        //   | LeftTouchStart s 
        //   | LeftTouchUpdate s -> 
        //        let pos = V2d(s.distance * cos(s.angle * Constant.RadiansPerDegree), s.distance * -sin(s.angle * Constant.RadiansPerDegree))

        //        { m with leftstick = Some s; cameraState = (FreeFlyController.startAnimation { m.cameraState with moveVec = V3d(pos.X,0.0,pos.Y) }) }
        //   | LeftTouchEnd -> 
        //        { m with leftstick = None; cameraState = (FreeFlyController.startAnimation { m.cameraState with moveVec = V3d.Zero }) }
        //   | Camera im -> { m with cameraState = FreeFlyController.update m.cameraState im}


    let rec update (m : TouchStickModel) (msg : TouchStickMessage) =
        match msg with
            | Camera im -> { m with cameraState = FreeFlyController.update m.cameraState im}

            | StartMovStick s ->
                { m with movStick = Some s}
            | MoveMovStick s ->
                let pos = V2d(s.distance * cos(s.angle * Constant.RadiansPerDegree), s.distance * -sin(s.angle * Constant.RadiansPerDegree))

                { m with movStick = Some s; cameraState = (FreeFlyController.startAnimation { m.cameraState with moveVec = V3d(pos.X,0.0,pos.Y) }) }
            | EndMovStick ->
                { m with movStick = None }

            | StartRotStick s ->
                { m with rotStick = Some s}
            | MoveRotStick s ->
                let pos = V2d(s.distance * cos(s.angle * Constant.RadiansPerDegree), s.distance * sin(s.angle * Constant.RadiansPerDegree))

                { m with rotStick = Some s; cameraState = (FreeFlyController.startAnimation { m.cameraState with rotateVec = V3d(-pos.X,-pos.Y,0.0) * 0.01 }) }
            | EndRotStick ->
                { m with rotStick = None }

           
    let viewScene (model : MTouchStickModel) =
        IndexedGeometryPrimitives.solidCoordinateBox 100.0
            |> Sg.ofIndexedGeometry
            |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                }
            |> Sg.andAlso(
                Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
                 |> Sg.shader {
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.vertexColor
                        do! DefaultSurfaces.simpleLighting
                    })
        
    let view (m : MTouchStickModel) =
        let sticks =
            [
                { name="leftstick"; area=Box2d(V2d(-1.0,-1.0),V2d(0.0,1.0)); radius = 100.0 }
                { name="ritestick"; area=Box2d(V2d( 0.0,-1.0),V2d(1.0,1.0)); radius = 100.0 }
            ]

                
        div [] [
            withTouchSticks sticks (
                FreeFlyController.controlledControl m.cameraState Camera (Frustum.perspective 60.0 0.1 1000.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [  
                        style "width: 100vw; height:100vh"
                                                        
                        onTouchStickStart "leftstick" (fun stick pos -> StartMovStick stick)
                        onTouchStickStart "ritestick" (fun stick pos -> StartRotStick stick)

                        onTouchStickMove "leftstick" (fun stick -> MoveMovStick stick)
                        onTouchStickMove "ritestick" (fun stick -> MoveRotStick stick)

                        onTouchStickStop "leftstick" (fun _ -> EndMovStick)
                        onTouchStickStop "ritestick" (fun _ -> EndRotStick)

                        attribute "showFPS" "true";
                        attribute "data-samples" "8"
                    ]
                    ) 
                    (viewScene m)
            )
        ]
    let threads (m : TouchStickModel) = 
        ThreadPool.empty
    let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = TouchStickModel.initial
            update = update 
            view = view
        }
