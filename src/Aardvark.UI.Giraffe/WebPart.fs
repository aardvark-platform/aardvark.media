namespace Aardvark.UI.Giraffe

open System.Reflection
open Aardvark.Rendering
open Aardvark.UI

type WebPart = Giraffe.Core.HttpHandler

module WebPart =
    let ofAssembly (assembly: Assembly) : WebPart =
        HttpBackend.Instance.assembly assembly

module MutableApp =
    let toWebPart' (runtime : IRuntime) (useGpuCompression: bool) (app: MutableApp<'model, 'mmodel, 'msg>) : WebPart =
        MutableApp.toWebPart' HttpBackend.Instance runtime useGpuCompression app

    let toWebPart (runtime : IRuntime) (app: MutableApp<'model, 'mmodel, 'msg>) : WebPart =
        MutableApp.toWebPart HttpBackend.Instance runtime app