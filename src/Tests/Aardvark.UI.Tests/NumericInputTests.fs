namespace Aardvark.UI.Tests

open System
open System.Globalization
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive
open Expecto

module ``Numeric Input Tests`` =

    type private Message<'a> = Changed of 'a

    let private withCulture custom action =
        let previous = CultureInfo.CurrentCulture
        let culture = CultureInfo.InvariantCulture.Clone() :?> CultureInfo
        if custom then
            culture.NumberFormat.NegativeSign <- "\u2212"
            culture.NumberFormat.NumberDecimalSeparator <- ","
        try
            CultureInfo.CurrentCulture <- culture
            action()
        finally
            CultureInfo.CurrentCulture <- previous

    let private helpers<'a> () =
        let config = NumericConfigDefaults<'a>.Get()
        let sliderConfig = { min = config.min; max = config.max; step = config.smallStep }
        let value = AVal.constant config.smallStep
        let attributes : Attribute<Message<'a>> list = []
        [
            "Incremental.numeric'", (fun update ->
                SimplePrimitives.Incremental.numeric' config AttributeMap.empty
                    (AVal.constant None) (AVal.constant None) value (update >> List.singleton))
            "Incremental.numeric", (fun update -> SimplePrimitives.Incremental.numeric config AttributeMap.empty value update)
            "Incremental.slider", (fun update -> SimplePrimitives.Incremental.slider sliderConfig AttributeMap.empty value update)
            "numeric", (fun update -> SimplePrimitives.numeric config attributes value update)
            "slider", (fun update -> SimplePrimitives.slider sliderConfig attributes value update)
        ] : (string * (('a -> Message<'a>) -> DomNode<Message<'a>>)) list

    let private inputEvent (node: DomNode<'msg>) =
        let root = node :?> InnerNode<'msg>
        match root.Attributes.Content |> AVal.force |> HashMap.tryFind "data-event" with
        | Some (AttributeValue.Event event) -> event
        | _ -> failtest "Expected the public helper's data-event handler"

    let private check name build args expected =
        let calls = ResizeArray<_>()
        let event = build (fun value -> calls.Add value; Changed value) |> inputEvent
        let messages = event.serverSide Guid.Empty "number" args
        Expect.equal calls.Count 0 $"{name}: callback is deferred until enumeration"
        Expect.equal (Seq.toList messages) (expected |> Option.toList |> List.map Changed) $"{name}: unexpected messages for {args}"
        Expect.equal (Seq.toList calls) (Option.toList expected) $"{name}: invoke the callback exactly once for accepted input"

    let private negativeIntegers = [
        "-1", -1; "-12", -12; " -2 ", -2; "-1.4", -1
        "-1.5", -2; "-2.5", -2; "-3.5", -4
        "-1e2", -100; "-2.5e0", -2
        "-0", 0; "-0.0", 0; "-0.1", 0; "-0.5", 0; "-0e3", 0
    ]

    let private checkNegatives custom =
        withCulture custom (fun () ->
            for name, build in helpers<int>() do
                for input, expected in negativeIntegers do check name build [input] (Some expected))

    let private checkNonnegative () =
        for custom in [false; true] do
            withCulture custom (fun () ->
                for name, build in helpers<int>() do
                    for input, expected in ["0", 0; "1", 1; "+2", 2; "1.5", 2; "2.5", 2; "3.5", 4; "1e2", 100] do
                        check name build [input] (Some expected)
                for name, build in helpers<uint32>() do
                    for input, expected in ["0", 0u; "1", 1u; "1.5", 2u; "2.5", 2u; "3.5", 4u; "1e2", 100u] do
                        check name build [input] (Some expected))

    let private checkFloating () =
        for custom in [false; true] do
            withCulture custom (fun () ->
                for name, build in helpers<float>() do
                    for input, expected in ["-1.25", -1.25; "2.5", 2.5; "-1.25e2", -125.0; "-0", 0.0] do
                        check name build [input] (Some expected)
                for name, build in helpers<float32>() do
                    for input, expected in ["-1.25", -1.25f; "2.5", 2.5f; "-1.25e2", -125.0f; "0", 0.0f] do
                        check name build [input] (Some expected)
                for name, build in helpers<decimal>() do
                    for input, expected in ["-1.25", -1.25m; "2.5", 2.5m; "0", 0m] do
                        check name build [input] (Some expected))

    let private checkArguments () =
        for custom in [false; true] do
            withCulture custom (fun () ->
                for name, build in helpers<int>() do
                    for args in [[]; [null]; [""]; ["not-a-number"]; ["1,5"]; ["\u22121"]; ["--1"]; ["bad"; "2.5"]] do
                        check name build args None
                    check name build ["2.5"; "ignored"; "-3.5"] (Some 2))

    let private checkExceptions () =
        for custom in [false; true] do
            withCulture custom (fun () ->
                for name, build in helpers<int>() do
                    let failure = InvalidOperationException "callback failure"
                    let mutable calls = 0
                    let event = build (fun _ -> calls <- calls + 1; raise failure) |> inputEvent
                    let messages = event.serverSide Guid.Empty "number" ["2"]
                    Expect.equal calls 0 $"{name}: do not invoke a throwing callback early"
                    let caught =
                        try messages |> Seq.toList |> ignore; None
                        with error -> Some error
                    Expect.isTrue (caught |> Option.exists (fun error -> obj.ReferenceEquals(error, failure))) $"{name}: preserve the callback exception"
                    Expect.equal calls 1 $"{name}: do not retry a throwing callback")

    [<Tests>]
    let tests =
        testList "Numeric Input Tests" [
            test "Signed integer handlers ignore server number formatting" {
                for custom in [false; true] do checkNegatives custom
            }
            test "Nonnegative integer and unsigned rounding is unchanged" {
                checkNonnegative()
            }
            test "Floating branches remain invariant" {
                checkFloating()
            }
            test "Malformed missing and extra arguments retain behavior" {
                checkArguments()
            }
            test "Callbacks remain deferred and propagate exact exceptions" {
                checkExceptions()
            }
        ]
