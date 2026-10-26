namespace Aardvark.UI.Tests

open System
open System.Collections.Generic
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.UI
open Aardvark.UI.Updaters
open FSharp.Data.Adaptive
open Expecto

module ``Updater Tests`` =

    let private eventName = "onready"
    let private jsonString = """("(?:\\.|[^"\\])*")"""

    let private singleMatch pattern code =
        let matches = Regex.Matches(code, pattern)
        Expect.equal matches.Count 1 "Expected exactly one emitted event call"
        matches.[0]

    // Decode the emitted literals independently of the JavaScript encoder and AST.
    let private activation code =
        let m = singleMatch ($"aardvark\\.setEventHandler\\({jsonString}, {jsonString}, (\\d+)\\);") code
        JsonSerializer.Deserialize<string>(m.Groups.[1].Value),
        JsonSerializer.Deserialize<string>(m.Groups.[2].Value),
        Byte.Parse(m.Groups.[3].Value)

    let private bootEvent code =
        let m = singleMatch ($"aardvark\\.processEvent\\({jsonString}, {jsonString}\\);") code
        JsonSerializer.Deserialize<string>(m.Groups.[1].Value),
        JsonSerializer.Deserialize<string>(m.Groups.[2].Value)

    let private createState () =
        let handlers = EventHandlers<string>()
        let messages = FSharp.Control.Event<string>()
        let state =
            {
                scenes = ContraDict.ofDictionary (Dictionary<string, SceneInfo<string>>())
                handlers = handlers :> ContraDict<_, _>
                references = Dictionary<ReferenceId, Reference>()
                channels = Dictionary<ChannelId, ChannelReader>()
                messages = messages.Publish
            }
        handlers, state

    // As in MutableApp: lookup activates the version, but absent args mean acknowledgement only.
    let private receive (handlers: EventHandlers<string>) sender name (version: byte voption) args =
        match handlers.TryGet(ChannelId(sender, name), version), args with
        | ValueSome handler, ValueSome args -> handler.invoke Guid.Empty sender args |> Seq.toList
        | _ -> []

    let private withUpdater tag attributes children action =
        let node =
            Incremental.elem tag attributes children
            |> onBoot "aardvark.processEvent(\"__ID__\", \"onready\");"
        let updater = node.NewUpdater(Unchecked.defaultof<IHttpRequest>)
        let handlers, state = createState ()
        let insert =
            if tag = "body" then ValueNone
            else ValueSome (fun self -> JSExpr.AppendChild(JSExpr.Body, self))
        try
            let code = updater.Update(AdaptiveToken.Top, state, insert) |> JSExpr.toString
            action updater handlers state code
        finally
            updater.Destroy(state, JSExpr.GetElementById updater.Id) |> ignore

    let private checkInitial tag =
        let mutable calls = 0
        let event = Event.ofTrigger (fun () -> calls <- calls + 1; "ready")
        let attributes = AttributeMap.single eventName (AttributeValue.Event event)
        withUpdater tag attributes AList.empty (fun updater handlers _ code ->
            let sender, name, version = activation code
            let bootSender, bootName = bootEvent code
            Expect.equal (bootSender, bootName) (updater.Id, eventName) "Boot must use the registered ID"
            Expect.isTrue (handlers.TryGet(ChannelId(bootSender, bootName))).IsNone "Initial handler must still be pending"
            Expect.equal (receive handlers sender name (ValueSome version) ValueNone) [] "Acknowledgement must not emit messages"
            Expect.equal calls 0 "Acknowledgement must not invoke the reaction"
            Expect.equal (receive handlers bootSender bootName ValueNone (ValueSome [])) ["ready"] "Versionless boot event must resolve the activated handler"
            Expect.equal calls 1 "Boot reaction must run exactly once"
            Expect.equal (sender, name, version) (updater.Id, eventName, 0uy) "Activation must use the registered ID and initial version"
        )

    // Flatten statement containers only; this is not a DOM or JavaScript interpreter.
    let rec private operations = function
        | JSExpr.Sequential children -> List.collect operations children
        | JSExpr.Let(name, value, body) -> JSExpr.Let(name, value, JSExpr.Nop) :: operations body
        | JSExpr.Nop -> []
        | operation -> [operation]

    let private child value =
        let attributes = AttributeMap.single eventName (AttributeValue.Event (Event.ofTrigger (fun () -> value)))
        Incremental.elem "button" attributes AList.empty
        |> onBoot "created(\"__ID__\");"
        |> onShutdown "retire(\"__ID__\");"

    let private checkUnregistered (handlers: EventHandlers<string>) id =
        let key = ChannelId(id, eventName)
        Expect.isTrue (handlers.TryGet key).IsNone "Retired handler must not remain current"
        Expect.isTrue (handlers.TryGet(key, 0uy)).IsNone "Retired handler must not remain pending or accept a late acknowledgement"
        Expect.equal (receive handlers id eventName ValueNone (ValueSome [])) [] "Retired element must not dispatch events"

    let private checkRemoval id expression =
        let ops = operations expression
        let removal = JSExpr.Remove(JSExpr.Var id)
        Expect.equal (ops |> List.filter (function JSExpr.Remove _ -> true | _ -> false)) [removal] "Retired element must be removed exactly once, without removing siblings"
        let lookup = JSExpr.Let(id, JSExpr.GetElementById id, JSExpr.Nop)
        let shutdown = JSExpr.Raw $"retire(\"{id}\");"
        let lifecycle = ops |> List.filter (fun op -> op = lookup || op = shutdown || op = removal)
        Expect.equal lifecycle [lookup; shutdown; removal] "Resolve the retired element, then shut it down while attached, then remove it"
        let code = JSExpr.toString expression
        let removeCode = $"{id}.remove();"
        Expect.equal (Regex.Matches(code, Regex.Escape removeCode).Count) 1 "Emitted JavaScript must remove the retired element exactly once"
        Expect.isTrue (code.IndexOf($"retire(\"{id}\");", StringComparison.Ordinal) < code.IndexOf(removeCode, StringComparison.Ordinal)) "Emitted shutdown must precede removal"
        Expect.equal (Regex.Matches(code, Regex.Escape $"document.getElementById(\"{id}\")").Count) 1 "Resolve the retired element only once"

    let private recordedHandler (calls: ResizeArray<string * Guid * string * string list>) version value : EventHandler<string> =
        { version = version
          invoke = fun session sender args ->
              calls.Add(value, session, sender, args)
              Seq.singleton value }

    let private sameHandler (expected: EventHandler<string>) (found: EventHandler<string> voption) =
        match found with
        | ValueSome actual ->
            Expect.isTrue (Object.ReferenceEquals(actual, expected)) "Preserve the exact handler record"
            Expect.isTrue (Object.ReferenceEquals(actual.invoke, expected.invoke)) "Preserve callback identity"
        | ValueNone -> failtest "Expected the registered handler"

    let private acknowledgeAndDispatch (handlers: EventHandlers<string>) (key: ChannelId) handler value (calls: ResizeArray<_>) =
        let count = calls.Count
        Expect.equal (receive handlers key.ElementId key.ChannelName (ValueSome handler.version) ValueNone) [] "Acknowledgements do not dispatch"
        sameHandler handler (handlers.TryGet key)
        Expect.equal calls.Count count "Activation must not invoke callbacks"
        Expect.equal (receive handlers key.ElementId key.ChannelName ValueNone (ValueSome ["payload"])) [value] "Versionless events dispatch the active callback"
        Expect.equal calls.Count (count + 1) "Invoke exactly once"
        Expect.equal calls.[count] (value, Guid.Empty, key.ElementId, ["payload"]) "Preserve invocation arguments"

    [<Tests>]
    let tests =
        testList "Updater Tests" [
            test "Unknown versions preserve active and pending handlers" {
                let handlers = EventHandlers<string>()
                let key = ChannelId("source", eventName)
                let calls = ResizeArray<_>()
                let zero = recordedHandler calls 0uy "zero"
                let one = recordedHandler calls 1uy "one"
                let two = recordedHandler calls 2uy "two"
                handlers.Enqueue(key, zero)
                acknowledgeAndDispatch handlers key zero "zero" calls
                handlers.Enqueue(key, one)
                handlers.Enqueue(key, two)
                for version in [255uy; 3uy; 255uy] do
                    Expect.isTrue (handlers.TryGet(key, version)).IsNone "An absent version has no handler"
                    Expect.equal (receive handlers key.ElementId key.ChannelName (ValueSome version) (ValueSome [])) [] "A miss must not dispatch"
                    sameHandler zero (handlers.TryGet key)
                    Expect.equal calls.Count 1 "Repeated misses do not invoke callbacks"
                Expect.equal (receive handlers key.ElementId key.ChannelName (ValueSome 1uy) (ValueSome ["payload"])) ["one"] "A valid versioned event can still activate and dispatch"
                sameHandler one (handlers.TryGet key)
                Expect.equal calls.Count 2 "The versioned event invokes only its selected callback"
                Expect.equal calls.[1] ("one", Guid.Empty, key.ElementId, ["payload"]) "Versioned invocation arguments"
                acknowledgeAndDispatch handlers key two "two" calls
                Expect.equal (calls |> Seq.map (fun (value, _, _, _) -> value) |> Seq.toList) ["zero"; "one"; "two"] "No callback is lost or repeated"
            }

            test "Misses before activation and with no pending entries are harmless" {
                let handlers = EventHandlers<string>()
                let key = ChannelId("source", eventName)
                let calls = ResizeArray<_>()
                Expect.isTrue (handlers.TryGet(key, 255uy)).IsNone "Missing key"
                Expect.isFalse (handlers.Remove key) "A miss must not create a key"
                let first = recordedHandler calls 7uy "first"
                let later = recordedHandler calls 42uy "later"
                handlers.Enqueue(key, first)
                handlers.Enqueue(key, later)
                for version in [255uy; 0uy; 255uy] do
                    Expect.isTrue (handlers.TryGet(key, version)).IsNone "Missing initial version"
                    Expect.isTrue (handlers.TryGet(key, ValueNone)).IsNone "Misses do not activate a pending handler"
                Expect.equal calls.Count 0 "Lookups never invoke callbacks"
                acknowledgeAndDispatch handlers key first "first" calls
                acknowledgeAndDispatch handlers key later "later" calls
                for version in [255uy; 7uy; 0uy] do
                    Expect.isTrue (handlers.TryGet(key, version)).IsNone "Missing version with an empty queue"
                    sameHandler later (handlers.TryGet key)
                Expect.equal (receive handlers key.ElementId key.ChannelName ValueNone (ValueSome [])) ["later"] "Active handler survives empty-queue misses"
                Expect.equal calls.Count 3 "Only explicit dispatch invokes callbacks"
            }

            test "Retired and skipped versions leave later replacements available" {
                let handlers = EventHandlers<string>()
                let key = ChannelId("source", eventName)
                let calls = ResizeArray<_>()
                let active = recordedHandler calls 0uy "active"
                let superseded = recordedHandler calls 3uy "superseded"
                let selected = recordedHandler calls 7uy "selected"
                let later = recordedHandler calls 9uy "later"
                handlers.Enqueue(key, active)
                acknowledgeAndDispatch handlers key active "active" calls
                for handler in [superseded; selected; later] do handlers.Enqueue(key, handler)
                acknowledgeAndDispatch handlers key selected "selected" calls
                for version in [0uy; 3uy; 8uy; 3uy] do
                    Expect.isTrue (handlers.TryGet(key, version)).IsNone "Retired or never-enqueued version"
                    sameHandler selected (handlers.TryGet key)
                acknowledgeAndDispatch handlers key later "later" calls
                Expect.equal (calls |> Seq.map (fun (value, _, _, _) -> value) |> Seq.toList) ["active"; "selected"; "later"] "Skipped callbacks must not run"
            }

            test "Byte wraparound uses FIFO membership rather than numeric order" {
                let handlers = EventHandlers<string>()
                let key = ChannelId("source", eventName)
                let calls = ResizeArray<_>()
                let active = recordedHandler calls 254uy "before-wrap"
                let skipped = recordedHandler calls 255uy "skipped"
                let wrapped = recordedHandler calls 0uy "wrapped"
                let later = recordedHandler calls 1uy "later"
                handlers.Enqueue(key, active)
                acknowledgeAndDispatch handlers key active "before-wrap" calls
                for handler in [skipped; wrapped; later] do handlers.Enqueue(key, handler)
                Expect.isTrue (handlers.TryGet(key, 253uy)).IsNone "Missing pre-wrap version"
                sameHandler active (handlers.TryGet key)
                acknowledgeAndDispatch handlers key wrapped "wrapped" calls
                Expect.isTrue (handlers.TryGet(key, 255uy)).IsNone "Retired version after wrap"
                sameHandler wrapped (handlers.TryGet key)
                acknowledgeAndDispatch handlers key later "later" calls
                Expect.equal calls.Count 3 "Only selected handlers dispatch across wrap"
            }

            test "Current versions take precedence and acknowledgements prune only through their match" {
                let handlers = EventHandlers<string>()
                let key = ChannelId("source", eventName)
                let calls = ResizeArray<_>()
                let current = recordedHandler calls 0uy "current"
                let first = recordedHandler calls 9uy "first"
                let duplicate = recordedHandler calls 0uy "duplicate"
                let skipped = recordedHandler calls 2uy "skipped"
                let selected = recordedHandler calls 4uy "selected"
                let later = recordedHandler calls 5uy "later"
                handlers.Enqueue(key, current)
                acknowledgeAndDispatch handlers key current "current" calls
                for handler in [first; duplicate; skipped; selected; later] do handlers.Enqueue(key, handler)
                sameHandler current (handlers.TryGet(key, 0uy))
                sameHandler current (handlers.TryGet(key, ValueNone))
                Expect.equal calls.Count 1 "Current lookups do not invoke or advance the queue"
                acknowledgeAndDispatch handlers key first "first" calls
                acknowledgeAndDispatch handlers key duplicate "duplicate" calls
                acknowledgeAndDispatch handlers key selected "selected" calls
                acknowledgeAndDispatch handlers key later "later" calls
                Expect.equal (calls |> Seq.map (fun (value, _, _, _) -> value) |> Seq.toList) ["current"; "first"; "duplicate"; "selected"; "later"] "Retain successful FIFO and current-version semantics"
            }

            test "Version misses and Remove do not affect unrelated keys" {
                let handlers = EventHandlers<string>()
                let calls = ResizeArray<_>()
                let keys = [ChannelId("source", eventName); ChannelId("source", "onclick"); ChannelId("other", eventName)]
                let registered = keys |> List.mapi (fun i key ->
                    let active = recordedHandler calls 0uy ($"active-{i}")
                    let pending = recordedHandler calls 1uy ($"pending-{i}")
                    handlers.Enqueue(key, active)
                    acknowledgeAndDispatch handlers key active ($"active-{i}") calls
                    handlers.Enqueue(key, pending)
                    key, active, pending)
                let key, _, _ = registered.Head
                Expect.isTrue (handlers.TryGet(key, 255uy)).IsNone "Missing version for one key"
                for key, active, _ in registered do sameHandler active (handlers.TryGet key)
                Expect.isTrue (handlers.Remove key) "Remove active and pending handlers together"
                Expect.isFalse (handlers.Remove key) "Repeated Remove reports absence"
                Expect.isTrue (handlers.TryGet key).IsNone "Removed current handler"
                Expect.isTrue (handlers.TryGet(key, 1uy)).IsNone "Removed pending handler"
                Expect.equal (receive handlers key.ElementId key.ChannelName ValueNone (ValueSome [])) [] "Removed keys do not dispatch"
                for key, _, pending in registered.Tail do acknowledgeAndDispatch handlers key pending (if key.ElementId = "other" then "pending-2" else "pending-1") calls
                let fresh = recordedHandler calls 0uy "fresh"
                handlers.Enqueue(key, fresh)
                acknowledgeAndDispatch handlers key fresh "fresh" calls
                Expect.equal calls.Count 6 "Unrelated handlers and a re-created key remain usable"
            }
            test "Initial body acknowledgement activates its versionless boot event" {
                checkInitial "body"
            }

            test "Initial ordinary-element acknowledgement activates its versionless boot event" {
                checkInitial "div"
            }

            test "Adaptive handler replacements remain pending until acknowledged" {
                for tag in ["body"; "div"] do
                    let calls = ResizeArray<string>()
                    let event value =
                        AttributeValue.Event (Event.ofTrigger (fun () -> calls.Add value; value))
                    let attributes = cmap [eventName, event "initial"]
                    withUpdater tag (AttributeMap.ofAMap attributes) AList.empty (fun updater handlers state code ->
                        // Prime the initial handler by its registered key to isolate subsequent updates.
                        receive handlers updater.Id eventName (ValueSome 0uy) ValueNone |> ignore
                        let bootSender, bootName = bootEvent code
                        Expect.equal (receive handlers bootSender bootName ValueNone (ValueSome [])) ["initial"] "Initial handler must be active"

                        for version, value in [1uy, "second"; 2uy, "third"] do
                            transact (fun () -> attributes.[eventName] <- event value)
                            let code = updater.Update(AdaptiveToken.Top, state, ValueNone) |> JSExpr.toString
                            let sender, name, emittedVersion = activation code
                            Expect.equal (sender, name, emittedVersion) (updater.Id, eventName, version) "Adaptive update must preserve sender and increment version"
                            Expect.equal (handlers.TryGet(ChannelId(sender, name)) |> ValueOption.map (fun h -> h.version)) (ValueSome (version - 1uy)) "Replacement must not become active before acknowledgement"
                            let count = calls.Count
                            Expect.equal (receive handlers sender name (ValueSome emittedVersion) ValueNone) [] "Acknowledgement must not emit messages"
                            Expect.equal calls.Count count "Acknowledgement must not invoke either reaction"
                            Expect.equal (receive handlers sender name ValueNone (ValueSome [])) [value] "Versionless event must use the replacement"
                            Expect.equal calls.Count (count + 1) "Each event must invoke its reaction exactly once"
                            Expect.equal (updater.Update(AdaptiveToken.Top, state, ValueNone) |> JSExpr.toString) "" "Unchanged updater must emit no activation"
                        Expect.equal (calls |> Seq.toList) ["initial"; "second"; "third"] "Only the active reactions may run"
                    )
            }

            test "Replacing a child by empty removes it after shutdown and before empty boot" {
                for activate in [false; true] do
                    let children = clist [child "old"]
                    let index = children.TryGetIndex 0 |> Option.get
                    withUpdater "div" AttributeMap.empty children (fun updater handlers state initial ->
                        let id, name, version = activation initial
                        if activate then
                            receive handlers id name (ValueSome version) ValueNone |> ignore
                            Expect.equal (receive handlers id name ValueNone (ValueSome [])) ["old"] "Initial child handler must be active"
                        let empty = DomNode.Empty<string>() |> onBoot "emptyReady();"
                        transact (fun () -> children.[index] <- empty)
                        let expression = updater.Update(AdaptiveToken.Top, state, ValueNone)
                        checkUnregistered handlers id
                        checkRemoval id expression
                        let ops = operations expression
                        let removeIndex = ops |> List.findIndex ((=) (JSExpr.Remove(JSExpr.Var id)))
                        let bootIndex = ops |> List.findIndex ((=) (JSExpr.Raw "emptyReady();"))
                        Expect.isTrue (removeIndex < bootIndex) "Empty replacement must update only after the retired element is removed"
                        let code = JSExpr.toString expression
                        Expect.isTrue (code.IndexOf($"{id}.remove();", StringComparison.Ordinal) < code.IndexOf("emptyReady();", StringComparison.Ordinal)) "Emitted removal must precede the replacement boot"
                        Expect.equal (children.TryGetIndex 0) (Some index) "Replacement must retain the clist index"
                        Expect.equal (updater.Update(AdaptiveToken.Top, state, ValueNone) |> JSExpr.toString) "" "Unchanged empty replacement must not repeat shutdown or removal"
                    )
            }

            test "Repeated hide and show retire each child without accumulating elements" {
                let children = clist [child "visible"]
                let index = children.TryGetIndex 0 |> Option.get
                withUpdater "div" AttributeMap.empty children (fun updater handlers state initial ->
                    let firstId, _, _ = activation initial
                    let created = ResizeArray<string>([firstId])
                    let removed = ResizeArray<string>()
                    let mutable currentId = firstId
                    for cycle in 1 .. 10 do
                        receive handlers currentId eventName (ValueSome 0uy) ValueNone |> ignore
                        Expect.equal (receive handlers currentId eventName ValueNone (ValueSome [])) ["visible"] "Shown child must dispatch its own event"
                        transact (fun () -> children.[index] <- DomNode.Empty<string>())
                        let hidden = updater.Update(AdaptiveToken.Top, state, ValueNone)
                        checkUnregistered handlers currentId
                        checkRemoval currentId hidden
                        for op in operations hidden do
                            match op with
                            | JSExpr.Remove(JSExpr.Var id) -> removed.Add id
                            | _ -> ()
                        Expect.equal (created |> Seq.except removed |> Seq.toList) [] "Hidden state must retain no previously created child"
                        Expect.equal (updater.Update(AdaptiveToken.Top, state, ValueNone) |> JSExpr.toString) "" "Hidden state must be stable"

                        transact (fun () -> children.[index] <- child "visible")
                        let shown = updater.Update(AdaptiveToken.Top, state, ValueNone)
                        let id, name, version = activation (JSExpr.toString shown)
                        Expect.isFalse (created.Contains id) "Showing must create a fresh element ID"
                        Expect.equal (name, version) (eventName, 0uy) "Fresh child must register its initial event handler"
                        let insertions = operations shown |> List.filter (function JSExpr.AppendChild _ | JSExpr.InsertBefore _ | JSExpr.InsertAfter _ | JSExpr.Replace _ | JSExpr.Remove _ -> true | _ -> false)
                        Expect.equal insertions [JSExpr.AppendChild(JSExpr.Var updater.Id, JSExpr.Var id)] "Showing must insert only the new child"
                        created.Add id
                        currentId <- id
                        Expect.equal (created |> Seq.except removed |> Seq.toList) [id] "Shown state must contain exactly the current child"
                        Expect.equal created.Count (cycle + 1) "Each show must create one child"
                        Expect.equal removed.Count cycle "Each hide must remove one child"
                        Expect.equal (children.TryGetIndex 0) (Some index) "All transitions must use the same clist index"
                        Expect.equal (updater.Update(AdaptiveToken.Top, state, ValueNone) |> JSExpr.toString) "" "Shown state must be stable"
                    for id in removed do checkUnregistered handlers id
                )
            }

            test "Non-empty replacement, initially empty creation and list removal preserve lifecycle" {
                for change in ["replace"; "create"; "remove"] do
                    let children = clist [if change = "create" then DomNode.Empty<string>() else child "old"]
                    let index = children.TryGetIndex 0 |> Option.get
                    withUpdater "div" AttributeMap.empty children (fun updater handlers state initial ->
                        let oldId =
                            if change = "create" then None
                            else
                                let id, name, version = activation initial
                                receive handlers id name (ValueSome version) ValueNone |> ignore
                                Some id
                        transact (fun () ->
                            if change = "remove" then children.Remove index |> ignore
                            else children.[index] <- child "new"
                        )
                        let expression = updater.Update(AdaptiveToken.Top, state, ValueNone)
                        let ops = operations expression
                        match oldId with
                        | Some id -> checkUnregistered handlers id
                        | _ -> ()
                        if change = "remove" then
                            checkRemoval (Option.get oldId) expression
                            Expect.equal children.Count 0 "Ordinary removal must delete the list entry"
                        else
                            let code = JSExpr.toString expression
                            let id, name, version = activation code
                            let insertion =
                                match oldId with
                                | Some old -> JSExpr.Replace(JSExpr.Var old, JSExpr.Var id)
                                | None -> JSExpr.AppendChild(JSExpr.Var updater.Id, JSExpr.Var id)
                            let mutations = ops |> List.filter (function JSExpr.Remove _ | JSExpr.Replace _ | JSExpr.AppendChild _ -> true | _ -> false)
                            Expect.equal mutations [insertion] "Other paths must retain their existing replacement or insertion operation"
                            let insertIndex = ops |> List.findIndex ((=) insertion)
                            let bootIndex = ops |> List.findIndex ((=) (JSExpr.Raw $"created(\"{id}\");"))
                            Expect.isTrue (insertIndex < bootIndex) "New child must be attached before boot"
                            match oldId with
                            | Some old ->
                                let shutdownIndex = ops |> List.findIndex ((=) (JSExpr.Raw $"retire(\"{old}\");"))
                                Expect.isTrue (shutdownIndex < insertIndex) "Non-empty replacement must shut down before replaceChild"
                                Expect.stringContains code ".parentElement.replaceChild(" "Emitted non-empty replacement must keep replaceChild"
                            | None ->
                                Expect.isFalse (code.Contains("retire(")) "Initially empty creation must not shut down a nonexistent element"
                            receive handlers id name (ValueSome version) ValueNone |> ignore
                            Expect.equal (receive handlers id name ValueNone (ValueSome [])) ["new"] "Inserted child must dispatch through its registered handler"
                            Expect.equal (children.TryGetIndex 0) (Some index) "Replacement and creation must retain the clist index"
                        Expect.equal (updater.Update(AdaptiveToken.Top, state, ValueNone) |> JSExpr.toString) "" "Unchanged control must emit no lifecycle operations"
                    )
            }

            test "Activation encodes the registered ID rather than the DOM target" {
                for id in [""; "registered-id"; "quote\"\\\n\u0000\u2028';<>&"; "Grüße λ 漢字 😀"] do
                    let attributes = AttributeMap.single eventName (AttributeValue.Event (Event.ofTrigger (fun () -> "ready")))
                    let updater = AttributeUpdater(attributes)
                    let handlers, state = createState ()
                    try
                        let code = updater.Update(AdaptiveToken.Top, state, id, JSExpr.Var "target") |> JSExpr.toString
                        let sender, name, version = activation code
                        Expect.equal (sender, name, version) (id, eventName, 0uy) "Activation literal must preserve the registered ID"
                        Expect.equal (receive handlers sender name (ValueSome version) ValueNone) [] "Encoded acknowledgement must activate without dispatch"
                        Expect.equal (receive handlers id eventName ValueNone (ValueSome [])) ["ready"] "Encoded ID must resolve the registered handler"
                    finally
                        updater.Destroy state
            }
        ]
