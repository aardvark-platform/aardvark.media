namespace TouchStick

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

module TouchStickApp =
    open Aardvark.Application
    open Aardvark.Base

    let rec update (m : TouchStickModel) (msg : TouchStickMessage) =
        match msg with
           | RiteTouchStart s 
           | RiteTouchUpdate s -> 
                let pos = V2d(s.distance * cos(s.angle * Constant.RadiansPerDegree), s.distance * sin(s.angle * Constant.RadiansPerDegree))

                { m with ritestick = Some s; cameraState = (FreeFlyController.startAnimation { m.cameraState with rotateVec = V3d(-pos.Y,-pos.X,0.0) * 0.01 }) }
           | RiteTouchEnd -> 
                { m with ritestick = None; cameraState = (FreeFlyController.startAnimation { m.cameraState with rotateVec = V3d.Zero }) }


           | LeftTouchStart s 
           | LeftTouchUpdate s -> 
                let pos = V2d(s.distance * cos(s.angle * Constant.RadiansPerDegree), s.distance * -sin(s.angle * Constant.RadiansPerDegree))

                { m with leftstick = Some s; cameraState = (FreeFlyController.startAnimation { m.cameraState with moveVec = V3d(pos.X,0.0,pos.Y) }) }
           | LeftTouchEnd -> 
                { m with leftstick = None; cameraState = (FreeFlyController.startAnimation { m.cameraState with moveVec = V3d.Zero }) }
           | Camera im -> { m with cameraState = FreeFlyController.update m.cameraState im}


    let withTouchStick el =
        let rs = 
            [
                { name = "touchstick.js"; url = "touchstick.js"; kind = Script }
                { name = "touch.css"; url = "touch.css"; kind = Stylesheet }
                { name = "hammerjs"; url = "https://cdnjs.cloudflare.com/ajax/libs/hammer.js/2.0.8/hammer.js"; kind = Script }
            ]       
        require rs (
            onBoot ("initTouchStick('__ID__', 100);") (
                el
            )
        )
        

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
        div [] [
            //text "stick:"
            //text "    "
            ////Incremental.text (m.stick |> Mod.map (fun o -> match o with | None -> "nothing" | Some v -> "dist="+string v.distance+" ang="+string v.angle))
            //br []
            
            
            FreeFlyController.controlledControl m.cameraState Camera (Frustum.perspective 60.0 0.1 1000.0 1.0 |> Mod.constant) 
                        (AttributeMap.ofList [ style "width: 100vw; height:100vh"; 
                                                attribute "showFPS" "true";       // optional, default is false
                                                attribute "data-samples" "8"
                                                //onEvent "onRendered" [] (fun _ -> SetTime)
                                                ]) 
                        (viewScene m)

            withTouchStick (div [
                style "position:fixed;top:0;left:0;width:50vw;height:100vh;padding:0;margin:0;border:0;z-index:2;background-color:rgba(255,255,0,0.3);"
                onEvent "touchstickstart" [] (( fun args -> 
                    match args with
                    | [d;a] -> { distance = float d; angle = float a } |> LeftTouchStart
                    | _ -> failwith ""
                ))
                onEvent "touchstickmove" [] (( fun args -> 
                    match args with
                    | [d;a] -> { distance = float d; angle = float a } |> LeftTouchUpdate
                    | _ -> failwith ""
                ))
                onEvent "touchstickstop" [] ( fun _ -> LeftTouchEnd)
            ] [])
            
            withTouchStick (div [
                style "position:fixed;top:0;left:50vw;width:50vw;height:100vh;padding:0;margin:0;border:0;z-index:2;background-color:rgba(255,0,255,0.3);"
                onEvent "touchstickstart" [] (( fun args -> 
                    match args with
                    | [d;a] -> { distance = float d; angle = float a } |> RiteTouchStart
                    | _ -> failwith ""
                ))
                onEvent "touchstickmove" [] (( fun args -> 
                    match args with
                    | [d;a] -> { distance = float d; angle = float a } |> RiteTouchUpdate
                    | _ -> failwith ""
                ))
                onEvent "touchstickstop" [] ( fun _ -> RiteTouchEnd)
            ] [])
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
