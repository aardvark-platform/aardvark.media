namespace Aardvark.UI

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