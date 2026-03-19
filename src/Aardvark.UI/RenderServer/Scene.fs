namespace Aardvark.UI

open System.Collections.Concurrent
open System.Threading

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

[<AbstractClass>]
type Scene() =
    let cache = ConcurrentDictionary<IFramebufferSignature, ConcreteScene>()
    let clientInfos = ConcurrentDictionary<RenderClientId, unit -> RenderClientInfo>()

    member internal _.AddClientInfo(id: RenderClientId, getter: unit -> RenderClientInfo) =
        clientInfos.TryAdd(id, getter) |> ignore

    member internal _.RemoveClientInfo(id: RenderClientId) =
        clientInfos.TryRemove id |> ignore

    member internal _.TryGetClientInfo(id: RenderClientId) : RenderClientInfo voption =
        match clientInfos.TryGetValue id with
        | true, getter -> ValueSome <| getter()
        | _ -> ValueNone

    member internal this.GetConcreteScene(signature: IFramebufferSignature) =
        cache.GetOrAdd(signature, fun signature -> ConcreteScene(this, signature))

    abstract member Compile : RenderClientValues -> IRenderTask

and internal ConcreteScene(scene: Scene, signature: IFramebufferSignature) as this =
    inherit AdaptiveObject()

    let mutable refCount = 0
    let mutable task : IRenderTask option = None

    let values = RenderClientValues(signature)

    let destroy _ =
        let deadTask =
            lock this (fun () ->
                if refCount = 0 then
                    match task with
                    | Some t ->
                        task <- None
                        Some t
                    | None ->
                        Log.error "[Scene] Invalid state"
                        None
                else
                    None
            )

        match deadTask with
        | Some t ->
            t.Dispose()
            Log.line "[Scene] Destroyed"
        | None ->
            ()

    let timer = new Timer(TimerCallback(destroy), null, Timeout.Infinite, Timeout.Infinite)

    let create() =
        refCount <- refCount + 1
        if refCount = 1 then
            match task with
            | Some task ->
                // refCount was 0 but task was not deleted
                timer.Change(Timeout.Infinite, Timeout.Infinite) |> ignore
                task
            | None ->
                // refCount was 0 and there was no task
                Log.line "[Scene] Created"
                let t = scene.Compile values
                task <- Some t
                t
        else
            match task with
            | Some t -> t
            | None -> failwithf "[Scene] Invalid state"

    let release() =
        refCount <- refCount - 1

        if refCount = 0 then
            timer.Change(Config.sceneDestroyTimeout, Timeout.Infinite) |> ignore

    member _.Scene = scene

    member internal _.Apply(info: RenderClientInfo) =
        values.Update(info)

    member internal _.Values = values

    member internal this.CreateNewRenderTask() =
        lock this (fun () ->
            let task = create()

            { new AbstractRenderTask() with
                member _.FramebufferSignature = task.FramebufferSignature
                member _.Runtime              = task.Runtime
                member _.PerformUpdate(t, rt) = task.Update(t, rt)
                member _.Perform(t, rt, o)    = task.Run(t, rt, o)
                member _.Release()            = lock this release
                member _.Use action           = task.Use action
            } :> IRenderTask
        )

    member _.FramebufferSignature = signature

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Scene =

    [<AutoOpen>]
    module private Implementation =
        type EmptyScene() =
            inherit Scene()
            override x.Compile _ = RenderTask.empty

        type ArrayScene(scenes: Scene[]) =
            inherit Scene()
            override _.Compile(values) =
                scenes |> Array.map (fun s -> s.Compile values) |> RenderTask.ofArray

        type CustomScene(compile: RenderClientValues -> IRenderTask) =
            inherit Scene()
            override _.Compile(values) = compile values

    let empty =
        EmptyScene() :> Scene

    let custom (compile : RenderClientValues -> IRenderTask) =
        CustomScene(compile) :> Scene

    let ofArray (scenes: Scene[]) =
        ArrayScene(scenes) :> Scene

    let ofList (scenes: list<Scene>) =
        ArrayScene(List.toArray scenes) :> Scene

    let ofSeq (scenes: seq<Scene>) =
        ArrayScene(Seq.toArray scenes) :> Scene