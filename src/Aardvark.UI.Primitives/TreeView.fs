namespace Aardvark.UI.Primitives

open System
open Suave

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.UI

module TreeViewFancy =

    let s = MBrace.FsPickler.Json.FsPickler.CreateJsonSerializer(false, true)
    let pickle o = s.PickleToString(o).Replace("\"", "\\\"") |> sprintf "emit(JSON.parse(\"%s\"));"


    let view () =
        require [ 
            { kind = Script; name = "jquery"; url = "https://code.jquery.com/jquery-3.1.1.min.js" } 
            { kind = Script; name = "jquery-ui"; url = "https://code.jquery.com/ui/1.12.0/jquery-ui.min.js" } 
            { kind = Script; name = "jquery-resize"; url = "https://cdnjs.cloudflare.com/ajax/libs/jquery-resize/1.1/jquery.ba-resize.min.js" } 
            { kind = Script; name = "fancytree"; url = "https://cdnjs.cloudflare.com/ajax/libs/jquery.fancytree/2.22.5/jquery.fancytree-all.min.js" } 
            { kind = Stylesheet; name = "fancytree-ui"; url = "https://cdnjs.cloudflare.com/ajax/libs/jquery.fancytree/2.22.5/skin-win8/ui.fancytree.min.css" }
            { kind = Script; name = "treeview"; url = "./treeview.js" } 
                
        ] (
            div [] [
                div [attribute "id" "tree"] [
                    ul [attribute "id" "treeData"; attribute "style" "display: none;"] [
                    
                        li [attribute "id" "id1"; attribute "title" "asfas"] [
                            text "asofuhasofhweuh"
                        ]

                    ]
                ]
            ]
        )

