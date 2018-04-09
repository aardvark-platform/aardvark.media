open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.UI

open Suave
open Suave.WebPart
open Suave.Filters
open Suave.Successful
open Suave.Operators

type Dummy = Dummy

[<EntryPoint; STAThread>]
let main argv = 

    Xilium.CefGlue.ChromiumUtilities.unpackCef()
    Chromium.init argv

    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication()
    use form = new Form(Width = 1024, Height = 600)

    let instance = 
        App.app |> App.start

    WebPart.startServer 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        prefix "/resources" >=> (Suave.Embedded.browse typeof<Dummy>.Assembly)
        Suave.Files.browseHome
    ]  


    use ctrl = new AardvarkCefBrowser()
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl
    ctrl.StartUrl <- "http://localhost:4321/"
    ctrl.ShowDevTools()
    form.Text <- "Examples"
    form.Icon <- Icons.aardvark 

    Application.Run form
    0 
