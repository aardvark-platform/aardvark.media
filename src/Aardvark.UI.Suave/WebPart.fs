namespace Aardvark.UI.Suave

open System.Reflection
open Aardvark.Rendering
open Aardvark.UI

type WebPart = Suave.Http.WebPart

module WebPart =
    let ofAssembly (assembly: Assembly) : WebPart =
        HttpBackend.Instance.assembly assembly

    let ofType<'T> : WebPart =
        ofAssembly typeof<'T>.Assembly

module MutableApp =
    let toWebPart' (runtime : IRuntime) (useGpuCompression: bool) (app: MutableApp<'model, 'mmodel, 'msg>) : WebPart =
        ThreadPoolAdjustment.adjust()
        MutableApp.toWebPart' HttpBackend.Instance runtime useGpuCompression app

    let toWebPart (runtime : IRuntime) (app: MutableApp<'model, 'mmodel, 'msg>) : WebPart =
        ThreadPoolAdjustment.adjust()
        MutableApp.toWebPart HttpBackend.Instance runtime app