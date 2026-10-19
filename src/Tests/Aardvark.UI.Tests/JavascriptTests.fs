namespace Aardvark.UI.Tests

open System
open System.Text.Json
open Aardvark.UI
open Expecto

module ``Javascript Tests`` =

    let private lineBreaks =
        [
            "data-lf", "first\nsecond", @"first\nsecond"
            "data-cr", "first\rsecond", @"first\rsecond"
            "data-crlf", "first\r\nsecond", @"first\r\nsecond"
            "data-slash-lf", "first\\\nsecond", """first\\\nsecond"""
            "onclick", "  // comment\r\n  run(\"quoted\");\n  return false;\r\n", """  // comment\r\n  run(\"quoted\");\n  return false;\r\n"""
        ]

    let private encodedCharacters =
        [
            "data-controls", String(Array.init 32 char), """\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\b\t\n\u000b\f\r\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f"""
            "data-delimiters", "'\"\\<>&", """\u0027\"\\\u003c\u003e\u0026"""
            "data-separators", "\u0085\u2028\u2029", @"\u0085\u2028\u2029"
        ]

    let private literals =
        [
            "data-empty", "", ""
            "data-plain", "  keep spaces  ", "  keep spaces  "
            "data-quotes", "\"quoted\"", "\\\"quoted\\\""
            "data-backslashes", @"C:\tmp\file", @"C:\\tmp\\file"
            "data-escaped", @"\n\r\u2028", @"\\n\\r\\u2028"
            "data-unicode", "Grüße λ 漢字 😀", "Grüße λ 漢字 😀"
        ]

    let private generate name value =
        JSExpr.Sequential [
            JSExpr.SetAttribute(JSExpr.Var "target", name, value)
            JSExpr.SetAttribute(JSExpr.Var "target", "data-after", "updated")
        ]
        |> JSExpr.toString

    let private check cases =
        for name, value, encoded in cases do
            let code = generate name value
            let prefix = $"aardvark.setAttribute(target, \"{name}\", "
            let suffix = ");aardvark.setAttribute(target, \"data-after\", \"updated\");"
            Expect.equal code (prefix + "\"" + encoded + "\"" + suffix) $"Unexpected JavaScript for {name}"

            // The emitted double-quoted literal is also valid JSON, providing an independent decoder.
            let literal = code.Substring(prefix.Length, code.Length - prefix.Length - suffix.Length)
            Expect.equal (JsonSerializer.Deserialize<string> literal) value $"Changed attribute value for {name}"

    [<Tests>]
    let tests =
        testList "Javascript Tests" [
            test "SetAttribute encodes line breaks and multiline handlers" {
                check lineBreaks
            }
            test "SetAttribute encodes control and delimiter characters" {
                check encodedCharacters
            }
            test "SetAttribute preserves plain and already-escaped text" {
                check literals
            }
        ]
