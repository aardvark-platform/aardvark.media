namespace Aardvark.UI.Tests

open System
open System.Runtime.Serialization
open System.Text.Json
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``Channel Tests`` =

    [<DataContract>]
    type private Payload(contents: string, failures: int) =
        let mutable contents = contents
        let mutable remaining = failures
        let mutable attempts = 0
        let failure = InvalidOperationException("Injected serialization failure")

        member _.Attempts = attempts
        member _.Failure = failure

        [<DataMember(Name = "contents")>]
        member _.Contents
            with get() =
                attempts <- attempts + 1
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

    let private fails (reader: ChannelReader) (payload: Payload) attempt =
        let error =
            try
                reader.GetMessages AdaptiveToken.Top |> ignore
                failtest "Expected the serialization exception to propagate"
            with error -> error
        Expect.isTrue (Object.ReferenceEquals(error.GetBaseException(), payload.Failure)) "Preserve the serializer's underlying exception"
        Expect.equal payload.Attempts attempt "The failed value really reached serialization"

    [<Tests>]
    let tests =
        testList "Channel Tests" [
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
        ]
