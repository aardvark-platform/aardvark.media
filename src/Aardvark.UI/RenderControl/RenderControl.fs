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
                    if includeButton then ["event.offsetX"; "event.offsetY"; "event.altKey"; "event.shiftKey"; "event.ctrlKey"; "event.button"]
                    else ["event.offsetX"; "event.offsetY"; "event.altKey"; "event.shiftKey"; "event.ctrlKey" ]

                {
                    clientSide = fun send id -> send id args + ";"
                    serverSide = fun session id args ->
                        match args with
                        | x :: y :: alt :: shift :: ctrl :: buttons :: _ ->
                            let x = round (float x) |> int
                            let y = round (float y) |> int
                            let button = MouseButtons.parseEventButton buttons
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

            let shutdown (id : string) =
                $"aardvark.destroyRenderer(\"{id}\");"

            DomNode.Scene(ownAttributes, scene, getState).WithBoot(ValueSome boot).WithShutdown(ValueSome shutdown)

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