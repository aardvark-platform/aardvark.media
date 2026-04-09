namespace Aardvark.UI

open System
open System.Threading
open FSharp.Data.Adaptive

type IDomNode<'msg> = interface end

type IMutableApp =
    inherit IDisposable
    abstract member UpdateLock : obj
    abstract member CancellationToken : CancellationToken
    abstract member Register : resource: IDisposable -> unit

type IMutableApp<'model, 'msg> =
    inherit IMutableApp
    abstract member Dom : IDomNode<'msg>
    abstract member Model : aval<'model>
    abstract member Update : session: Guid * messages: 'msg seq -> unit

type IApp<'model, 'msg> =
    abstract member Start : unit -> IMutableApp<'model, 'msg>

type IApp<'model, 'mmodel, 'msg> =
    inherit IApp<'model, 'msg>
    abstract member Initial : 'model
    abstract member Update : model: 'model * message: 'msg -> 'model
    abstract member Threads : 'model -> ThreadPool<'msg>
    abstract member View : state: 'mmodel -> IDomNode<'msg>

type ISubApp<'model, 'inner, 'outer> =
    inherit IApp<'model, 'inner>
    abstract member ToOuter : 'model * 'inner -> seq<'outer>
    abstract member ToInner : 'model * 'outer -> seq<'inner>