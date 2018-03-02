namespace Aardvark.Cef

open System
open System.Text.RegularExpressions

open System.Reflection
open System.Windows.Forms
open System.Threading
open System.Collections.Concurrent
open System.Collections.Generic

open Aardvark.Base

open Xilium.CefGlue.Wrapper
open Xilium.CefGlue
open Aardvark.Cef

type AardvarkIO(browser : CefBrowser, ctx : CefV8Context) as this =
    inherit CefV8Handler()

    let functions : Dictionary<string, CefV8Value[] -> CefResult> = 
        typeof<AardvarkIO>.GetMethods(BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
            |> Seq.filter (fun mi -> mi.ReturnType = typeof<CefResult> && mi.GetParameters().Length = 1)
            |> Seq.map (fun mi -> mi.Name, FunctionReflection.buildFunction this mi)
            |> Dictionary.ofSeq

                
    let runner = ctx.GetTaskRunner()
    let reactions = ConcurrentDictionary<int, CefV8Value[] -> CefV8Value>()
    let mutable currentId = 0

    member x.FunctionNames = 
        functions.Keys

    override x.Execute(name, _, args, ret, exn) =
        match functions.TryGetValue(name) with
            | (true, f) ->
                match f args with
                    | Success v -> 
                        ret <- v
                        true
                    | NoRet ->
                        true
                    | Error err ->
                        exn <- err
                        false
            | _ ->
                exn <- sprintf "unknown function %s" name
                false

    member x.got(response : Response) =
        match reactions.TryRemove response.id with
            | (true, f) ->
                runner.PostTask {
                    new CefTask() with
                        member x.Execute() =
                            ctx.Use (fun () ->
                                match response with
                                    | Ok(_,files) ->
                                        let files = List.toArray files
                                        use arr = CefV8Value.CreateArray(files.Length)
                                        for i in 0 .. files.Length - 1 do
                                            use file = CefV8Value.CreateString files.[i]
                                            arr.SetValue(i, file) |> ignore

                                        f [| arr |] |> ignore
                                    | Abort _ ->
                                        f [| |] |> ignore
                            )
                }
            | _ ->
                true

    member x.openFileDialog(args : CefV8Value[]) =
        let config =
            match args with
                | [| FunctionValue f |] -> Some (OpenDialogConfig.empty, f)
                | [| v; FunctionValue f |] -> Some(OpenDialogConfig.parse v, f)
                | _ -> None

        match config with
            | Some (config, f) -> 
                let id = Interlocked.Increment(&currentId)
                reactions.[id] <- f
                use msg = IPC.toProcessMessage (OpenDialog(id, config))
                browser.SendProcessMessage(CefProcessId.Browser, msg) |> ignore

                NoRet
            | _ ->
                Error "no argument for openFileDialog"