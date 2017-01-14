
open Scratch
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
                return browserSampler.[pixel]
            else
                return V4d(0.0,0.0,0.0,0.0)
        }

    let overlay (v : Effects.Vertex) =
        fragment {
            let coord = V2d(0.5 + 0.5 * v.pos.X, 0.5 + 0.5 * v.pos.Y)
            let textureSize = browserSampler.Size
            let offset : V2i = uniform?TextureOffset

            //let offset = V2i(offset.X, uniform.ViewportSize.Y - offset.Y - 1)

            let pixel = (V2d uniform.ViewportSize * coord |> V2i) - offset  

            if pixel.X < textureSize.X && pixel.Y < textureSize.Y && pixel.X >= 0 && pixel.Y >= 0 then
                let color = browserSampler.[pixel]
                return color
            else
                return V4d(0.0,0.0,0.0,0.0)
        }

    let overlay2 (v : Effects.Vertex) =
        fragment {
            return browserSampler.SampleLevel(v.tc, 0.0) + V4d(1.5,0.5,0.5,1.0)
        }
open Fable.Helpers.Virtualdom
open Fable.Helpers.Virtualdom.Html

module TestApp =

    open Fablish

    type Model = { cnt : int; info : string }

    type Action = Inc | Dec | Reset | SetInfo of string

    let update (m : Model) (a : Action) =
        printfn "[Test] computing udpate"
        match a with
            | Inc -> { m with cnt = m.cnt + 1 }
            | Dec -> { m with cnt = m.cnt - 1 }
            | Reset -> { m with cnt = 0 }
            | SetInfo info -> { m with info = info}

    let view (m : Model) : DomNode<Action> =
        printfn "[Test] Computing view"
        div [] [
            div [Style ["width", "100%"; "height", "100%"; "background-color", "transparent"]; attribute "id" "renderControl"] [
                text (sprintf "current content: %d" m.cnt)
                br []
                button [onMouseClick (fun dontCare -> Inc); attribute "class" "ui button"] [text "increment"]
                button [onMouseClick (fun dontCare -> Dec)] [text "decrement"]
                button [onMouseClick (fun dontCare -> Reset)] [text "reset"]
                br []
                text (sprintf "ray: %s" m.info)
            ]
        ]

    let app =
        {
            initial = { info = "not known"; cnt = 0 }
            update = update 
            view = view
            onRendered = OnRendered.ignore
        }

type AppMsg = RenderMsg of InteractionTest.TranslateController.Action
            | Ui of TestApp.Action

[<EntryPoint>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()

//    InteractionTest.InteractionTest.run() |> ignore
//    System.Environment.Exit 0
    
    Chromium.init()


    use app = new OpenGlApplication()
    use win = app.CreateSimpleRenderWindow()
    win.Visible <- false
    win.Text <- "Aardvark rocks media \\o/"

    let client = Browser(win.FramebufferSignature,win.Time,app.Runtime, true, win.Sizes)

    let mutable lastSize = 256 * V2i.II
    let renderControlViewport =
        adaptive {
            let! (size,version) = client.Size, client.Version
            let vp = client.GetViewport "renderControl"
            match vp with
                | Some vp ->
                    lastSize <- vp.Size
                    let vp2 = Box2i(V2i(vp.Min.X, size.Y - vp.Max.Y), V2i(vp.Max.X, size.Y - vp.Min.Y))
                    return vp2

                | None ->
                    return Box2i(V2i.OO,lastSize)
        }

    let renderRect = renderControlViewport |> Mod.map (fun s -> Box2i.FromMinAndSize(V2i(s.Min.X,win.Size.Y - s.Max.Y),s.Size))
    let cameraView = 
        CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
            //|> DefaultCameraController.control win.Mouse win.Keyboard win.Time
            |> Mod.constant 

    let frustum = 
        renderControlViewport
            |> Mod.map (fun b -> let s = b.Size in Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

    let mutable result = Unchecked.defaultof<Fablish.Fablish2.FablishResult<_,_>>

    let three3dToUi msg send =
        match msg with
            | InteractionTest.TranslateController.Action.Hover(x,p) -> 
                result.runningApp.EmitMessage (TestApp.Action.SetInfo (sprintf "hover: %A" (x,p)))
            | _ -> ()
        send msg
    
    let camera = Mod.map2 Camera.create cameraView frustum
    let three3dApp = InteractionTest.TranslateController.app camera
    let running = Elmish3DADaptive.createAppAdaptiveD win.Keyboard win.Mouse renderRect camera three3dToUi three3dApp

    let uiTo3d msg send =
        match msg with
            | TestApp.Action.Reset -> running.send InteractionTest.TranslateController.Action.ResetTrafo
            | _ -> ()
        send msg

    result <- Fablish.Fablish2.serveLocally "8083" (Some uiTo3d) TestApp.app
    let res = client.LoadUrlAsync "http://localhost:8083/mainPage"

    let fullscreenBrowser =
        Sg.fullScreenQuad
            |> Sg.diffuseTexture client.Texture 
            |> Sg.effect [
                Shader.fullScreen |> toEffect
               ]
            |> Sg.blendMode (Mod.constant BlendMode.Blend)
            |> Sg.pass (RenderPass.after "gui" RenderPassOrder.Arbitrary RenderPass.main)
    

    let scene =
        Sg.box' C4b.White Box3d.Unit
            |> Sg.effect [
                DefaultSurfaces.trafo          |> toEffect           
                DefaultSurfaces.constantColor C4f.Blue |> toEffect  
                ]
            |> Sg.viewTrafo (Mod.map CameraView.viewTrafo cameraView)
            |> Sg.projTrafo (Mod.map Frustum.projTrafo frustum)



    let sceneTask = 
        RenderTask.ofList [
            app.Runtime.CompileClear(win.FramebufferSignature, Mod.constant C4f.Green)
            app.Runtime.CompileRender(win.FramebufferSignature, running.sg)
        ]
    let sceneSize = renderControlViewport |> Mod.map (fun box -> box.Size )
    let renderContent = RenderTask.renderToColor sceneSize sceneTask

    let renderOverlay =
        Sg.fullScreenQuad
            |> Sg.diffuseTexture renderContent 
            |> Sg.effect [
                Shader.overlay |> toEffect
               ]
            |> Sg.uniform "TextureOffset" (renderControlViewport |> Mod.map (fun box -> box.Min ))


    let composite = Sg.ofSeq [renderOverlay; fullscreenBrowser] |> Sg.depthTest (Mod.constant DepthTestMode.None)
    
    client.SetFocus true
    client.Mouse.Use(win.Mouse) |> ignore
    client.Keyboard.Use(win.Keyboard) |> ignore

    let task =
        RenderTask.ofList [
            app.Runtime.CompileRender(win.FramebufferSignature, composite)
        ]

    
    win.RenderTask <- task
    
    win.Run()

    result.shutdown.Cancel()
    Chromium.shutdown()
    0