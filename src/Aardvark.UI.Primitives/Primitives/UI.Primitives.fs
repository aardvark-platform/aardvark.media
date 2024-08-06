namespace Aardvark.UI.Primitives

open System

open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.UI
open Aardvark.UI.Operators
open Aardvark.UI.Primitives

type NumericInputType = Slider | InputBox

module Html =

    module Layout =
        let boxH ch = td [clazz "collapsing"; style "padding: 0px 5px 0px 0px"] ch

        let horizontal ch = table [clazz "ui table"; style "backgroundColor: transparent"] [ tbody [] [ tr [] ch ] ]

    type ColorConverter =
        static member inline ToHtml(c : C3b) = $"rgb({c.R},{c.G},{c.B})"
        static member inline ToHtml(c : C3b, a : float) = if a < 1.0 then $"rgba({c.R},{c.G},{c.B},{string a})" else ColorConverter.ToHtml c
        static member inline ToHtml(c : C3b, a : float32) = if a < 1.0f then $"rgba({c.R},{c.G},{c.B},{string a})" else ColorConverter.ToHtml c
        static member inline ToHtml(c : C4b) = ColorConverter.ToHtml(c.RGB, Col.ByteToFloat c.A)

        static member inline ToHtml(c : C3us) = ColorConverter.ToHtml(c3b c)
        static member inline ToHtml(c : C4us) = ColorConverter.ToHtml(c3b c, Col.UShortToDouble c.A)

        static member inline ToHtml(c : C3ui) = ColorConverter.ToHtml(c3b c)
        static member inline ToHtml(c : C4ui) = ColorConverter.ToHtml(c3b c, Col.UIntToDouble c.A)

        static member inline ToHtml(c : C3f) = ColorConverter.ToHtml(c3b c)
        static member inline ToHtml(c : C4f) = ColorConverter.ToHtml(c3b c, c.A)

        static member inline ToHtml(c : C3d) = ColorConverter.ToHtml(c3b c)
        static member inline ToHtml(c : C4d) = ColorConverter.ToHtml(c3b c, c.A)

    let inline private colorAux (_ : ^Converter) (color : ^Color) : string =
        ((^Converter or ^Color) : (static member ToHtml : ^Color -> string) color)

    /// Converts the given color to an rgb() or rgba() string.
    let inline color (color : ^Color) : string =
        colorAux Unchecked.defaultof<ColorConverter> color

    /// Converts the given color to an rgb() or rgba() string.
    [<Obsolete("Use Html.color instead.")>]
    let ofC4b (c : C4b) = color c

    let table rows = table [clazz "ui celled striped inverted table unstackable"] [ tbody [] rows ]

    let row k v = tr [] [ td [clazz "collapsing"] [text k]; td [clazz "right aligned"] v ]

    /// Sets the (top.)document.title property adaptively.
    let title (top : bool) (title : aval<string>) (node : DomNode<'msg>) =
        let doc = if top then "top.document" else "document";
        let code = "chDocumentTitle.onmessage = function (title) { " + doc + ".title = title };"
        onBoot' ["chDocumentTitle", AVal.channel title] code node

    let semui =
        SimplePrimitives.Html.semui

    let multiselectList (entries : list<'a>) (getId : 'a -> string) (getDomNode : 'a -> DomNode<'msg>) (getValue : string -> 'a) (onSelected : list<'a> -> 'msg) =
        div [attribute "style" "width:100%"] [
            select [
                attribute "style" "width:100%"
                attribute "multiple" ""
                onEvent "onchange" ["Array.prototype.slice.call(event.target.selectedOptions).map(x => x.value)"]
                    (fun xs ->
                        let s = (xs |> Seq.head)

                        //shame
                        let vals = s.Substring(1,s.Length-1).Split([|','|])
                                    |> Array.map ( fun v -> v.Replace("\"","").Replace("[","").Replace("]","").Trim())
                                    |> Array.toList

                        vals |> List.map getValue |> onSelected
                    )
            ] (entries |> List.map ( fun s ->
                option [attribute "value" (getId s)] [getDomNode s]
            ))
        ]

    let multiselectListSimple (entries : list<string>) (onSelected : list<string> -> 'msg) =
        multiselectList entries id text id onSelected

    module SemUi =

        let menu (c : string )(entries : list<string * list<DomNode<'msg>>>) =
            div [ clazz c ] (
                entries |> List.map (fun (name, children) ->
                    div [ clazz "item"] [
                        b [] [text name]
                        div [ clazz "menu" ] (
                            children |> List.map (fun c ->
                                div [clazz "item"] [c]
                            )
                        )
                    ]
                )
            )

        let adornerMenu (sectionsAndItems : list<string * list<DomNode<'msg>>>) (rest : list<DomNode<'msg>>) =
            let pushButton() =
                div [
                    clazz "ui black big launch right attached fixed button menubutton"
                    js "onclick"        "$('.sidebar').sidebar('toggle');"
                    style "z-index:1"
                ] [
                    i [clazz "content icon"] []
                    span [clazz "text"] [text "Menu"]
                ]
            [
                yield
                    div [clazz "pusher"] [
                        yield pushButton()
                        yield! rest
                    ]
                yield
                    menu "ui vertical inverted sidebar menu" sectionsAndItems
            ]

        let stuffStack (ls) =
            div [clazz "ui inverted segment"] [
                div [clazz "ui inverted relaxed divided list"] [
                    for l in ls do
                        yield
                            div [clazz "item"] [
                                div [clazz "content"] [
                                    l
                                ]
                            ]
                ]
            ]

        open Microsoft.FSharp.Reflection
        let private fields r =
            try
                let t = r.GetType()
                let props = t.GetProperties()
                let vals = FSharpValue.GetRecordFields(r)
                [
                    for i in 0..props.Length-1 do
                        yield props.[i].Name, if props.[i].PropertyType = typeof<System.Double> then sprintf "%.2f" (vals.[i] :?> System.Double) else string vals.[i]
                ]
            with e -> []

        let recordPrint record =
            div [clazz "ui label"] [
                for (n,v) in fields record do
                    yield text (sprintf "%s: %s" n v)
            ]

        let accordion text' icon active content' =
            let title = if active then "title active inverted" else "title inverted"
            let content = if active then "content active" else "content"

            onBoot "$('#__ID__').accordion();" (
                div [clazz "ui inverted segment"] [
                    div [clazz "ui inverted accordion fluid"] [
                        div [clazz title] [
                                i [clazz (icon + " icon circular")] []
                                text text'
                                //Static.a [clazz "ui label"] [
                                //    i [clazz (icon + " icon circular inverted")] []
                                //    text text'
                                //]
                        ]
                        div [clazz content] content'
                    ]
                ]
            )

        let dropDown<'a, 'msg when 'a : enum<int> and 'a : equality> (selected : aval<'a>) (change : 'a -> 'msg) =
            let names = Enum.GetNames(typeof<'a>)
            let values = Enum.GetValues(typeof<'a>) |> unbox<'a[]>
            let nv = Array.zip names values

            let attributes (name : string) (value : 'a) =
                AttributeMap.ofListCond [
                    always (attribute "value" name)
                    onlyWhen (AVal.map ((=) value) selected) (attribute "selected" "selected")
                ]

            select [onChange (fun str -> Enum.Parse(typeof<'a>, str) |> unbox<'a> |> change); style "color:black"] [
                for (name, value) in nv do
                    let att = attributes name value
                    yield Incremental.option att (AList.ofList [text name])
            ]

        //Html.row "CullMode:" [Html.SemUi.dropDown model.cullMode SetCullMode]
        let dropDown' (values : alist<'a>)(selected : aval<'a>) (change : 'a -> 'msg) (f : 'a ->string)  =

            let attributes (name : string) =
                AttributeMap.ofListCond [
                    always (attribute "value" (name))
                    onlyWhen (selected |> AVal.map (fun x -> f x = name)
                                            //fun x ->
                                            //    match x with
                                            //        | Some s -> (f s) = name
                                            //        | None -> false)
                             ) (attribute "selected" "selected")
                ]

            let ortisOnChange  =
                let cb (i : int) =
                    let currentState = values.Content |> AVal.force
                    match IndexList.tryAt i currentState with
                        | None -> failwith ""
                        | Some a -> change a
                onEvent "onchange" ["event.target.selectedIndex"] (fun x -> x |> List.head |> Int32.Parse |> cb)

            Incremental.select (AttributeMap.ofList [ortisOnChange; style "color:black"])
                (values
                    |> AList.mapi(fun i x -> Incremental.option (attributes (f x)) (AList.ofList [text (f x)]))
                )

        let textBox (text : aval<string>) (set : string -> 'msg) =

            let attributes =
                amap {
                    yield "type" => "text"
                    yield onChange set
                    let! t = text
                    yield "value" => t
                }

            Incremental.input (AttributeMap.ofAMap attributes)

        let toggleBox (state : aval<bool>) (toggle : 'msg) =

            let attributes =
                amap {
                     yield "type" => "checkbox"
                     yield onChange (fun _ -> toggle)

                     let! check = state
                     if check then
                        yield "checked" => ""
                }

      //      div [clazz "ui toggle checkbox"] [
            Incremental.input (AttributeMap.ofAMap attributes)

        let toggleImage (state : aval<bool>) (toggle : unit -> 'msg) = 0

        let tabbed attr content active =
            onBoot "$('.menu .item').tab();" (
                div attr [
                    yield div [clazz "ui inverted segment top attached tabular menu"] [
                            for (name,ch) in content do
                                let active = if name = active then "inverted item active" else "inverted item"
                                yield Static.a [clazz active; attribute "data-tab" name] [text name]
                          ]

                    for (name,ch) in content do
                        let classAttr = "ui inverted bottom attached tab segment"
                        let active = if name = active then (sprintf "%s %s" classAttr "active") else classAttr
                        yield div [clazz active; attribute "data-tab" name] [ch]
                ]
            )

        let iconToggle (state : aval<bool>) onIcon offIcon action =
          let toggleIcon = state |> AVal.map(fun isOn -> if isOn then onIcon else offIcon)

          let attributes =
            amap {
                let! icon = toggleIcon
                yield clazz icon
                yield onClick (fun _ -> action)
            } |> AttributeMap.ofAMap

          Incremental.i attributes AList.empty

        let iconCheckBox (dings : aval<bool>) action =
          iconToggle dings "check square outline icon" "square icon" action

    module IO =

        let fileDialog action =
            [
                onEvent "onchoose" [] (List.head >> Aardvark.UI.Pickler.unpickleOfJson >> List.head >> action)
                clientEvent "onclick" ("aardvark.openFileDialog({ allowMultiple: true, mode: 'file' }, function(files) { if(files != undefined) aardvark.processEvent('__ID__', 'onchoose', files); });")
            ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Choice =
    type Model = Red=0 | Yellow=1 | Blue=2

    type Action = Select of Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Numeric =

    type Action =
        | SetValue of float
        | SetMin of float
        | SetMax of float
        | SetStep of float
        | SetFormat of string

    let update (model : NumericInput) (action : Action) =
        match action with
        | SetValue v -> { model with value = v }
        | SetMin v ->   { model with min = v }
        | SetMax v ->   { model with max = v }
        | SetStep v ->  { model with step = v }
        | SetFormat s -> { model with format = s }

    let formatNumber (format : string) (value : float) =
        String.Format(Globalization.CultureInfo.InvariantCulture, format, value)

    let numericField<'msg> ( f : Action -> seq<'msg> ) ( atts : AttributeMap<'msg> ) ( model : AdaptiveNumericInput ) inputType =

        let tryParseAndClamp min max fallback s =
            let parsed = 0.0
            match Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                | (true,v) -> clamp min max v
                | _ ->  printfn "validation failed: %s" s
                        fallback

        let onWheel' (f : Aardvark.Base.V2d -> seq<'msg>) =
            let serverClick (args : list<string>) : Aardvark.Base.V2d =
                let delta = List.head args |> Pickler.unpickleOfJson
                delta  / Aardvark.Base.V2d(-100.0,-100.0) // up is down in mouse wheel events

            onEvent' "onwheel" ["{ X: event.deltaX.toFixed(10), Y: event.deltaY.toFixed(10)  }"] (serverClick >> f)

        let attributes =
            amap {
                yield style "text-align:right; color : black"

                let! min = model.min
                let! max = model.max
                let! value = model.value
                match inputType with
                    | Slider ->
                        yield "type" => "range"
                        yield onInput' (tryParseAndClamp min max value >> SetValue >> f)   // continous updates for slider
                    | InputBox ->
                        yield "type" => "number"
                        yield onChange' (tryParseAndClamp min max value >> SetValue >> f)  // batch updates for input box (to let user type)

                let! step = model.step
                yield onWheel' (fun d -> value + d.Y * step |> clamp min max |> SetValue |> f)

                yield "step" => sprintf "%f" step
                yield "min"  => sprintf "%f" min
                yield "max"  => sprintf "%f" max

                let! format = model.format
                yield "value" => formatNumber format value
            }

        Incremental.input (AttributeMap.ofAMap attributes |> AttributeMap.union atts)

    let numericField' = numericField (Seq.singleton) AttributeMap.empty

    let view' (inputTypes : list<NumericInputType>) (model : AdaptiveNumericInput) : DomNode<Action> =
        inputTypes
            |> List.map (numericField' model)
            |> List.intersperse (text " ")
            |> div []

    let view (model : AdaptiveNumericInput) =
        view' [InputBox] model

    let init = {
        value   = 0.0
        min     = 0.0
        max     = 10.0
        step    = 0.20
        format  = "{0:0.00}"
    }

    let app' inputTypes =
        {
            unpersist = Unpersist.instance
            threads = fun _ -> ThreadPool.empty
            initial = init
            update = update
            view = view' inputTypes
        }

    let app () = app' [NumericInputType.InputBox; NumericInputType.InputBox; NumericInputType.Slider]

    let start () =
        app () |> App.start


module Vector3d =

    type Action =
        | SetX of Numeric.Action
        | SetY of Numeric.Action
        | SetZ of Numeric.Action
        | SetXYZ of Numeric.Action * Numeric.Action * Numeric.Action

    let update (model : V3dInput) (action : Action) =
        match action with
            | SetX a ->
                let x = Numeric.update model.x a
                {
                    model with
                        x = x
                        value = V3d(x.value, model.value.Y, model.value.Z)
                }
            | SetY a ->
                let y = Numeric.update model.y a
                {
                    model with
                        y = y
                        value = V3d(model.value.X, y.value, model.value.Z)
                }
            | SetZ a ->
                let z = Numeric.update model.z a
                {
                    model with
                        z = z
                        value = V3d(model.value.X, model.value.Y, z.value)
                }
            | SetXYZ (a,b,c) ->
                let x = Numeric.update model.x a
                let y = Numeric.update model.y b
                let z = Numeric.update model.z c
                {
                    model with
                        x = x
                        y = y
                        z = z
                        value = V3d(x.value, y.value, z.value)
                }

    let view (model : AdaptiveV3dInput) =

        Html.table [
            Html.row "X" [Numeric.view' [InputBox] model.x |> UI.map SetX]
            Html.row "Y" [Numeric.view' [InputBox] model.y |> UI.map SetY]
            Html.row "Z" [Numeric.view' [InputBox] model.z |> UI.map SetZ]
        ]

    let init =
        let x = Numeric.init
        let y = Numeric.init
        let z = Numeric.init

        {
            x = x
            y = y
            z = z
            value = V3d(x.value,y.value,z.value)
        }

    let initV3d (v : V3d) = {
        x = { Numeric.init with value = v.X }
        y = { Numeric.init with value = v.Y }
        z = { Numeric.init with value = v.Z }
        value = v
    }

    let updateV3d (model : V3dInput) (v : V3d) = {
        x = { model.x with value = v.X }
        y = { model.y with value = v.Y }
        z = { model.z with value = v.Z }
        value = v
    }

    let app : App<V3dInput, AdaptiveV3dInput, Action> =
        {
            unpersist = Unpersist.instance
            threads = fun _ -> ThreadPool.empty
            initial = init
            update = update
            view = view
        }

    let start () =
        app |> App.start

module TreeView =

    type Action<'id> =
        | Click of 'id

    let view attribs children = Incremental.div (AttributeMap.ofList [clazz "ui list"]) children

    let leaf click content dragStartMsg dragStopMsg =
        let dragStart = onEvent "ondragstart" ["event.target.id"] (fun xs -> printfn "start: %A" xs; dragStartMsg)
        let dragOver = js "ondragover" "console.warn('urdar'); event.preventDefault()"
        let dragStop = js "ondrop" "console.warn('bla'); event.preventDefault()" //onEvent "ondrop" ["event.preventDefault(); event.target.id"] (fun xs -> printfn "stop %A" xs; dragStartMsg)
        div [ clazz "item"; onMouseClick (fun _ -> click ()); dragStart; dragOver; dragStop; "draggable" => "true"] [
            i [ clazz "file icon";  ] []
            Incremental.div (AttributeMap.ofList [clazz "content" ]) content
        ]

    let node (isExpanded : aval<bool>) (clickMsg : unit -> 'a) header description (children : alist<_>) =
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
            Tree.node (LeafValue.Text "0") defaultP <| IndexList.ofList [
                Leaf (LeafValue.Number 1)
                Leaf (LeafValue.Text "2" )
                Tree.node (LeafValue.Number 3) defaultP <| IndexList.ofList [
                    yield Leaf (LeafValue.Number 4)
                    yield Leaf (LeafValue.Number 5)
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
                            match IndexList.tryGet x xs with
                                | Some c -> Node(l,p, IndexList.set x (go rest c) xs)
                                | None   -> t
        go (List.rev p) t

    let update (model : TreeModel) action =
        printfn "action: %A" action
        match action with
            | Click p ->
                { model with
                    data = updateAt p (function | Leaf v ->( match v with
                                                                | LeafValue.Number n -> Leaf ( LeafValue.Number (n + 1))
                                                                | LeafValue.Text t -> Leaf ( LeafValue.Text (sprintf "%s a" t)))
                                                | p -> p) model.data
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
                                            let value = match l with
                                                           | Number n -> Number (IndexList.count xs + 1)
                                                           | Text   t -> LeafValue.Text t
                                            Node(l,p, IndexList.add (Leaf value) xs)
                           ) model.data
                }
            | RemChild p ->
                { model with
                    data = updateAt p (
                             function | Leaf v -> Leaf v
                                      | Node(l,p,xs) ->
                                          Node(l,p, if IndexList.count xs > 0 then IndexList.removeAt 0 xs else xs)
                           ) model.data
                }
            | Nop -> model

    let viewLabel v =
        v
        |> AVal.bind (fun u ->
            match u with
            | AdaptiveNumber n -> n |> AVal.map (fun x -> sprintf "Number %A" (string x))
            | AdaptiveText t   -> t |> AVal.map (fun x -> sprintf "Text %A" x))
        |> Incremental.text


    let rec viewTree path (model : AdaptiveTreeCase) =
        alist {
            //let! model = model
            match model with
            | AdaptiveLeaf v ->
                yield TreeView.leaf (click path) (AList.ofList [viewLabel v]) Nop Nop
            | AdaptiveNode(s, p, xs) ->
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

    let view (model : AdaptiveTreeModel) =
        require Html.semui (
            TreeView.view [] (model.data |> AList.bind (viewTree []))
        )

    let app =
        {
            unpersist =  Unpersist.instance
            threads = fun _ -> ThreadPool.empty
            initial = init
            update = update
            view = view
        }

    let start () =
        app |> App.start