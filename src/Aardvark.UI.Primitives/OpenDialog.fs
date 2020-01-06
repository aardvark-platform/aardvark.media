namespace Aardvark.UI.Primitives

open System
open Aardvark.UI


type OpenDialogMode =
    | File = 0
    | Folder = 1

type OpenDialogConfig =
    {
        mode            : OpenDialogMode
        title           : string
        startPath       : string
        filters         : string[]
        allowMultiple   : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OpenDialogConfig =
    let file =
        {
            mode = OpenDialogMode.File
            title = "Open File"
            startPath = ""
            filters = [||]
            allowMultiple = false
        }

    let folder =
        {
            mode = OpenDialogMode.Folder
            title = "Open Folder"
            startPath = ""
            filters = [||]
            allowMultiple = false
        }

    let internal toJSON (cfg : OpenDialogConfig) =
        let properties = 
            [
                match cfg.mode with
                    | OpenDialogMode.File -> yield "mode: 'file'"
                    | OpenDialogMode.Folder -> yield "mode: 'folder'"
                    | _ -> ()

                yield sprintf "title: '%s'" cfg.title
                    
                if not (String.IsNullOrWhiteSpace cfg.startPath) then
                    yield sprintf "startPath: '%s'" (Aardvark.Service.PathUtils.toUnixStyle cfg.startPath)

                if cfg.filters.Length > 0 then
                    yield sprintf "filters: %s" (cfg.filters |> Seq.map (sprintf "'%s'") |> String.concat ", " |> sprintf "[%s]")
                        
                yield sprintf "allowMultiple: %s" (if cfg.allowMultiple then "true" else "false")


            ]

        properties |> String.concat ", " |> sprintf "{ %s }"

[<AutoOpen>]
module OpenFileDialogExtensions =
    [<AutoOpen>]
    module Static =  
        let openDialogButton (config : OpenDialogConfig) (att : list<string * AttributeValue<'msg>>) (content : list<DomNode<'msg>>) =
            let cfg = OpenDialogConfig.toJSON config
            button [
                yield clientEvent "onclick" ("aardvark.openFileDialog(" + cfg + ", function(files) { if(files != undefined) aardvark.processEvent('__ID__', 'onchoosefile', files); });")
                yield! att
            ] content

        let onChooseFiles (chosen : list<string> -> 'msg) =
            onEvent "onchoosefile" [] (List.head >> Aardvark.Service.Pickler.json.UnPickleOfString >> List.map Aardvark.Service.PathUtils.ofUnixStyle >> chosen)
        
        let onChooseFile (chosen : string -> 'msg) =
            onEvent "onchoosefile" [] (List.head >> Aardvark.Service.Pickler.json.UnPickleOfString >> List.head >> Aardvark.Service.PathUtils.ofUnixStyle >> chosen)
