/// Regression tests for the REST API and the MCP server. Every test starts the real web parts on a free port
/// against a fresh app (three boxes, none selected) and talks to them over HTTP.
module RemoteControlTests

open System
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading

open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Suave
open Expecto

// The Aardvark opens shadow Error with another type, restore the Result cases.
open Microsoft.FSharp.Core

/// Aardvark.UI defines its own HttpMethod.
type private Method = System.Net.Http.HttpMethod

type private Server = { client : HttpClient; port : int }

/// Registers the scene graph semantics the app view needs, like Program.main does. Under a test host the entry
/// assembly is the host, so introspection has to be pointed at this assembly.
let private init =
    lazy (
        IntrospectionProperties.CustomEntryAssembly <- Reflection.Assembly.GetExecutingAssembly()
        Aardvark.Init()
    )

let private withServer (test : Server -> unit) =
    init.Force()
    use app = App.app |> App.start
    use cts = new CancellationTokenSource()
    let port = Server.getFreeTcpPort IPAddress.Loopback
    let server = Server.startLocalhost port cts.Token [ Rest.webPart app; Mcp.webPart app ]
    use client = new HttpClient(BaseAddress = Uri $"http://localhost:{port}/")

    try
        test { client = client; port = port }
    finally
        cts.Cancel()
        try server.Wait() with _ -> ()

let private send (s : Server) (meth : Method) (path : string) (headers : list<string * string>) (body : string option) =
    use request = new HttpRequestMessage(meth, path)
    body |> Option.iter (fun b -> request.Content <- new StringContent(b, Encoding.UTF8, "application/json"))
    for name, value in headers do request.Headers.TryAddWithoutValidation(name, value) |> ignore

    use response = s.client.Send request
    int response.StatusCode, response.Content.ReadAsStringAsync().Result

let private post (s : Server) (path : string) (body : string) =
    send s Method.Post path [] (Some body)

let private scene (s : Server) =
    let status, body = send s Method.Get "/api/scene" [] None
    Expect.equal status 200 "GET /api/scene"
    JsonDocument.Parse(body).RootElement

let private boxCount (s : Server) =
    (scene s).GetProperty("boxCount").GetInt32()

let private selectedCount (s : Server) =
    (scene s).GetProperty("selectedCount").GetInt32()

/// POSTs a JSON-RPC message to /mcp and returns the status code and the parsed body (if any).
let private rpc (s : Server) (body : string) =
    let status, response = post s "/mcp" body
    status, (if String.IsNullOrEmpty response then None else Some (JsonDocument.Parse(response).RootElement))

let private expectInvalidRequest (expectedId : string) (response : JsonElement) =
    Expect.equal (response.GetProperty("error").GetProperty("code").GetInt32()) -32600 "error code"
    Expect.equal (response.GetProperty("id").GetRawText()) expectedId "id"

[<Tests>]
let boxRanges =
    testList "Box ranges" [
        testCase "Huge range is rejected without enumerating it" <| fun () ->
            match Api.resolveBoxes App.initial [ "1-2147483647" ] with
            | Error e -> Expect.stringContains e "out of range" "error message"
            | Ok _ -> failtest "Expected an error for an out-of-range range."

        testCase "Range within bounds resolves to the boxes" <| fun () ->
            match Api.resolveBoxes App.initial [ "2-3" ] with
            | Ok ids -> Expect.equal ids.Length 2 "resolved ids"
            | Error e -> failtest e

        testCase "Range starting at zero is rejected" <| fun () ->
            Expect.isError (Api.resolveBoxes App.initial [ "0-2" ]) "range starting at 0"

        testCase "Huge range via REST answers 400" <| fun () ->
            withServer (fun s ->
                let status, _ = post s "/api/selection" """{"boxes":["1-2147483647"]}"""
                Expect.equal status 400 "status"
                Expect.equal (selectedCount s) 0 "selection unchanged"
            )
    ]

[<Tests>]
let origins =
    testList "Origin validation" [
        testCase "REST rejects a foreign origin and leaves the app unchanged" <| fun () ->
            withServer (fun s ->
                let status, _ = send s Method.Post "/api/selection/invert" [ "Origin", "https://evil.example" ] None
                Expect.equal status 403 "status"
                Expect.equal (selectedCount s) 0 "selection unchanged"
            )

        testCase "REST rejects reading the scene from a foreign origin" <| fun () ->
            withServer (fun s ->
                let status, _ = send s Method.Get "/api/scene" [ "Origin", "https://evil.example" ] None
                Expect.equal status 403 "status"
            )

        testCase "REST rejects the null origin" <| fun () ->
            withServer (fun s ->
                let status, _ = send s Method.Post "/api/selection/invert" [ "Origin", "null" ] None
                Expect.equal status 403 "status"
            )

        testCase "REST accepts loopback origins" <| fun () ->
            withServer (fun s ->
                for origin in [ $"http://localhost:{s.port}"; $"http://127.0.0.1:{s.port}" ] do
                    let status, _ = send s Method.Post "/api/selection/invert" [ "Origin", origin ] None
                    Expect.equal status 200 origin
            )

        testCase "REST accepts requests without an origin" <| fun () ->
            withServer (fun s ->
                let status, _ = send s Method.Post "/api/selection/invert" [] None
                Expect.equal status 200 "status"
                Expect.equal (selectedCount s) 3 "selection inverted"
            )

        testCase "MCP rejects a foreign origin" <| fun () ->
            withServer (fun s ->
                let body = Some """{"jsonrpc":"2.0","id":1,"method":"ping"}"""
                let status, _ = send s Method.Post "/mcp" [ "Origin", "https://evil.example" ] body
                Expect.equal status 403 "status"
            )
    ]

