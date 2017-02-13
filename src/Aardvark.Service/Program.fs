open Aardvark.Service

open System

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms

[<EntryPoint>]
let main args =

    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication()
    let runtime = app.Runtime


    Server.start runtime 8888 (fun id yeah ->
        let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
        let proj = yeah.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

        let view =
            view |> DefaultCameraController.control yeah.Mouse yeah.Keyboard yeah.Time

        let trafo = yeah.Time |> Mod.map (fun dt -> Trafo3d.RotationZ(float dt.Ticks / float TimeSpan.TicksPerSecond))

        let sg =
            Sg.box' C4b.Red (Box3d(-V3d.III, V3d.III))
                |> Sg.trafo trafo
                |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)
                |> Sg.diffuseFileTexture' @"C:\Users\Schorsch\Development\WorkDirectory\cliffs_color.jpg" true
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.diffuseTexture
                    do! DefaultSurfaces.simpleLighting
                }


        let task = runtime.CompileRender(yeah.FramebufferSignature, sg)
        Some task
    )

    Console.ReadLine() |> ignore
    0

