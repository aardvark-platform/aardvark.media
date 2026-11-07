namespace Aardvark.UI.Tests

open System
open System.Runtime.Serialization
open System.Text.Json
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``Channel Tests`` =

    [<DataContract>]
    type private Payload(contents: string, failures: int, ?beforeRead: unit -> unit) =
        let mutable contents = contents
        let mutable remaining = failures
        let mutable attempts = 0
        let failure = InvalidOperationException("Injected serialization failure")
        let beforeRead = defaultArg beforeRead ignore

        member _.Attempts = attempts
        member _.Failure = failure
        member _.FailNext count = remaining <- count

        [<DataMember(Name = "contents")>]
        member _.Contents
            with get() =
                attempts <- attempts + 1
                beforeRead()
                if remaining > 0 then
                    remaining <- remaining - 1
                    raise failure
                contents
            and set value = contents <- value

    let private receive (reader: ChannelReader) expected =
        let messages = reader.GetMessages AdaptiveToken.Top
        Expect.equal messages.Length 1 "A changed value produces exactly one complete message"
        use json = JsonDocument.Parse messages.Head
        Expect.equal (json.RootElement.GetProperty("contents").GetString()) expected "Independent decoding of the serialized contents"

    let private unchanged (reader: ChannelReader) =
        Expect.isEmpty (reader.GetMessages AdaptiveToken.Top) "No duplicate or redundant message"

    let private failsWith (reader: ChannelReader) (failure: exn) =
        let error =
            try
                reader.GetMessages AdaptiveToken.Top |> ignore
                failtest "Expected the serialization exception to propagate"
            with error -> error
        Expect.isTrue (Object.ReferenceEquals(error.GetBaseException(), failure)) "Preserve the serializer's underlying exception"

    let private fails reader (payload: Payload) attempt =
        failsWith reader payload.Failure
        Expect.equal payload.Attempts attempt "The failed value really reached serialization"

    let private receiveSet (reader: ChannelReader) (receiver: Map<string, int> ref) operations values =
        // Decode the wire directly, not with FsPickler, SetOperation or HashSetDelta.
        let decoded =
            reader.GetMessages AdaptiveToken.Top |> List.map (fun message ->
                use json = JsonDocument.Parse message
                let count = json.RootElement.GetProperty("cnt").GetInt32()
                let value = json.RootElement.GetProperty("value")
                let text = if value.ValueKind = JsonValueKind.Object then value.GetProperty("contents").GetString() else value.GetString()
                count, text)
        Expect.equal (List.sort decoded) (List.sort operations) "Exactly the undelivered operations, irrespective of set iteration order"
        for count, value in decoded do
            Expect.isTrue (count = 1 || count = -1) "Set additions and removals retain their signed wire counts"
            let next = (Map.tryFind value receiver.Value |> Option.defaultValue 0) + count
            Expect.isTrue (next = 0 || next = 1) "Receiver never loses a removal or receives a duplicate addition"
            receiver.Value <- if next = 0 then Map.remove value receiver.Value else Map.add value next receiver.Value
        Expect.equal receiver.Value (values |> List.map (fun value -> value, 1) |> Map.ofList) "Independent counted receiver matches the expected set"

    [<Tests>]
    let tests =
        testList "DOM.Channel" [
            test "Failed initial values remain available for reevaluation" {
                for failures in [1; 2; 3] do
                    let payload = Payload("initial \"quoted\"\nü東京", failures)
                    let source = cval payload
                    use reader = (source :> aval<_>).Channel.GetReader()
                    for attempt in 1 .. failures do fails reader payload attempt
                    receive reader "initial \"quoted\"\nü東京"
                    Expect.equal payload.Attempts (failures + 1) "Retry serializes the same pending value"
                    unchanged reader
                    Expect.equal payload.Attempts (failures + 1) "Successful retry is then suppressed"
                    let next = Payload("later", 0)
                    transact (fun () -> source.Value <- next)
                    receive reader "later"
                    Expect.equal next.Attempts 1 "Later adaptive values still serialize once"
                    unchanged reader
            }

            test "Failed later values retry without losing subsequent updates" {
                for failures in [1; 2; 3] do
                    let initial = Payload("first", 0)
                    let source = cval initial
                    use reader = (source :> aval<_>).Channel.GetReader()
                    receive reader "first"
                    let pending = Payload("second", failures)
                    transact (fun () -> source.Value <- pending)
                    for attempt in 1 .. failures do fails reader pending attempt
                    receive reader "second"
                    Expect.equal pending.Attempts (failures + 1) "Every explicit reevaluation retries until success"
                    unchanged reader
                    let next = Payload("third", 0)
                    transact (fun () -> source.Value <- next)
                    receive reader "third"
                    unchanged reader
                    Expect.equal initial.Attempts 1 "Earlier successful value is not serialized again"
                    Expect.equal next.Attempts 1 "Later updates are not dropped or duplicated"
            }

            test "Reverting after failure does not retransmit the last successful value" {
                for contents in ["remembered"; ""; null] do
                    for failures in [1; 2] do
                        let initial = Payload(contents, 0)
                        let source = cval initial
                        use reader = (source :> aval<_>).Channel.GetReader()
                        receive reader contents
                        let pending = Payload("pending", failures)
                        transact (fun () -> source.Value <- pending)
                        for attempt in 1 .. failures do fails reader pending attempt
                        transact (fun () -> source.Value <- initial)
                        unchanged reader
                        Expect.equal initial.Attempts 1 "Reversion uses the last successful serialization, not the failed value"
                        Expect.equal pending.Attempts failures "Reversion does not retry an obsolete value"
                        transact (fun () -> source.Value <- pending)
                        receive reader "pending"
                        Expect.equal pending.Attempts (failures + 1) "A later return to the failed value can succeed"
                        unchanged reader
            }

            test "Ordinary null empty and equal values retain message suppression" {
                let cases = [|
                    null, true; null, false
                    "", true; "", false
                    "ordinary", true; "ordinary", false
                    "quotes \"\\\nü東京", true
                    "", true; null, true
                |]
                let revision = cval 0
                let mutable evaluations = 0
                let value = revision |> AVal.map (fun index ->
                    evaluations <- evaluations + 1
                    fst cases.[index])
                use reader = value.Channel.GetReader()
                for index in 0 .. cases.Length - 1 do
                    transact (fun () -> revision.Value <- index)
                    let expected, send = cases.[index]
                    let messages = reader.GetMessages AdaptiveToken.Top
                    if send then
                        Expect.equal messages.Length 1 "Exactly one ordinary-value message"
                        Expect.equal (JsonSerializer.Deserialize<string>(messages.Head)) expected "Independent decoding of primitive values"
                    else
                        Expect.isEmpty messages "Equal values stay suppressed even when reevaluated"
                    Expect.equal evaluations (index + 1) "The mapped value was actually reevaluated"
                    unchanged reader
            }

            test "Readers maintain independent successful and failed states" {
                let initial = Payload("initial", 0)
                let source = cval initial
                let channel = (source :> aval<_>).Channel
                use first = channel.GetReader()
                use second = channel.GetReader()
                receive first "initial"
                receive second "initial"
                Expect.equal initial.Attempts 2 "Each reader independently initializes"
                let pending = Payload("pending", 1)
                transact (fun () -> source.Value <- pending)
                fails first pending 1
                receive second "pending"
                unchanged second
                receive first "pending"
                Expect.equal pending.Attempts 3 "One reader's success does not consume the other's retry"
                unchanged first
                let next = Payload("next", 0)
                transact (fun () -> source.Value <- next)
                receive second "next"
                receive first "next"
                unchanged first
                unchanged second
                Expect.equal next.Attempts 2 "Both readers continue receiving later values independently"
            }

            testList "Set" [
                test "Failed snapshots retry every undelivered addition" {
                    for failures in [1; 2; 3] do
                        let pending = Payload("initial \"quoted\"\nü東京", failures)
                        let good = Payload("other", 0)
                        let source = cset [pending; good]
                        use reader = (source :> aset<_>).Channel.GetReader()
                        let receiver = ref Map.empty
                        for attempt in 1 .. failures do
                            fails reader pending attempt
                            Expect.isEmpty receiver.Value "A failed snapshot delivers no partial list"
                        receiveSet reader receiver [1, "initial \"quoted\"\nü東京"; 1, "other"] ["initial \"quoted\"\nü東京"; "other"]
                        Expect.equal pending.Attempts (failures + 1) "Only explicit reevaluation retries serialization"
                        unchanged reader
                        Expect.equal pending.Attempts (failures + 1) "Successful retry is consumed once"
                }

                test "Failed additions removals and mixed batches remain recoverable" {
                    for kind in ["addition"; "removal"; "mixed"] do
                        for failures in [1; 2; 3] do
                            let kept, removed, added = Payload("kept", 0), Payload("removed", 0), Payload("added", 0)
                            let source = cset [kept; removed]
                            use reader = (source :> aset<_>).Channel.GetReader()
                            let receiver = ref Map.empty
                            receiveSet reader receiver [1, "kept"; 1, "removed"] ["kept"; "removed"]
                            let pending = if kind = "addition" then added else removed
                            let priorAttempts = pending.Attempts
                            pending.FailNext failures
                            transact (fun () ->
                                if kind <> "addition" then source.Remove removed |> ignore
                                if kind <> "removal" then source.Add added |> ignore)
                            for attempt in 1 .. failures do fails reader pending (priorAttempts + attempt)
                            let operations = [
                                if kind <> "addition" then yield -1, "removed"
                                if kind <> "removal" then yield 1, "added"
                            ]
                            let values = [
                                yield "kept"
                                if kind = "addition" then yield "removed"
                                if kind <> "removal" then yield "added"
                            ]
                            receiveSet reader receiver operations values
                            unchanged reader
                            let next = Payload("next", 0)
                            transact (fun () -> source.Add next |> ignore)
                            receiveSet reader receiver [1, "next"] ("next" :: values)
                            unchanged reader
                }

                test "Changes before retry coalesce with failed additions and removals" {
                    for initialFailure, removal, cancel in [true, false, true; false, false, true; false, true, true; false, false, false; false, true, false] do
                        let kept, pending, later = Payload("kept", 0), Payload("pending", 0), Payload("later", 0)
                        let source = cset [kept]
                        if initialFailure || removal then transact (fun () -> source.Add pending |> ignore)
                        use reader = (source :> aset<_>).Channel.GetReader()
                        let receiver = ref Map.empty
                        if not initialFailure then
                            receiveSet reader receiver ([1, "kept"] @ [if removal then 1, "pending"]) (["kept"] @ [if removal then "pending"])
                        pending.FailNext 1
                        let priorAttempts = pending.Attempts
                        if not initialFailure then
                            transact (fun () ->
                                if removal then source.Remove pending |> ignore else source.Add pending |> ignore)
                        fails reader pending (priorAttempts + 1)
                        transact (fun () ->
                            if cancel then
                                if removal then source.Add pending |> ignore else source.Remove pending |> ignore
                            else source.Add later |> ignore)
                        let operations = [
                            if initialFailure then yield 1, "kept"
                            if not cancel then
                                yield (if removal then -1 else 1), "pending"
                                yield 1, "later"
                        ]
                        let values = [
                            yield "kept"
                            if removal = cancel then yield "pending"
                            if not cancel then yield "later"
                        ]
                        receiveSet reader receiver operations values
                        if cancel then Expect.equal pending.Attempts (priorAttempts + 1) "Canceled operations are not serialized again"
                        unchanged reader
                }

                test "Failure partway through a batch returns no partial delivery" {
                    for failAt in [2; 3] do
                        let failure = InvalidOperationException("Injected batch serialization failure")
                        let mutable attempts = 0
                        let beforeRead () =
                            attempts <- attempts + 1
                            if attempts = failAt then raise failure
                        let payloads = [for text in ["one"; "two"; "three"] -> Payload(text, 0, beforeRead = beforeRead)]
                        let source = cset []
                        use reader = (source :> aset<_>).Channel.GetReader()
                        let receiver = ref Map.empty
                        receiveSet reader receiver [] []
                        transact (fun () -> for payload in payloads do source.Add payload |> ignore)
                        failsWith reader failure
                        Expect.equal attempts failAt "Some complete values serialized before the failing value"
                        Expect.isEmpty receiver.Value "No partially serialized list was returned"
                        receiveSet reader receiver [1, "one"; 1, "two"; 1, "three"] ["one"; "two"; "three"]
                        Expect.equal attempts (failAt + 3) "Retry serializes the whole undelivered batch"
                        unchanged reader
                }

                test "Readers retain independent pending set changes" {
                    let initial, pending, next = Payload("initial", 0), Payload("pending", 1), Payload("next", 0)
                    let source = cset [initial]
                    let channel = (source :> aset<_>).Channel
                    use first = channel.GetReader()
                    use second = channel.GetReader()
                    let a, b = ref Map.empty, ref Map.empty
                    receiveSet first a [1, "initial"] ["initial"]
                    receiveSet second b [1, "initial"] ["initial"]
                    transact (fun () -> source.Add pending |> ignore)
                    fails first pending 1
                    receiveSet second b [1, "pending"] ["initial"; "pending"]
                    receiveSet first a [1, "pending"] ["initial"; "pending"]
                    unchanged first
                    unchanged second
                    pending.FailNext 1
                    transact (fun () -> source.Remove pending |> ignore)
                    fails second pending 4
                    receiveSet first a [-1, "pending"] ["initial"]
                    transact (fun () -> source.Add next |> ignore)
                    receiveSet second b [-1, "pending"; 1, "next"] ["initial"; "next"]
                    receiveSet first a [1, "next"] ["initial"; "next"]
                    unchanged first
                    unchanged second
                    Expect.equal next.Attempts 2 "Each reader continues independently after recovery"
                }

                test "Empty successful and unchanged sets retain their protocol" {
                    let source = cset<string> []
                    use reader = (source :> aset<_>).Channel.GetReader()
                    let receiver = ref Map.empty
                    receiveSet reader receiver [] []
                    unchanged reader
                    for value in [""; null; "ordinary"; "quotes \"\\\nü東京"] do
                        transact (fun () -> source.Add value |> ignore)
                        receiveSet reader receiver [1, value] [value]
                        transact (fun () -> source.Add value |> ignore)
                        receiveSet reader receiver [] [value]
                        unchanged reader
                        transact (fun () -> source.Remove value |> ignore)
                        receiveSet reader receiver [-1, value] []
                        unchanged reader
                    let canceled = Payload("never delivered", 1)
                    let empty = cset<Payload> []
                    use emptyReader = (empty :> aset<_>).Channel.GetReader()
                    receiveSet emptyReader (ref Map.empty) [] []
                    transact (fun () -> empty.Add canceled |> ignore; empty.Remove canceled |> ignore)
                    unchanged emptyReader
                    Expect.equal canceled.Attempts 0 "An ordinary canceled change never reaches serialization"
                }
            ]
        ]