[<Tests>]
let jsonRpc =
    testList "JSON-RPC envelope" [
        testCase "Valid request is answered" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s """{"jsonrpc":"2.0","id":1,"method":"ping"}"""
                Expect.equal status 200 "status"
                Expect.isTrue (response.Value.TryGetProperty("result") |> fst) "has result"
            )

        testCase "Request with null id is answered" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s """{"jsonrpc":"2.0","id":null,"method":"ping"}"""
                Expect.equal status 200 "status"
                Expect.equal (response.Value.GetProperty("id").ValueKind) JsonValueKind.Null "id"
                Expect.isTrue (response.Value.TryGetProperty("result") |> fst) "has result"
            )

        testCase "Notification gets 202 without body" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s """{"jsonrpc":"2.0","method":"notifications/initialized"}"""
                Expect.equal status 202 "status"
                Expect.isNone response "no body"
            )

        testCase "Response from the client gets 202 without body" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s """{"jsonrpc":"2.0","id":1,"result":{}}"""
                Expect.equal status 202 "status"
                Expect.isNone response "no body"
            )

        testCase "Non-object message is an invalid request" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s "42"
                Expect.equal status 400 "status"
                expectInvalidRequest "null" response.Value
            )

        testCase "Empty batch is an invalid request" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s "[]"
                Expect.equal status 400 "status"
                expectInvalidRequest "null" response.Value
            )

        testCase "Wrong JSON-RPC version is an invalid request" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s """{"jsonrpc":"1.0","id":1,"method":"ping"}"""
                Expect.equal status 400 "status"
                expectInvalidRequest "1" response.Value
            )

        testCase "Missing JSON-RPC version is an invalid request" <| fun () ->
            withServer (fun s ->
                let _, response = rpc s """{"id":1,"method":"ping"}"""
                expectInvalidRequest "1" response.Value
            )

        testCase "Id of invalid type is an invalid request with null id" <| fun () ->
            withServer (fun s ->
                let _, response = rpc s """{"jsonrpc":"2.0","id":{},"method":"ping"}"""
                expectInvalidRequest "null" response.Value
            )

        testCase "Non-string method is an invalid request" <| fun () ->
            withServer (fun s ->
                let _, response = rpc s """{"jsonrpc":"2.0","id":1,"method":5}"""
                expectInvalidRequest "1" response.Value
            )

        testCase "Scalar params are an invalid request" <| fun () ->
            withServer (fun s ->
                let _, response = rpc s """{"jsonrpc":"2.0","id":1,"method":"ping","params":5}"""
                expectInvalidRequest "1" response.Value
            )

        testCase "Batch of invalid messages gets one error per message" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s "[1, 2]"
                Expect.equal status 200 "status"
                let errors = response.Value.EnumerateArray() |> Seq.toList
                Expect.equal errors.Length 2 "error count"
                errors |> List.iter (expectInvalidRequest "null")
            )

        testCase "Batch only answers requests" <| fun () ->
            withServer (fun s ->
                let status, response = rpc s """[{"jsonrpc":"2.0","method":"notifications/initialized"},{"jsonrpc":"2.0","id":7,"method":"ping"}]"""
                Expect.equal status 200 "status"
                let responses = response.Value.EnumerateArray() |> Seq.toList
                Expect.equal responses.Length 1 "response count"
                Expect.equal (responses.Head.GetProperty("id").GetInt32()) 7 "id"
            )
    ]

[<Tests>]
let protocolVersion =
    let ping = Some """{"jsonrpc":"2.0","id":1,"method":"ping"}"""

    testList "MCP-Protocol-Version header" [
        testCase "Unsupported version is rejected before dispatch" <| fun () ->
            withServer (fun s ->
                let call = Some """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"invert_selection"}}"""
                let status, body = send s Method.Post "/mcp" [ "MCP-Protocol-Version", "2099-01-01" ] call
                Expect.equal status 400 "status"
                expectInvalidRequest "null" (JsonDocument.Parse(body).RootElement)
                Expect.equal (selectedCount s) 0 "tool not executed"
            )

        testCase "Supported versions are accepted" <| fun () ->
            withServer (fun s ->
                for version in [ "2025-11-25"; "2025-06-18"; "2025-03-26" ] do
                    let status, _ = send s Method.Post "/mcp" [ "MCP-Protocol-Version", version ] ping
                    Expect.equal status 200 version
            )

        testCase "Missing header is accepted" <| fun () ->
            withServer (fun s ->
                let status, _ = send s Method.Post "/mcp" [] ping
                Expect.equal status 200 "status"
            )
    ]

[<Tests>]
let addBoxes =
    testList "Add boxes" [
        testCase "Explicit zero count is rejected" <| fun () ->
            withServer (fun s ->
                let status, _ = post s "/api/boxes" """{"count":0}"""
                Expect.equal status 400 "status"
                Expect.equal (boxCount s) 3 "box count unchanged"
            )

        testCase "Omitted count adds one box" <| fun () ->
            withServer (fun s ->
                let status, _ = post s "/api/boxes" "{}"
                Expect.equal status 200 "status"
                Expect.equal (boxCount s) 4 "box count"
            )

        testCase "Given count and color are used" <| fun () ->
            withServer (fun s ->
                let status, _ = post s "/api/boxes" """{"count":2,"color":"red"}"""
                Expect.equal status 200 "status"
                Expect.equal (boxCount s) 5 "box count"
            )
    ]
