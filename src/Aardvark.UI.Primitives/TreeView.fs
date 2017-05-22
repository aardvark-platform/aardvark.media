namespace Aardvark.UI.Primitives

open System
open Suave

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.UI

module TreeViewFancy =
    open TreeViewModel

    let s = MBrace.FsPickler.Json.FsPickler.CreateJsonSerializer(false, true)
    let emit o = s.PickleToString(o).Replace("\"", "\\\"") |> sprintf "emit(JSON.parse(\"%s\"));"
    
    type NodeSelected =
        {
            node : string
            selected : bool
        }
    let msg = TreeViewMessage.Add ({pointer = Root; mode = FirstChild}, NodeData.node "afhasf" false None "asfdg")
    let view (mmodel : MTreeViewModel) (f : TreeViewMessage -> 'msg) : DomNode<'msg> =
        require [ 
            { kind = Script; name = "jquery"; url = "https://code.jquery.com/jquery-3.1.1.min.js" } 
            { kind = Script; name = "jquery-ui"; url = "https://code.jquery.com/ui/1.12.0/jquery-ui.min.js" } 
            { kind = Script; name = "jquery-resize"; url = "https://cdnjs.cloudflare.com/ajax/libs/jquery-resize/1.1/jquery.ba-resize.min.js" } 
            { kind = Script; name = "fancytree"; url = "https://cdnjs.cloudflare.com/ajax/libs/jquery.fancytree/2.22.5/jquery.fancytree-all.min.js" } 
            { kind = Stylesheet; name = "fancytree-ui"; url = "https://cdnjs.cloudflare.com/ajax/libs/jquery.fancytree/2.22.5/skin-win8/ui.fancytree.min.css" }
            { kind = Script; name = "treeview"; url = "./treeview.js" } 
                
        ] (
            div [] [

                text "asdgasdgasdgasdgasdg"
                br []
                button [
                    
                    //onMouseClick ( fun _ -> 
                    //    TreeViewMessage.Add ({pointer = Root; mode = FirstChild}, NodeData.node "afhasf" false None "asfdg") |> f
                    //)
                    yield js "onclick" (emit msg)
                ] [ text "addasssssssdgg" ]



                div [
                    attribute "id" "tree"
                    onEvent "TreeViewSelected" 
                            ["{ node: event.detail.node, selected: event.detail.selected }"]
                            (List.head >> Pickler.json.UnPickleOfString >> ( fun (ns : NodeSelected) ->
                                Log.warn "nodeselected: %A" ns
                                f msg
                            ))
                ] [
                    ul [attribute "id" "treeData"; attribute "style" "display: none;"] [
                        li [attribute "id" "id1"; attribute "title" "asfas"] [
                            text "asofuhasofhweuh"
                        ]
                        
                    ]
                ]

            ]
        )

    let update (model : TreeViewModel) (msg : TreeViewMessage) =
        emit msg
        model

