namespace Aardvark.UI

open FSharp.Data.Adaptive

type App<'model, 'mmodel, 'msg> =
    {
        unpersist : Unpersist<'model, 'mmodel>
        initial   : 'model
        threads   : 'model -> ThreadPool<'msg>
        update    : 'model -> 'msg -> 'model
        view      : 'mmodel -> DomNode<'msg>
    }

    member this.start() =
        new MutableApp<'model, 'mmodel, 'msg>(this, this.unpersist)

    interface IApp<'model, 'mmodel, 'msg> with
        member this.Initial = this.initial
        member this.Update(model, message) = this.update model message
        member this.View(state) = this.view state
        member this.Threads(model) = this.threads model
        member x.Start() = x.start()

module App =
    let start (app: App<'model, 'mmodel, 'msg>) = app.start()