open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application.WinForms
open Aardvark.Cef.OffScreen
open FSharp.Data.Adaptive
open System
open System.Windows.Forms

module Shader =
    open FShade

    let flipTexture (v : Effects.Vertex) =
        fragment {
            return { v with tc = V2f(v.tc.X, 1.0f - v.tc.Y) }
        }

[<EntryPoint>]
let main argv =
    Aardvark.Init()
    use _ = AardvarkCef.Init()

    use app = new OpenGlApplication()
    let win = app.CreateSimpleRenderWindow()
    win.Text <- "Aardvark rocks media \\o/"

    use browser = AardvarkCef.CreateBrowser(app.Runtime, win.Sizes, false)
    browser.LoadUrl "https://orf.at/"

    let sampler =
        { SamplerState.Default with
            Filter = TextureFilter.MinMagPoint
            AddressU = WrapMode.Clamp
            AddressV = WrapMode.Clamp }

    let sg =
        Sg.fullScreenQuad
        |> Sg.diffuseTexture browser.Texture
        |> Sg.samplerState' DefaultSemantic.DiffuseColorTexture sampler
        |> Sg.shader {
            do! Shader.flipTexture
            do! DefaultSurfaces.diffuseTexture
        }

    browser.SetFocus true
    browser.Mouse.Use(win.Mouse) |> ignore
    browser.Keyboard.Use(win.Keyboard) |> ignore

    let createCursor, destroyCursor =
        let mutable current = null

        let create (handle: nativeint) =
            current.TryDispose() |> ignore
            let cursor = new Cursor(handle)
            current <- cursor
            cursor

        let destroy() =
            Try.Dispose(&current)

        create, destroy

    browser.CursorChanged.Add (fun args ->
        win.Invoke (Action (fun _ ->
            win.Cursor <- createCursor args.Handle
        )) |> ignore
    )

    let task =
        RenderTask.ofList [
            app.Runtime.CompileClear(win.FramebufferSignature, AVal.constant C4f.Gray)
            app.Runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    win.RenderTask <- task
    win.Run()

    destroyCursor()

    0