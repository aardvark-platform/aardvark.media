open Examples

open System
open System.Windows.Forms
open Suave
open Suave.Filters
open Suave.Operators

open Aardvark.UI
open Aardvark.Base
open Aardvark.Application.WinForms

let response (statusCode : HttpCode) =
    fun (ctx : HttpContext) ->
      let response =
        { ctx.response with status = statusCode.status; content = HttpContent.NullContent }
      { ctx with response = response } |> succeed


[<EntryPoint; STAThread>]
let main argv = 

    Xilium.CefGlue.ChromiumUtilities.unpackCef()
    Chromium.init argv

    let useVulkan = false

    Ag.initialize()
    Aardvark.Init()

    let app, runtime = 
        if useVulkan then
             let app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication(false) 
             app :> IDisposable, app.Runtime :> IRuntime
         else 
             let app = new OpenGlApplication()
             app :> IDisposable, app.Runtime :> IRuntime
    use app = app
    
    use form = new Form(Width = 1024, Height = 768)

    let app = MultiviewApp.app

    let mapp = app |> App.start

    let folderRx = System.Text.RegularExpressions.Regex @"^/(?<name>[^/]+)"

    let folder (name : string) (inner : list<WebPart>) : WebPart =
        fun (ctx : HttpContext) ->
            let url = ctx.request.url
            let path = url.PathAndQuery
            let prefix = "/" + name
            if path.StartsWith prefix then
                let newUri = Uri(url.Scheme + "://" + url.Host  + ":" + string url.Port + path.Substring prefix.Length)
                choose inner { ctx with request = { ctx.request with url = newUri } }
            else
                never ctx

    
    let dynamicFolder (content : string -> list<WebPart>) : WebPart =
        fun (ctx : HttpContext) ->
            let url = ctx.request.url
            let path = url.PathAndQuery
            let m = folderRx.Match(path)

            if m.Success then
                let name = m.Groups.["name"].Value
                let inner = content name
                match inner with
                    | [] -> 
                        never ctx
                    | _ -> 
                        let newUri = Uri(url.Scheme + "://" + url.Host  + ":" + string url.Port + path.Substring m.Length)
                        choose inner { ctx with request = { ctx.request with url = newUri } }
            else
                never ctx


    WebPart.startServer 4321 [ 
//        folder "hugo" [
////            path "/a" >=> Successful.OK "YEAH"
////            path "/b" >=> Successful.OK "YIPPIE"
////
////            dynamicFolder <| fun str -> 
////                [path "/" >=> Successful.OK str]
//
//        ]
        
        MutableApp.toWebPart' runtime false mapp

        Suave.Files.browseHome

        response HttpCode.HTTP_404

    ] 

    //Console.ReadLine() |> ignore
    use ctrl = new AardvarkCefBrowser()
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl
    ctrl.StartUrl <- "http://localhost:4321/?viewType=simple"
    //ctrl.ShowDevTools()

    Application.Run form
    0 
