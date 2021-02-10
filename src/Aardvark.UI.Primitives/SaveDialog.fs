namespace Aardvark.UI.Primitives

open System
open Aardvark.UI

type FileFilter =
    {
        name : string
        extensionWithoutDot : string
    }

type SaveDialogConfig =
    {
        title           : string
        startPath       : string
        filters         : FileFilter[]
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SaveDialogConfig =
    let Default =
        {
            title = "Save File"
            startPath = ""
            filters = [||]
        }

    let internal toJSON (cfg : SaveDialogConfig) =
        let properties = 
            [
                yield sprintf "title: '%s'" cfg.title
                    
                if not (String.IsNullOrWhiteSpace cfg.startPath) then
                    yield sprintf "startPath: '%s'" (Aardvark.Service.PathUtils.toUnixStyle cfg.startPath)

                if cfg.filters.Length > 0 then
                    yield sprintf "filters: [%s]" (cfg.filters |> Seq.map (fun {name = name; extensionWithoutDot = ext} -> sprintf "{name:'%s',extensions:['%s']}" name ext) |> String.concat ", ")
            ]
        properties |> String.concat ", " |> sprintf "{ %s }"

[<AutoOpen>]
module SaveFileDialogExtensions =
    [<AutoOpen>]
    module Static =  
        let saveDialogButton (config : SaveDialogConfig) (att : list<string * AttributeValue<'msg>>) (content : list<DomNode<'msg>>) =
            let cfg = SaveDialogConfig.toJSON config
            button [
                yield clientEvent "onclick" ("aardvark.saveFileDialog(" + cfg + ", function(file) { if(file != undefined) aardvark.processEvent('__ID__', 'onchoosefile', file); });")
                yield! att
            ] content

