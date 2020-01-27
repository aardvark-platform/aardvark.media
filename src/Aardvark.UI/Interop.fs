namespace Aardvark.UI

#nowarn "1337"
open Aardvark.Base


[<AutoOpen>]
module JSON =
    open Newtonsoft.Json
    open Newtonsoft.Json.Linq

    [<CompilerMessage("internal", 1337, IsHidden = true)>]
    type JTokenCreator private () =
        static member inline Token(v : bool) : JToken = JToken.op_Implicit v
        static member inline Token(v : string) : JToken = JToken.op_Implicit v
        static member inline Token(v : int8) : JToken = JToken.op_Implicit v
        static member inline Token(v : int16) : JToken = JToken.op_Implicit v
        static member inline Token(v : int32) : JToken = JToken.op_Implicit v
        static member inline Token(v : int64) : JToken = JToken.op_Implicit v
        static member inline Token(v : uint8) : JToken = JToken.op_Implicit v
        static member inline Token(v : uint16) : JToken = JToken.op_Implicit v
        static member inline Token(v : uint32) : JToken = JToken.op_Implicit v
        static member inline Token(v : uint64) : JToken = JToken.op_Implicit v
        static member inline Token(v : float32) : JToken = JToken.op_Implicit v
        static member inline Token(v : float) : JToken = JToken.op_Implicit v
        static member inline Token(v : decimal) : JToken = JToken.op_Implicit v
        static member inline Token(v : System.Uri) : JToken = JToken.op_Implicit v
        static member inline Token(v : System.DateTime) : JToken = JToken.op_Implicit v
        static member inline Token(v : System.DateTimeOffset) : JToken = JToken.op_Implicit v
        static member inline Token(v : System.TimeSpan) : JToken = JToken.op_Implicit v 
        static member inline Token(v : System.Guid) : JToken = JToken.op_Implicit v 
        static member inline Token(v : byte[]) : JToken = JToken.op_Implicit v 

        static member inline Token(v : JObject) : JToken = v :> JToken
        static member inline Token(v : string[]) : JToken = JArray(v) :> JToken
        static member inline Token(v : seq<string>) : JToken = JArray(Seq.toArray (Seq.cast<obj> v)) :> JToken
        static member inline Token(v : seq<JObject>) : JToken = JArray(Seq.toArray (Seq.cast<obj> v)) :> JToken
        
    let inline private jtokenAux (t : ^a) (v : ^b) : ^c =
        ((^a or ^b) : (static member Token : ^b -> ^c) (v))

    let inline private jtoken v = jtokenAux Unchecked.defaultof<JTokenCreator> v
    
    [<CompilerMessage("internal", 1337, IsHidden = true)>]
    type JSONBuilder() =
        member inline x.Zero() = []

        member inline x.Yield(k : string, v) = [k, jtoken v]
        
        member inline x.Delay(action : unit -> list<string * JToken>) =
            action
            
        member inline x.Combine(l : list<string * JToken>, r : unit -> list<string * JToken>) =
            l @ r()

        member inline x.For(elements : seq<'a>, mapping : 'a -> list<string * JToken>) =
            elements |> Seq.toList |> List.collect mapping

        member inline x.Run(action : unit -> list<string * JToken>) =
            let o = JObject()
            for (k,v) in action() do
                o.[k] <- v
            o

    let json = JSONBuilder()




[<RequireQualifiedAccess>]
type DialogKind =
    | OpenFolder
    | OpenFile of multiple : bool
    | SaveFile

type FileFilter =
    { name : string; extensions : list<string> }

type DialogConfig = 
    {
        kind        : DialogKind
        title       : option<string>
        buttonLabel : option<string>
        directory   : option<string>
        showHidden  : bool
        filters     : list<FileFilter>
    }
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DialogConfig =
    
    let openFile = 
        {
            kind = DialogKind.OpenFile true
            title = None
            buttonLabel = None
            directory = None
            showHidden = false
            filters = []
        }
        
    let openFolder = 
        {
            kind = DialogKind.OpenFolder
            title = None
            buttonLabel = None
            directory = None
            showHidden = false
            filters = []
        }

    let saveFile = 
        {
            kind = DialogKind.SaveFile
            title = None
            buttonLabel = None
            directory = None
            showHidden = false
            filters = []
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Dialog =
    let onEvent (eventName : string) (cfg : DialogConfig) (callback : list<string> -> 'msg) =

        let config =
            json {
                match cfg.kind with
                | DialogKind.OpenFolder -> "properties", [ "openDirectory" ]
                | DialogKind.OpenFile false -> "properties", [ "openFile" ]
                | DialogKind.OpenFile true -> "properties", [ "openFile"; "multiSelections" ]
                | DialogKind.SaveFile -> ()

                match cfg.title with
                | Some title -> "title", title
                | None -> ()

                match cfg.buttonLabel with
                | Some label -> "buttonLabel", label
                | None -> ()

                match cfg.directory with
                | Some dir -> "defaultPath", dir
                | None -> ()

                "showHidden", cfg.showHidden
                match cfg.filters with
                | [] -> ()
                | _ -> 
                    "filters", [
                        for f in cfg.filters do
                            json { "name", f.name; "extensions", f.extensions }
                    ]
            }

        let cfgStr = config.ToString(Newtonsoft.Json.Formatting.None).Replace("\"", "'")

        let cb = 
            match cfg.kind with
            | DialogKind.SaveFile ->
                String.concat "" [
                    "aardvark.electron.remote.dialog.showSaveDialog(" + cfgStr + ").then((e) => {"
                    "aardvark.processEvent('__ID__', 'dialogchoose', (e.filePath ? [e.filePath] : []));"
                    "});"
                ]
            | _ -> 
                String.concat "" [
                    "aardvark.electron.remote.dialog.showOpenDialog(" + cfgStr + ").then((e) => {"
                    "aardvark.processEvent('__ID__', 'dialogchoose', (e.filePaths ? e.filePaths : []));"
                    "});"
                ]
        [
            clientEvent eventName cb
            "dialogchoose", 
                AttributeValue.Event {
                    clientSide = fun _ _ -> ""
                    serverSide = fun _ _ args ->
                        match args with
                        | [arg] -> 
                            try 
                                let o = Newtonsoft.Json.Linq.JArray.Parse arg
                                let res = System.Collections.Generic.List<string>()
                                for i in 0 .. o.Count - 1 do
                                    let e = o.[i]
                                    try 
                                        let str = (Newtonsoft.Json.Linq.JToken.op_Explicit e : string)
                                        res.Add str
                                    with _ ->
                                        ()

                                Seq.singleton (callback (Seq.toList res))
                                    
                            with _ ->
                                Seq.empty
                        | _ ->
                            Seq.empty
                }
        ]

    let inline onClick (cfg : DialogConfig) (callback : list<string> -> 'msg) =
        onEvent "onclick" cfg callback