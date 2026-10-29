namespace Aardvark.UI.Tests

open System
open System.Text
open System.Text.Json
open Aardvark.Base
open Aardvark.UI
open Expecto

module ``Json Converter Tests`` =
    let private options =
        let options = JsonSerializerOptions()
        options.Converters.Add(JsonV2iConverter())
        options.Converters.Add(JsonC4bConverter())
        options

    let private vector (json: string) = JsonSerializer.Deserialize<V2i>(json, options)
    let private color (json: string) = JsonSerializer.Deserialize<C4b>(json, options)
    let private message (json: string) =
        RenderServerMessage.fromJson (ArraySegment(Encoding.UTF8.GetBytes json))

    let private invalidShapes = ["0"; "-7"; "1.5"; "true"; "false"; "\"text\""; "null"]
    let private image field value = $"{{\"case\":\"RequestImage\",\"{field}\":{value}}}"
    let private request size background = RenderServerMessage.RequestImage { size = size; background = background }

    let private rejectsShapes decode valid expected =
        let accepted = ResizeArray<string>()
        for json in invalidShapes do
            try
                decode json |> ignore
                accepted.Add json
            with :? JsonException -> ()
            Expect.equal (decode valid) expected $"Successful decoding after rejecting {json}"
        let detail = String.concat ", " accepted
        Expect.isEmpty accepted $"Expected JsonException for all non-object values; accepted: {detail}"

    let private vectors = [
        "{}", V2i.Zero
        "{\"X\":-7}", V2i(-7, 0)
        "{\"y\":19}", V2i(0, 19)
        "{\"Y\":2147483647,\"X\":-2147483648}", V2i(Int32.MinValue, Int32.MaxValue)
        "{\"X\":1,\"x\":2,\"X\":3,\"y\":4,\"Y\":5}", V2i(3, 5)
        "{\"nested\":{\"X\":999,\"values\":[{\"Y\":999},[1,null]]},\"X\":7,\"ignored\":[1,{\"X\":\"bad\"}],\"Y\":-9}", V2i(7, -9)
    ]

    let private colors = [
        "{}", C4b.Zero
        "{\"R\":17}", C4b(17uy, 0uy, 0uy, 0uy)
        "{\"a\":255}", C4b(0uy, 0uy, 0uy, 255uy)
        "{\"A\":64,\"B\":127,\"G\":0,\"R\":255}", C4b(255uy, 0uy, 127uy, 64uy)
        "{\"R\":1,\"r\":2,\"R\":3,\"G\":4,\"g\":5,\"B\":6,\"b\":7,\"A\":8,\"a\":9}", C4b(3uy, 5uy, 7uy, 9uy)
        "{\"nested\":[{\"R\":200},{\"v\":{\"A\":250}}],\"g\":5,\"R\":4,\"B\":6,\"A\":7}", C4b(4uy, 5uy, 6uy, 7uy)
    ]

    let private messages = [
        "{\"case\":\"RequestImage\",\"size\":{\"X\":640,\"Y\":480},\"background\":{\"R\":1,\"G\":2,\"B\":3,\"A\":4}}", request (V2i(640, 480)) (C4b(1uy, 2uy, 3uy, 4uy))
        "{\"background\":{\"a\":4,\"b\":3,\"g\":2,\"r\":1},\"size\":{\"y\":480,\"x\":640},\"case\":\"RequestImage\"}", request (V2i(640, 480)) (C4b(1uy, 2uy, 3uy, 4uy))
        "{\"case\":\"RequestImage\"}", request V2i.Zero C4b.Zero
        "{\"case\":\"RequestImage\",\"background\":{\"g\":2},\"size\":{\"Y\":9}}", request (V2i(0, 9)) (C4b(0uy, 2uy, 0uy, 0uy))
        "{\"case\":\"Rendered\"}", RenderServerMessage.Rendered
        "{\"irrelevant\":{\"case\":\"Bogus\",\"size\":true},\"case\":\"Rendered\"}", RenderServerMessage.Rendered
        "{\"case\":\"Rendered\",\"case\":\"RequestImage\",\"size\":{\"X\":1},\"size\":{\"y\":2}}", request (V2i(0, 2)) C4b.Zero
    ]

    type private ThrowingFieldConverter(error: exn) =
        inherit JsonStructConverter<V2i>()
        override _.ReadField(_, _, _, _) = raise error

    [<Tests>]
    let tests =
        testList "Json Converter Tests" [
            test "Vector converter rejects non-object values" {
                rejectsShapes vector "{\"x\":7,\"Y\":9}" (V2i(7, 9))
            }
            test "Color converter rejects non-object values" {
                rejectsShapes color "{\"r\":1,\"G\":2,\"b\":3,\"A\":4}" (C4b(1uy, 2uy, 3uy, 4uy))
            }
            for field in ["size"; "background"] do
                testCase $"Render messages reject non-object {field}" (fun () ->
                    rejectsShapes (image field >> message) "{}" (request V2i.Zero C4b.Zero))

            test "Object defaults, aliases, duplicates and nested unknown fields remain supported" {
                for json, expected in vectors do Expect.equal (vector json) expected json
                for json, expected in colors do Expect.equal (color json) expected json
                Expect.equal (JsonSerializer.Serialize(V2i(3, -4), options)) "{\"X\":3,\"Y\":-4}" "Vector writer stays unchanged"
                Expect.equal (JsonSerializer.Serialize(C4b(1uy, 2uy, 3uy, 4uy), options)) "{\"R\":1,\"G\":2,\"B\":3,\"A\":4}" "Color writer stays unchanged"
            }

            test "Valid render messages preserve reordered fields and padded segments" {
                for json, expected in messages do
                    Expect.equal (message json) expected json
                    let data = Encoding.UTF8.GetBytes json
                    // Invalid bytes outside the segment must not reach the JSON reader.
                    let padded = Array.concat [ [|0xFFuy; 0uy; 0x7Buy|]; data; [|0xFFuy; 0x5Duy|] ]
                    Expect.equal (RenderServerMessage.fromJson (ArraySegment(padded, 3, data.Length))) expected "ArraySegment offset/count"
            }

            test "Malformed input is rejected without poisoning later decodes" {
                let malformed = [""; "{"; "{\"X\":"; "{\"X\":1"; "{\"X\":1,}"; "[]"; "[1,2]"; "{}{}"]
                for json in malformed do
                    Expect.throwsT<JsonException> (fun () -> vector json |> ignore) $"Vector: {json}"
                    Expect.throwsT<JsonException> (fun () -> color json |> ignore) $"Color: {json}"
                    Expect.equal (vector "{\"x\":7,\"Y\":9}") (V2i(7, 9)) "Vector after rejection"
                    Expect.equal (color "{\"r\":1,\"G\":2,\"b\":3,\"A\":4}") (C4b(1uy, 2uy, 3uy, 4uy)) "Color after rejection"
                for json in [""; "{"; "{\"case\":"; "{\"case\":\"RequestImage\""; "{\"case\":\"RequestImage\",\"size\":{\"X\":1}"; "{\"case\":\"Unknown\"}"; "{\"case\":\"Rendered\"}{}"] do
                    Expect.throwsT<JsonException> (fun () -> message json |> ignore) $"Message: {json}"
                    Expect.equal (message "{\"case\":\"Rendered\"}") RenderServerMessage.Rendered "Message after rejection"
            }

            test "Serializer-managed reference nulls and field exceptions propagate unchanged" {
                Expect.isNull (box (message "null")) "Reference-type null is managed by JsonSerializer, not the record reader"
                let error = InvalidOperationException("field-reader failure")
                let throwingOptions = JsonSerializerOptions()
                throwingOptions.Converters.Add(ThrowingFieldConverter(error))
                let actual =
                    try
                        JsonSerializer.Deserialize<V2i>("{\"X\":1}", throwingOptions) |> ignore
                        failtest "Expected the field-reader exception"
                    with ex -> ex
                Expect.isTrue (obj.ReferenceEquals(actual, error)) "Preserve original field-reader exception identity"
                Expect.equal (JsonSerializer.Deserialize<V2i>("{}", throwingOptions)) V2i.Zero "Empty object after field-reader failure"
            }
        ]
