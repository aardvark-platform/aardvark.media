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

type CefResult =
    | Success of CefV8Value
    | NoRet
    | Error of string

[<AutoOpen>]
module private CefV8ContextExt =
    type CefV8Context with
        member x.Use (f : unit -> 'a) =
            let mutable entered = false
            try
                entered <- x.Enter()
                f()
            finally
                if entered then
                    let exited = x.Exit()
                    if not exited then 
                        failwith "[Cef] could not exit CefV8Context"

[<AutoOpen>]
module Patterns =

    let (|StringValue|_|) (v : CefV8Value) =
        if v.IsString then
            Some (v.GetStringValue())
        else
            None

    let (|IntValue|_|) (v : CefV8Value) =
        if v.IsInt then
            Some (v.GetIntValue())
        else
            None

    let (|UIntValue|_|) (v : CefV8Value) =
        if v.IsUInt then
            Some (v.GetUIntValue())
        else
            None

    let (|BoolValue|_|) (v : CefV8Value) =
        if v.IsBool then
            Some (v.GetBoolValue())
        else
            None

    let (|DateValue|_|) (v : CefV8Value) =
        if v.IsDate then
            Some (v.GetDateValue())
        else
            None

    let (|DoubleValue|_|) (v : CefV8Value) =
        if v.IsDouble then
            Some (v.GetDoubleValue())
        else
            None

    let (|NullValue|_|) (v : CefV8Value) =
        if v.IsNull then
            Some ()
        else
            None

    let (|UndefinedValue|_|) (v : CefV8Value) =
        if v.IsUndefined then
            Some ()
        else
            None

    let (|ObjectValue|_|) (v : CefV8Value) =
        if v.IsObject then
            let keys = v.GetKeys()
            let values = Seq.init keys.Length (fun i -> keys.[i], v.GetValue(keys.[i])) |> Map.ofSeq

            Some values
        else
            None
                
                
    let (|FunctionValue|_|) (v : CefV8Value) =
        if v.IsFunction then
            Some (fun args -> v.ExecuteFunction(v, args))
        else
            None

    let (|ArrayValue|_|) (v : CefV8Value) =
        if v.IsArray then
            let len = v.GetArrayLength()
            let arr = Array.init len (fun i -> v.GetValue i)
            Some arr
        else
            None


type OpenDialogMode =
    | File = 0
    | Folder = 1

type OpenDialogConfig =
    {
        mode            : OpenDialogMode
        title           : string
        startPath       : string
        filters         : string[]
        activeFilter    : int
        allowMultiple   : bool
    }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OpenDialogConfig =
    let empty =
        {
            mode = OpenDialogMode.File
            title = "Open File"
            startPath = Environment.GetFolderPath Environment.SpecialFolder.Desktop
            filters = [||]
            activeFilter = -1
            allowMultiple = false
        }

    let parse (value : CefV8Value) =
        let mutable res = empty

        match value with
            | ObjectValue map ->
                match Map.tryFind "mode" map with
                    | Some (StringValue "file") -> res <- { res with mode = OpenDialogMode.File }
                    | Some (StringValue "folder") -> res <- { res with mode = OpenDialogMode.Folder }
                    | _ -> ()

                match Map.tryFind "startPath" map with
                    | Some (StringValue path) -> res <- { res with startPath = path }
                    | _ -> ()

                match Map.tryFind "title" map with
                    | Some (StringValue v) -> res <- { res with title = v }
                    | _ -> ()

                match Map.tryFind "filters" map with
                    | Some (ArrayValue filters) ->
                        let filters = filters |> Array.choose (function StringValue v -> Some v | _ -> None)
                        res <- { res with filters = filters }

                    | _ ->
                        ()

                match Map.tryFind "activeFilter" map with
                    | Some (IntValue v) -> res <- { res with activeFilter = v }
                    | Some (UIntValue v) -> res <- { res with activeFilter = int v }
                    | Some (DoubleValue v) -> res <- { res with activeFilter = int v }
                    | _ -> ()

                match Map.tryFind "allowMultiple" map with
                    | Some (BoolValue v) -> res <- { res with allowMultiple = v }
                    | _ -> ()

            | _ ->
                ()

        res

type Command =
    | OpenDialog of int * OpenDialogConfig

[<RequireQualifiedAccess>]
type Response =
    | Error of int * string
    | Abort of int
    | Ok of int * list<string>

    member x.id =
        match x with
            | Error(i,_) -> i
            | Abort i -> i
            | Ok(i,_) -> i

module IPC =
    let pickler = MBrace.FsPickler.BinarySerializer()

    let toProcessMessage (a : 'a) =
        let message = CefProcessMessage.Create(typeof<'a>.Name)
        use v = CefBinaryValue.Create(pickler.Pickle a)
        message.Arguments.SetBinary(0, v) |> ignore
        message

    let tryReadProcessMessage<'a> (msg : CefProcessMessage) : Option<'a> =
        if msg.Name = typeof<'a>.Name then
            let bin = msg.Arguments.GetBinary(0)
            bin.ToArray() |> pickler.UnPickle|> Some
        else
            None
