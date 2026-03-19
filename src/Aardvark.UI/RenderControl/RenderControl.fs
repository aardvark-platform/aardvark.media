namespace Aardvark.UI

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.UI
open Aardvark.UI.Semantics
open FSharp.Data.Adaptive
open FSharp.Data.Traceable

[<AutoOpen>]
module ``RenderControl DomNode`` =

    let private eventNames =
        Map.ofList [
            SceneEventKind.Click,           "onclick"
            SceneEventKind.DoubleClick,     "ondblclick"
            SceneEventKind.Down,            "onmousedown"
            SceneEventKind.Up,              "onmouseup"
            SceneEventKind.Move,            "onmousemove"
        ]

    let private needsButton =
        let needingButton = Set.ofList [SceneEventKind.Click; SceneEventKind.DoubleClick; SceneEventKind.Down; SceneEventKind.Up]
        fun k -> Set.contains k needingButton

    let private button (code : int) =
        match code with
        | 1 -> MouseButtons.Left
        | 2 -> MouseButtons.Middle
        | 3 -> MouseButtons.Right
        | _ -> MouseButtons.None

    type DomNode with
        static member RenderControl(attributes : AttributeMap<'msg>, processor : SceneEventProcessor<'msg>,
                                    getState : RenderClientInfo -> RenderState, scene : Scene) =

            let perform (session: Guid) (id: string) (kind : SceneEventKind)
                        (buttons : MouseButtons) (alt: bool) (shift: bool) (ctrl: bool) (pos : V2i) : seq<'msg> =
                match scene.TryGetClientInfo { session = session; elementId = id } with
                | ValueSome info ->
                    let pp = PixelPosition(pos.X, pos.Y, info.size.X, info.size.Y)
                    let ray = info.state |> RenderState.pickRay pp |> FastRay3d |> RayPart

                    let evt =
                        {
                            evtKind     = kind
                            evtPixel    = pos
                            evtRay      = ray
                            evtButtons  = buttons
                            evtAlt      = alt
                            evtShift    = shift
                            evtCtrl     = ctrl
                            evtTrafo    = AVal.constant Trafo3d.Identity
                            evtView     = info.state.viewTrafo
                            evtProj     = info.state.projTrafo
                            evtViewport = info.size
                        }

                    processor.Process(session, evt)

                | ValueNone ->
                    Log.warn "[UI] could not get client info for %A/%s" session id
                    Seq.empty

            let rayEvent (includeButton : bool) (kind : SceneEventKind) =
                let args =
                    if includeButton then ["event.offsetX"; "event.offsetY"; "event.altKey"; "event.shiftKey"; "event.ctrlKey"; "event.which"]
                    else ["event.offsetX"; "event.offsetY"; "event.altKey"; "event.shiftKey"; "event.ctrlKey" ]

                {
                    clientSide = fun send id -> send id args + ";"
                    serverSide = fun session id args ->
                        match args with
                        | x :: y :: alt :: shift :: ctrl :: which :: _ ->
                            let x = round (float x) |> int
                            let y = round (float y) |> int
                            let button = int which |> button
                            let alt = Boolean.Parse alt
                            let shift = Boolean.Parse shift
                            let ctrl = Boolean.Parse ctrl
                            perform session id kind button alt shift ctrl (V2i(x,y))

                        | x :: y :: alt :: shift :: ctrl :: _ ->
                            let vx = round (float x) |> int
                            let vy = round (float y) |> int
                            let alt = Boolean.Parse alt
                            let shift = Boolean.Parse shift
                            let ctrl = Boolean.Parse ctrl
                            perform session id kind MouseButtons.None alt shift ctrl (V2i(vx,vy))

                        | _ ->
                            Seq.empty

                }

            let events =
                processor.NeededEvents
                |> ASet.map (fun k ->
                    let kind =
                        match k with
                        | SceneEventKind.Enter | SceneEventKind.Leave -> SceneEventKind.Move
                        | _ -> k

                    let button = needsButton kind
                    eventNames.[kind], AttributeValue.Event(rayEvent button kind)
                )
                |> AMap.ofASet
                |> AMap.map (fun _ vs ->
                    match HashSet.toList vs with
                    | [] -> AttributeValue.Event Event.empty
                    | h :: rest -> rest |> List.fold (AttributeValue.combine "urdar") h
                )
                |> AttributeMap.ofAMap

            let ownAttributes =
                AttributeMap.unionMany [
                    events
                    attributes
                    AttributeMap.single "class" (AttributeValue.String "aardvark")
                ]

            let boot (id : string) =
                $"aardvark.getRenderer(\"{id}\");"

            DomNode.Scene(ownAttributes, scene, getState).WithBoot(ValueSome boot)

        static member RenderControl(attributes : AttributeMap<'msg>, getState : RenderClientInfo -> RenderState, scene : Scene) =
            DomNode.RenderControl(attributes, SceneEventProcessor.empty, getState, scene)

        static member RenderControl(attributes : AttributeMap<'msg>, camera : aval<Camera>, scene : Scene) =
            let getState (c : RenderClientInfo) =
                let cam = camera.GetValue(c.token)
                let cam = { cam with frustum = cam.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y) }

                {
                    viewTrafo = CameraView.viewTrafo cam.cameraView
                    projTrafo = Frustum.projTrafo cam.frustum
                }

            DomNode.RenderControl(attributes, SceneEventProcessor.empty, getState, scene)


        static member RenderControl(attributes : AttributeMap<'msg>, camera : aval<Camera>,
                                    sg : RenderClientValues -> ISg<'msg>, config: RenderControlConfig) =

            let getState (c : RenderClientInfo) =
                let camera = camera.GetValue(c.token)
                let frustum = camera.frustum |> config.adjustAspect c.size

                {
                    viewTrafo = CameraView.viewTrafo camera.cameraView
                    projTrafo = Frustum.projTrafo frustum
                }

            let tree = AVal.init <| PickTree.ofSg (Sg.ofList [])

            let globalPicks = AVal.init AMap.empty

            let scene =
                Scene.custom (fun values ->
                    let sg =
                        sg values
                        |> Sg.viewTrafo values.viewTrafo
                        |> Sg.projTrafo values.projTrafo
                        |> Sg.uniform "ViewportSize" values.size

                    transact ( fun _ -> tree.Value <- PickTree.ofSg sg )

                    transact ( fun _ -> globalPicks.Value <- sg.GlobalPicks(Ag.Scope.Root) )

                    values.runtime.CompileRender(values.signature, sg)
                )

            let proc =
                { new SceneEventProcessor<'msg>() with
                    member x.NeededEvents =
                        aset {
                            let! tree = tree
                            let! globalPicks = globalPicks
                            yield! ASet.union (AMap.keys globalPicks) tree.Needed
                        }

                    member x.Process (source : Guid, evt : SceneEvent) =
                        seq {
                            let _, msgs = tree.GetValue().Perform(evt)
                            yield! msgs

                            let m = globalPicks.GetValue().Content |> AVal.force
                            match m |> HashMap.tryFindV evt.kind with
                            | ValueSome cb -> yield! cb evt
                            | ValueNone -> ()
                    }
                }

            DomNode.RenderControl(attributes, proc, getState, scene)

        static member RenderControl(attributes : AttributeMap<'msg>, camera : aval<Camera>, sg : ISg<'msg>, config: RenderControlConfig) =
            DomNode.RenderControl(attributes, camera, constF sg, config)

        static member RenderControl(camera : aval<Camera>, scene : ISg<'msg>, config : RenderControlConfig) : DomNode<'msg> =
            DomNode.RenderControl(AttributeMap.empty, camera, constF scene, config)

        static member RenderControl(camera : aval<Camera>, scene : RenderClientValues -> ISg<'msg>, config : RenderControlConfig) : DomNode<'msg> =
            DomNode.RenderControl(AttributeMap.empty, camera, scene, config)

        static member RenderControl(attributes : AttributeMap<'msg>, camera : aval<Camera>, sgs : RenderClientValues -> alist<RenderCommand<'msg>>) =

            let getState (c : RenderClientInfo) =
                let camera = camera.GetValue(c.token)
                let frustum = camera.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y)

                {
                    viewTrafo = CameraView.viewTrafo camera.cameraView
                    projTrafo = Frustum.projTrafo frustum
                }

            let trees = AVal.init AList.empty
            let globalPicks = AVal.init ASet.empty

            let scene =
                Scene.custom (fun values ->

                    let sgs = sgs values

                    let t =
                      sgs |> AList.choose (function RenderCommand.Clear _ -> None | SceneGraph sg -> PickTree.ofSg sg |> Some)
                    let g =
                      sgs |> AList.toASet |> ASet.choose (function RenderCommand.Clear _ -> None | SceneGraph sg -> sg.GlobalPicks(Ag.Scope.Root) |> Some)

                    transact (fun _ -> trees.Value <- t; globalPicks.Value <- g)

                    let mutable reader : IOpReader<IndexList<IRenderTask>, IndexListDelta<IRenderTask>> =
                        let input = sgs.GetReader()
                        { new AbstractReader<IndexList<IRenderTask>, IndexListDelta<IRenderTask>>(IndexList.trace) with
                            member x.Compute(token) =
                                input.GetChanges token |> IndexListDelta.choose (fun index op ->
                                    match op with
                                    | Set v ->
                                        match x.State.TryGet index with
                                        | Some o -> o.Dispose()
                                        | None -> ()
                                        let task = v.Compile(values)
                                        Some (Set task)

                                    | Remove ->
                                        match x.State.TryGet index with
                                        | Some o ->
                                            o.Dispose()
                                            Some Remove
                                        | None -> None

                                )
                        } :> _

                    let update (t : AdaptiveToken) =
                        reader.GetChanges t |> ignore

                    { new AbstractRenderTask() with
                        override x.PerformUpdate(t,rt) =
                            update t
                        override x.Perform(t,rt,o) =
                            update t
                            for task in reader.State do
                                task.Run(t,rt,o)
                        override x.Release() =
                            for task in reader.State do
                                task.Dispose()
                            reader <- Unchecked.defaultof<_>

                        override x.Use f = f ()
                        override x.FramebufferSignature = Some values.signature
                        override x.Runtime = Some values.runtime
                    } :> IRenderTask)

            let globalNeeded = ASet.bind id globalPicks |> ASet.collect AMap.keys
            let treeNeeded = AList.bind id trees |> AList.toASet |> ASet.collect _.Needed
            let needed = ASet.union globalNeeded treeNeeded


            let rec pickTrees (trees : list<PickTree<'msg>>) evt =
                match trees with
                | [] -> false, Seq.empty
                | x::xs ->
                    let consumed,msgs = pickTrees xs evt
                    if consumed then true,msgs
                    else
                        let consumed, other = x.Perform evt
                        consumed, Seq.append msgs other

            let proc =
                { new SceneEventProcessor<'msg>() with
                    member x.NeededEvents = needed
                    member x.Process (source : Guid, evt : SceneEvent) =
                        let trees = trees |> AVal.force
                        let _ = trees.Content |> AVal.force |> IndexList.toList
                        let globalPicks = globalPicks |> AVal.force

                        seq {
                            for perScene in globalPicks.Content |> AVal.force do
                                let picks = perScene.Content |> AVal.force
                                match picks |> HashMap.tryFindV evt.kind with
                                | ValueSome cb ->
                                    yield! cb evt
                                | ValueNone ->
                                    ()
                        }
                }

            DomNode.RenderControl(attributes, proc, getState, scene)

        static member RenderControl(attributes : AttributeMap<'msg>, camera : aval<Camera>, sgs : alist<RenderCommand<'msg>>) =
            let getState (c : RenderClientInfo) =
                let camera = camera.GetValue(c.token)
                let frustum = camera.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y)

                {
                    viewTrafo = CameraView.viewTrafo camera.cameraView
                    projTrafo = Frustum.projTrafo frustum
                }

            let scene =
                Scene.custom (fun values ->
                    let mutable reader : IOpReader<IndexList<IRenderTask>, IndexListDelta<IRenderTask>> =
                        let input = sgs.GetReader()
                        { new AbstractReader<IndexList<IRenderTask>, IndexListDelta<IRenderTask>>(IndexList.trace) with
                            member x.Compute(token) =
                                input.GetChanges token |> IndexListDelta.choose (fun index op ->
                                    match op with
                                    | Set v ->
                                        match x.State.TryGet index with
                                        | Some o -> o.Dispose()
                                        | None -> ()
                                        let task = v.Compile(values)
                                        Some (Set task)

                                    | Remove ->
                                        match x.State.TryGet index with
                                        | Some o ->
                                            o.Dispose()
                                            Some Remove
                                        | None -> None

                                )
                        } :> _

                    let update (t : AdaptiveToken) =
                        reader.GetChanges t |> ignore

                    { new AbstractRenderTask() with
                        override x.PerformUpdate(t,rt) =
                            update t
                        override x.Perform(t,rt,o) =
                            update t
                            for task in reader.State do
                                task.Run(t,rt,o)
                        override x.Release() =
                            for t in reader.State do
                                t.Dispose()
                            reader <- Unchecked.defaultof<_>


                        override x.Use f = f ()
                        override x.FramebufferSignature = Some values.signature
                        override x.Runtime = Some values.runtime
                    } :> IRenderTask

                )

            let trees = sgs |> AList.choose (function Clear _ -> None | SceneGraph sg -> PickTree.ofSg sg |> Some)
            let globalPicks = sgs |> AList.toASet |> ASet.choose (function Clear _ -> None | SceneGraph sg -> sg.GlobalPicks(Ag.Scope.Root) |> Some)

            let globalNeeded = globalPicks |> ASet.collect AMap.keys
            let treeNeeded = trees |> AList.toASet |> ASet.collect _.Needed
            let needed = ASet.union globalNeeded treeNeeded


            let rec pickTrees (trees : list<PickTree<'msg>>) evt =
                match trees with
                | [] -> false, Seq.empty
                | x::xs ->
                    let consumed,msgs = pickTrees xs evt
                    if consumed then true,msgs
                    else
                        let consumed, other = x.Perform evt
                        consumed, Seq.append msgs other

            let proc =
                { new SceneEventProcessor<'msg>() with
                    member x.NeededEvents = needed
                    member x.Process (source : Guid, evt : SceneEvent) =
                        let trees = trees.Content |> AVal.force |> IndexList.toList
                        seq {
                            let _, msgs = pickTrees trees evt
                            yield! msgs

                            for perScene in globalPicks.Content |> AVal.force do
                                let picks = perScene.Content |> AVal.force
                                match picks |> HashMap.tryFindV evt.kind with
                                | ValueSome cb ->
                                    yield! cb evt
                                | ValueNone ->
                                    ()
                        }
                }

            DomNode.RenderControl(attributes, proc, getState, scene)