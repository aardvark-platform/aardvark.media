namespace Aardvark.Service.Suave

open System.Reflection
open Aardvark.Rendering
open Aardvark.UI

type WebPart = Suave.Http.WebPart

module WebPart =
    let ofAssembly (assembly: Assembly) : WebPart =
        HttpBackend.Instance.assembly assembly

module MutableApp =
    let toWebPart' (runtime : IRuntime) (useGpuCompression: bool) (app: MutableApp<'model, 'mmodel, 'msg>) : WebPart =
        ThreadPoolAdjustment.adjust()
        MutableApp.toWebPart' HttpBackend.Instance runtime useGpuCompression app

    let toWebPart (runtime : IRuntime) (app: MutableApp<'model, 'mmodel, 'msg>) : WebPart =
        ThreadPoolAdjustment.adjust()
        MutableApp.toWebPart HttpBackend.Instance runtime app