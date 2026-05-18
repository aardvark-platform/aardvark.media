open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Application.WinForms
open Aardvark.UI
open Aardvark.UI.Suave
open Aardvark.Cef.WinForms

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    use _ = AardvarkCef.Init()

    use app = new OpenGlApplication()
    use mapp = App.app |> App.start

    // use can use whatever suave server to start you mutable app. 
    // startServerLocalhost is one of the convinience functions which sets up 
    // a server without much boilerplate.
    // there is also WebPart.startServer and WebPart.runServer. 
    // look at their implementation here: https://github.com/aardvark-platform/aardvark.media/blob/master/src/Aardvark.UI.Suave/Suave.fs#L10
    // if you are unhappy with them, you can always use your own server config.
    // the localhost variant does not require to allow the port through your firewall.
    // the non localhost variant runs in 127.0.0.1 which enables remote acces (e.g. via your mobile phone)
    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' app.Runtime false mapp
        WebPart.ofAssembly <| Reflection.Assembly.GetEntryAssembly()
    ] |> ignore

    use form = new Form()
    form.Width <- 800
    form.Height <- 600

    use browser = AardvarkCef.CreateBrowser("http://localhost:4321/")
    form.Controls.Add browser
    form.Text <- "Examples"
    browser.OpenDevTools()

    Application.Run form
    form.Controls.Remove browser

    0 
