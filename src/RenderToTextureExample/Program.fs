open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Rendering.GL

open Aardvark.Cef

module Shader =
    open FShade

    let browserSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let fullScreen (v : Effects.Vertex) =
        fragment {
            let coord = V2d(0.5 + 0.5 * v.pos.X, 0.5 - 0.5 * v.pos.Y)
            let pixel = V2d uniform.ViewportSize * coord |> V2i
            let textureSize = browserSampler.Size

            if pixel.X < textureSize.X && pixel.Y < textureSize.Y then
                let color = browserSampler.[pixel]
                return color
            else
                return V4d(0.0,0.0,0.0,0.0)
        }

[<EntryPoint>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()
    
    Chromium.init()

    use app = new OpenGlApplication()
    let win = app.CreateSimpleRenderWindow()

    win.Text <- "Aardvark rocks media \\o/"

    let client = new Browser(win.FramebufferSignature,win.Time,app.Runtime, true, win.Sizes)
    let res = client.LoadUrl "http://orf.at/"
    printfn "%A" res

    let test =
        adaptive {
            let! (size,version) = client.Size, client.Version
            let vp = client.GetViewport "newsLogo"
            match vp with
                | Some vp ->
                    let bounds = Box2d(V2d vp.Min / V2d size, V2d vp.Max / V2d size)

                    let visible = bounds.Intersection(Box2d.Unit)

                    if not visible.IsEmpty then
                        Log.line "visible: %A" visible
                | None ->
                    ()

            return size
        }

    let sg =
        Sg.fullScreenQuad
            |> Sg.diffuseTexture client.Texture 
            |> Sg.effect [
                Shader.fullScreen |> toEffect
               ]
            |> Sg.uniform "ViewportSize" win.Sizes

    client.SetFocus true
    client.Mouse.Use(win.Mouse) |> ignore
    client.Keyboard.Use(win.Keyboard) |> ignore

    let task =
        RenderTask.ofList [
            app.Runtime.CompileClear(win.FramebufferSignature, Mod.constant C4f.Gray)
            app.Runtime.CompileRender(win.FramebufferSignature, sg)
                //|> DefaultOverlays.withStatistics
        ]

    
    win.RenderTask <- task
    win.Run()

    Chromium.shutdown()
    0