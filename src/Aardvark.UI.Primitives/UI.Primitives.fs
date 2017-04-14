namespace Aardvark.UI

open System
open Suave

open Aardvark.Base
open Aardvark.Base.Incremental

module UI = 
    open Aardvark.Base
    open Aardvark.UI

    let onWheel (f : Aardvark.Base.V2d -> 'msg) =
        let serverClick (args : list<string>) : Aardvark.Base.V2d = 
            let delta = List.head args |> Pickler.unpickleOfJson
            delta  / Aardvark.Base.V2d(-100.0,-100.0) // up is down in mouse wheel events

        onEvent "onwheel" ["{ X: event.deltaX.toFixed(), Y: event.deltaY.toFixed()  }"] (serverClick >> f)

module Combinators =
    let (=>) n v = attribute n v

open Combinators

type NumericInputType = Slider | InputBox

module List =
    /// The intersperse function takes an element and a list and
    /// 'intersperses' that element between the elements of the list.
    let intersperse sep ls =
        List.foldBack (fun x -> function
            | [] -> [x]
            | xs -> x::sep::xs) ls []

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Numeric = 
    open Aardvark.Base
    open Aardvark.UI

    type Action = 
        | Set of string
        | Step of float    

    let update (model : NumericInput) (action : Action) =
        match action with
            | Set s     ->
                let parsed = 0.0
                match Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                    | (true,v) -> { model with value = Fun.Clamp(v, model.min, model.max)  }
                    | _ -> 
                        printfn "validation failed: %s" s
                        model  
             | Step v -> { model with value = model.value + v }

    let formatNumber (format : string) (value : float) =
        String.Format(Globalization.CultureInfo.InvariantCulture, format, value)

    let numericField (set : string -> Action) (model : MNumericInput) inputType =         
      
        let attributes = 
            amap {
                yield style "text-align:right"
                match inputType with
                    | Slider ->   
                        yield "type" => "range"
                        yield onInput set   // continous updates for slider
                    | InputBox -> 
                        yield "type" => "number"
                        yield onChange set  // batch updates for input box (to let user type)

                let! step = model.step
                yield UI.onWheel (fun d ->  d.Y * step |> Step)

                let! min = model.min
                let! max = model.max
                yield "step" => sprintf "%f" step
                yield "min"  => sprintf "%f" min
                yield "max"  => sprintf "%f" max

                let! value = model.value

                let! format = model.format
                yield "value" => formatNumber format value
            } 

        Incremental.input (AttributeMap.ofAMap attributes)

    let view' (inputTypes : list<NumericInputType>) (model : MNumericInput) : DomNode<Action> =
        inputTypes 
            |> List.map (numericField Set model) 
            |> List.intersperse (text " ") 
            |> div []

    let view (model : MNumericInput) =
        view' [InputBox]

    let init = {
        value   = 3.0
        min     = 0.0
        max     = 15.0
        step    = 1.5
        format  = "{0:0.00}"
    }

    let app' inputTypes =
        {
            unpersist = Unpersist.instance
            threads = fun _ -> ThreadPool.create()
            initial = init
            update = update
            view = view' inputTypes
        }

    let app = app' [NumericInputType.InputBox; NumericInputType.InputBox; NumericInputType.Slider]

    let start () =
        App.start app


module Html =

    module Layout =
        let boxH ch = td [clazz "collapsing"; style "padding: 0px 5px 0px 0px"] ch

        let horizontal ch = table [clazz "ui table"; style "backgroundColor: transparent"] [ tbody [] [ tr [] ch ] ]

        let finish<'msg> = td[] []

    let ofC4b (c : C4b) = sprintf "rgb(%i,%i,%i)" c.R c.G c.B

    let table rows = table [clazz "ui table unstackable"] [ tbody [] rows ]

    let row k v = tr [] [ td [clazz "collapsing"] [text k]; td [clazz "right aligned"] v ]

    let semui =
        [ 
            { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
            { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
        ]  

module TreeView =
    
    type Action<'id> = 
        | Click of 'id

    let view attribs children = Incremental.div (AttributeMap.ofList [clazz "ui list"]) children

    let leaf click content dragStartMsg dragStopMsg =
        let dragStart = onEvent "ondragstart" ["event.target.id"] (fun xs -> printfn "start: %A" xs; dragStartMsg)
        let dragOver = js "ondragover" "event.preventDefault()"
        let dragStop = onEvent "ondrop" ["event.target.id"] (fun xs -> printfn "stop %A" xs; dragStartMsg)
        div [ clazz "item"; onMouseClick (fun _ -> click ()); dragStart; dragOver; dragStop; "draggable" => "true" ] [
            i [ clazz "file icon" ] []
            Incremental.div (AttributeMap.ofList [clazz "content" ]) content
        ]

    let node (isExpanded : IMod<bool>) (clickMsg : unit -> 'a) header description children =
        let itemAttributes =
            amap {
                yield onMouseClick (fun _ -> clickMsg ())
                let! selected = isExpanded
                if selected then yield clazz "icon large outline open folder"
                else             yield clazz "icon large outline folder"
            } |> AttributeMap.ofAMap

        let childrenAttribs =
            amap {
                yield clazz "list"
                let! isExpanded = isExpanded
                if isExpanded then yield style "visible"
                else yield style "hidden"
            }

        div [ clazz "item" ] [
             Incremental.i itemAttributes AList.empty
             div [ clazz "content" ] [
                 div [ clazz "header"] [header]
                 div [ clazz "description noselect"] [description]
                 Incremental.div (AttributeMap.ofAMap childrenAttribs) 
                    <| alist { 
                        let! isExpanded = isExpanded
                        if isExpanded then yield! children
                    }
             ]
        ]



module TreeViewApp =
    
    open TreeView

    type Action =
        | Click of list<Index>
        | ToggleExpand of list<Index>
        | AddChild of list<Index>
        | RemChild of list<Index>
        | Nop

    let click v () = Click v
    let toggle v () = ToggleExpand v
    let addChild v () = AddChild v
    let remChild v () = RemChild v

    let defaultP = { isExpanded = true; isSelected = false; isActive = false }

    let init =
        { data =
            Tree.node 0 defaultP <| PList.ofList [ 
                Leaf 1
                Leaf 2 
                Tree.node 3 defaultP <| PList.ofList [
                    yield Leaf 4
                    yield Leaf 5 
                ]
            ] 
        }

    let updateAt (p : list<Index>) (f : Tree -> Tree) (t : Tree) =
        let rec go (p : list<Index>) (t : Tree)  =
            match p with
                | [] -> f t
                | x::rest -> 
                    match t with
                        | Leaf _ -> t
                        | Node(l,p,xs) -> 
                            match PList.tryGet x xs with
                                | Some c -> Node(l,p, PList.set x (go rest c) xs)
                                | None   -> t
        go (List.rev p) t

    let update (model : TreeModel) action =
        printfn "action: %A" action
        match action with
            | Click p -> 
                { model with
                    data = updateAt p (function | Leaf v -> Leaf (v + 1) | p -> p) model.data
                }
            | ToggleExpand p -> 
                { model with
                    data = 
                        updateAt p (
                            function | Leaf v -> Leaf v
                                     | Node(l,p,xs) -> 
                                         Node(l, { p with isExpanded = not p.isExpanded}, xs)
                        ) model.data
                }
            | AddChild p -> 
                { model with
                    data = updateAt p (
                             function | Leaf v -> Leaf v
                                      | Node(l,p,xs) -> 
                                         Node(l,p, PList.append (Leaf (PList.count xs + 1)) xs)
                           ) model.data
                }
            | RemChild p -> 
                { model with
                    data = updateAt p (
                             function | Leaf v -> Leaf v
                                      | Node(l,p,xs) -> 
                                          Node(l,p, if PList.count xs > 0 then PList.removeAt 0 xs else xs)
                           ) model.data
                }
            | Nop -> model

    let viewLabel v = v |> Mod.map string |> Incremental.text

    let rec viewTree path (model : IMod<MTree>) =
        alist {
            let! model = model
            match model with
            | MLeaf v -> 
                yield TreeView.leaf (click path) (AList.ofList [viewLabel v]) Nop Nop
            | MNode(s, p, xs) -> 
                let children = AList.collecti (fun i v -> viewTree (i::path) v) xs
                let desc =
                    div [] [
                         i [ clazz "plus icon";  onClick (addChild path) ] []
                         i [ clazz "minus icon"; onClick (remChild path) ] []
                    ]
                yield TreeView.node p.isExpanded (toggle path) 
                                    (viewLabel s) desc
                                    children
        }

    let view (model : MTreeModel) =
        require Html.semui (
            TreeView.view [] (viewTree [] model.data)
        )

    let app =
        {
            unpersist =  Unpersist.instance
            threads = fun _ -> ThreadPool.create()
            initial = init
            update = update
            view = view 
        }

    let start () =
        App.start app