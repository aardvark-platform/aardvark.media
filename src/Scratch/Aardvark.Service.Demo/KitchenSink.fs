namespace Viewer

open Aardvark.Service

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Rendering.Text
open Demo.TestApp
open Demo.TestApp.Mutable
open System.Threading


module KitchenSinkApp =

    type Message =
        | TreeMsg of list<Index> * int
        | ResetTree
        | RandomTree
        | ClearTree
        | KillNode of list<Index>
        | AddButton of Index * string
        | Hugo of list<string>
        | ToggleD3Hate
        | Enter 
        | Exit
        | Scale
        | ResetScale

        | StartDrag
        | StopDrag
        | MoveRay of Option<float> * RayPart
        | NewTime 

    let mutable currentId = 0
    let rand = RandomSystem()
    let rec randomNode(d : int) =
        let cnt = if d > 1 then rand.UniformInt(6) else 0
        let id = Interlocked.Increment(&currentId)
        let children = List.init cnt (fun i -> randomNode (d - 1))
        
        {
            content = id
            children = IndexList.ofList children
        }

    let randomTree (d : int) =
        currentId <- 0
        let cnt = 3
        let children = List.init cnt (fun i -> randomNode (d - 1))
        
        {
            nodes = IndexList.ofList children
        }



    let initial =
        {
            lastName = None
            elements = IndexList.ofList ["A"; "B"; "C"]
            hasD3Hate = true
            boxHovered = false
            boxScale = 1.0
            dragging = false
            lastTime = MicroTime.Now
            objects = HashMap.empty
            tree = randomTree 3
        }

    module Tree = 
        let remove (index : list<Index>) (tree : Tree<'a>) =
            let rec remNode (index : list<Index>) (n : TreeNode<'a>) =
                match index with
                    | [] -> None
                    | h :: index ->
                        match IndexList.tryGet h n.children with
                            | Some c -> 
                                match remNode index c with
                                    | Some c -> Some { n with children = IndexList.set h c n.children }
                                    | None -> Some { n with children = IndexList.remove h n.children }
                            | None ->
                                Some n

            match index with
                | h :: index ->
                    match IndexList.tryGet h tree.nodes with
                        | Some c -> 
                            match remNode index c with
                                | Some c -> { nodes = IndexList.set h c tree.nodes }
                                | None -> { nodes = IndexList.remove h tree.nodes }
                        | _ ->
                            tree
                | _ ->
                    tree

        let update (index : list<Index>) (f : 'a -> 'a) (tree : Tree<'a>) =
            let rec traverse (index : list<Index>) (n : TreeNode<'a>) =
                match index with
                    | [] -> Some { n with content = f n.content }
                    | h :: index ->
                        match IndexList.tryGet h n.children with
                            | Some c -> 
                                match traverse index c with
                                    | Some c -> Some { n with children = IndexList.set h c n.children }
                                    | None -> None
                            | None ->
                                None

            match index with
                | h :: index ->
                    match IndexList.tryGet h tree.nodes with
                        | Some c -> 
                            match traverse index c with
                                | Some c -> { nodes = IndexList.set h c tree.nodes }
                                | None -> tree
                        | _ ->
                            tree
                | _ ->
                    tree

    let update (m : Model) (msg : Message) =
        match msg with
            | TreeMsg(index, msg) ->
                { m with tree = Tree.update index (fun v -> v + msg) m.tree }

            | RandomTree ->
                { m with tree = randomTree 3 }
            | ResetTree ->
                { m with tree = initial.tree }

            | ClearTree ->
                { m with tree = { nodes = IndexList.empty } }

            | KillNode index ->
                { m with tree = Tree.remove index m.tree }
            | AddButton(before, str) -> 
                { m with lastName = Some str; elements = IndexList.remove before m.elements }
            | Hugo l ->
                let res : list<string> = l |> List.map Pickler.json.UnPickleOfString
                printfn "%A" res
                m
            | ToggleD3Hate ->
                { m with hasD3Hate = not m.hasD3Hate }
            | Enter ->
                { m with boxHovered = true }
            | Exit ->
                { m with boxHovered = false }
            | Scale ->
                { m with boxScale = 1.3 * m.boxScale } 
            | ResetScale ->
                { m with boxScale = 1.0 }

            | StartDrag ->
                printfn "start"
                { m with dragging = true }

            | StopDrag ->
                printfn "stop"
                { m with dragging = false }

            | NewTime ->
                let now = MicroTime.Now
                { m with lastName = Some (string (now - m.lastTime)); lastTime = now }

            | MoveRay(t,r) ->
                match t with
                    | Some t ->
                        { m with lastName = Some (string t) }
                    | None ->
                        { m with lastName = None }// Some "no hit" }

    let clazz str = attribute "class" str

    let semui =
        [ 
            { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
            { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
        ]

    let treeView (view : list<Index> -> 'a -> DomNode<'msg>) (t : MTree<'a, _>) =

        let rec nodeView (path : list<Index>) (n : MTreeNode<'a, _>) =
            li [] [
                view path n.content
                Incremental.ul AttributeMap.empty (AList.mapi (fun i n -> nodeView (path @ [i]) n) n.children)
            ]

        Incremental.ul
            AttributeMap.empty
            (AList.mapi (fun i n -> nodeView [i] n) t.nodes)

    let view (m : MModel) =
        let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        let cam = AVal.constant (Camera.create view frustum)

        div [attribute "style" "display: flex; flex-direction: row; width: 100%; height: 100%; border: 0; padding: 0; margin: 0"] [

            require semui (
                div [ attribute "class" "ui visible sidebar inverted vertical menu"; attribute "style" "min-width: 250px" ] [
                    div [ clazz "item"] [ 
                        b [] [text "Instructions"]
                        div [ clazz "menu" ] [
                            span [clazz "item"] [text "use typical WASD bindings to navigate"]
                            span [clazz "item"] [text "hover the box to see its highlighting"]
                            span [clazz "item"] [text "double click the box to persistently enlarge it"]
                        ]
                    ]

                    div [ clazz "item"] [ 
                        b [] [text "Actions"]
                        div [ clazz "menu" ] [
                            div [ clazz "item "] [ 
                                button [clazz "ui button tiny"; onClick (fun () -> Scale) ] [text "increase scale"]
                            ]
                            div [ clazz "item "] [ 
                                button [ clazz "ui button tiny"; onClick (fun () -> ResetScale) ] [text "reset scale"]
                            ]
                        ]
                    ]
                    
                    div [ clazz "item"] [ 
                        b [] [text "Status"]
                        div [ clazz "menu" ] [
                            span [clazz "item"; attribute "style" "color: red; font-weight: bold"] [ 
                                m.boxHovered |> AVal.map (function true -> "hovered" | false -> "not hovered") |> Incremental.text 
                            ]
                            
                            div [clazz "item"] [
                                span [] [text "current scale: "]
                                span [attribute "style" "color: red; font-weight: bold"] [ m.boxScale |> AVal.map string |> Incremental.text ]
                            ]

                            div [clazz "item"] [
                                button [clazz "ui green button mini"; onClick (fun () -> ResetTree)] [text "Reset"]
                                button [clazz "ui yellow button mini"; onClick (fun () -> RandomTree)] [text "Random"]
                                button [clazz "ui red button mini"; onClick (fun () -> ClearTree)] [text "Clear"]
                                br []
                                m.tree |> treeView (fun i v -> 
                                    button 
                                        [
                                            clazz "ui black button mini"
                                            onMouseUp (fun b _ -> 
                                                match b with
                                                    | MouseButtons.Left -> TreeMsg(i, 1)
                                                    | MouseButtons.Middle ->KillNode i
                                                    | MouseButtons.Right -> TreeMsg(i, -1)
                                                    | _ -> TreeMsg(i, 0)
                                            )

                                            js "oncontextmenu" "event.preventDefault();"
                                        ] 
                                        [
                                            Incremental.text (AVal.map (sprintf "Node%d") v)
                                        ]
                                )
                            ]
                        ]
                    ]
                ]
            )

            Incremental.renderControl
                cam
                (
                    AttributeMap.ofListCond [
                        always (style "display: flex; width: 100%; height: 100%")
                    ] 
                )
                (
                    let value = m.lastName |> AVal.map (function Some str -> str | None -> "yeah")

                    let baseBox = Box3d(-V3d.III, V3d.III)
                    let box = m.boxHovered |> AVal.map (fun h -> if h then baseBox.ScaledFromCenterBy(1.3) else baseBox)
                    let color = m.boxHovered |> AVal.map (fun h -> if h then C4b.Red else C4b.Green)

                    let box =
                        Sg.box color box
                            |> Sg.shader {
                                do! DefaultSurfaces.trafo
                                do! DefaultSurfaces.vertexColor
                                do! DefaultSurfaces.simpleLighting
                               }
                            |> Sg.noEvents
                            |> Sg.pickable (Box baseBox)        
                                 

                    let sg = 
                        box |> Sg.trafo (m.boxScale |> AVal.map Trafo3d.Scale)
                            |> Sg.withEvents [
                                Sg.onEnter (fun p -> Enter)
                                Sg.onLeave (fun () -> Exit)
                                Sg.onDoubleClick (fun _ -> Scale)
                                Sg.onMouseDown (fun _ _ -> StartDrag)
                            ]

                    Sg.ofList [
                        sg

                        Sg.markdown MarkdownConfig.light (m.lastName |> AVal.map (Option.defaultValue "yeah"))
                            |> Sg.noEvents
                            |> Sg.transform (Trafo3d.FromOrthoNormalBasis(-V3d.IOO, V3d.OOI, V3d.OIO))
                            |> Sg.translate 0.0 0.0 3.0
                    ]
                    |> Sg.Incremental.withGlobalEvents (
                        AMap.ofList [
                            Global.onMouseMove (fun e -> MoveRay(Some 0.0, e.evtRay))
                            Global.onMouseUp(fun e -> StopDrag)
                        ]
                    )


            )

        ]

    let rec timerThread() =
        proclist {
            do! Proc.Sleep 10
            yield NewTime
            yield! timerThread()
        }

    let pool = ThreadPool.empty |> ThreadPool.add "timer" (timerThread())

    let app =
        {
            unpersist = Unpersist.instance
            threads = fun _ -> pool
            view = view
            update = update
            initial = initial
        }

    let start () =
        app |> App.start