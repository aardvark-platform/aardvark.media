module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model


let initialCamera = { 
        CameraController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | OpenFiles m -> 
            { model with currentFiles = PList.ofList m }

let viewScene (model : MModel) =
    Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }

[<AutoOpen>]
module MoveToCore = 

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
                    
                    if not (String.isEmpty cfg.startPath) then
                        yield sprintf "startPath: '%s'" (Aardvark.Service.PathUtils.toUnixStyle cfg.startPath)

                    if cfg.filters.Length > 0 then
                        yield sprintf "filters: %s" (cfg.filters |> Seq.map (sprintf "'%s'") |> String.concat ", " |> sprintf "[%s]")
                        
                    yield sprintf "allowMultiple: %s" (if cfg.allowMultiple then "true" else "false")


                ]

            properties |> String.concat ", " |> sprintf "{ %s }"

    
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



let view (model : MModel) =
    require Html.semui (
        body [ style "background: black"] [
            div [clazz "ui inverted segment" ] [
                openDialogButton 
                    { OpenDialogConfig.file with allowMultiple = true; title = "ROCK THE POWER. ROCKET POWER" }
                    [ clazz "ui green button"; onChooseFiles OpenFiles ] 
                    [ text "Open File" ]
            ]


            div [clazz "ui inverted segment"] [
                Incremental.div (AttributeMap.ofList [clazz "ui inverted relaxed divided list"]) (
                    model.currentFiles |> AList.map (fun f ->
                        div [clazz "item"] [
                            div [ clazz "content" ] [
                                div [clazz "ui orange label"] [ text f ] 
                            ]
                        ]
                    )
                )
            ]
        ]
    )

let threads (model : Model) = 
    ThreadPool.empty

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               currentFiles = PList.empty
            }
        update = update 
        view = view
    }
