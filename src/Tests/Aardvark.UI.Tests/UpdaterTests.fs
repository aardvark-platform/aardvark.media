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

    let private withUpdater tag attributes action =
        let node =
            Incremental.elem tag attributes AList.empty
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
        withUpdater tag attributes (fun updater handlers _ code ->
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

    [<Tests>]
    let tests =
        testList "Updater Tests" [
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
                    withUpdater tag (AttributeMap.ofAMap attributes) (fun updater handlers state code ->
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
