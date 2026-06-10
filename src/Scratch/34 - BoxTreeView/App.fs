module BoxTreeView.App

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Golden

open BoxTreeView.Model
open TreeView.Model
open TreeView.App
open VirtualTree.Model
open VirtualTree.App
open VirtualTree.Utilities

// ---------------------------------------------------------------------------
// Scene helpers
// ---------------------------------------------------------------------------

let private makeBox name (pos : V3d) (color : C4b) =
    { id       = Guid.NewGuid().ToString("N").[..7]
      name     = name
      geometry = Box3d.FromCenterAndSize(pos, V3d.III * 0.8)
      color    = color }

/// Pastel cube colour palette (matches "11 - BoxSelection" example).
let private pastelColors =
    [| C4b(166, 206, 227)
       C4b(178, 223, 138)
       C4b(251, 154, 153)
       C4b(253, 191, 111)
       C4b(202, 178, 214) |]

/// Highlight colours used for selection / hover in the 3D view.
let private selectedColor = C4b(255, 0, 0, 255)
let private hoveredColor  = C4b(0, 0, 255, 255)

let private mkColor (model : AdaptiveModel) (box : AdaptiveVisibleBox) : aval<C4b> =
    let id = box.id
    let isSelected =
        model.selectedBoxes
        |> ASet.toAVal
        |> AVal.map (HashSet.contains id)

    let isHovered =
        model.hoveredBox
        |> AVal.map (fun h -> h = Some id)

    AVal.map3 (fun sel hov baseCol ->
        if sel then selectedColor
        elif hov then hoveredColor
        else baseCol
    ) isSelected isHovered box.color

let private mkISg (model : AdaptiveModel) (box : AdaptiveVisibleBox) =
    let color = mkColor model box
    Sg.box color box.geometry
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
        do! DefaultSurfaces.simpleLighting
    }
    |> Sg.requirePicking
    |> Sg.noEvents
    |> Sg.withEvents [
        Sg.onClick       (fun _  -> Select   box.id)
        Sg.onDoubleClick (fun _  -> ScrollTo box.id)
        Sg.onEnter       (fun _  -> Hover (Some box.id))
        Sg.onLeave       (fun () -> Hover None)
    ]

// ---------------------------------------------------------------------------
// Path helper
// ---------------------------------------------------------------------------

let private buildPathStr (labels : HashMap<string, string>) (hierarchy : FlatTree<string>) (nodeId : string) =
    hierarchy |> FlatTree.rootPath nodeId
    |> Array.choose (fun id -> HashMap.tryFind id labels)
    |> String.concat " / "

let private treeSortKey (values : HashMap<string, TreeItemData>) (id : string) =
    match HashMap.tryFind id values with
    | Some data -> (if data.isGroup then 0 else 1), data.label.ToLowerInvariant()
    | None      -> (1, id.ToLowerInvariant())

/// Sorts the direct children of parentId in-place: groups first, then alphabetically.
let private sortChildrenInTree (values : HashMap<string, TreeItemData>) (parentId : string) (tree : FlatTree<string>) =
    match FlatTree.tryIndexOf parentId tree with
    | ValueNone -> tree
    | ValueSome parentIdx ->
        let childIndices = tree.ChildrenIndices parentIdx
        if childIndices.Length <= 1 then tree
        else
            let sortedChildren =
                childIndices
                |> Array.map  (fun i  -> tree.[i].Value)
                |> Array.sortBy (treeSortKey values)
            let childSubtrees = sortedChildren |> Array.map (fun id -> tree.SubTree id)
            let treeSingleton = tree.Replace(parentId, FlatTree.singleton parentId)
            (treeSingleton, childSubtrees)
            ||> Array.fold (fun t st -> t.InsertSubTree(parentId, st))

// ---------------------------------------------------------------------------
// Initial state
// ---------------------------------------------------------------------------

let private buildInitialScene () =
    let alpha   = makeBox "Alpha"   (V3d(-3.0,  2.0, 0.0)) pastelColors.[0]
    let beta    = makeBox "Beta"    (V3d(-3.0,  0.0, 0.0)) pastelColors.[1]
    let gamma   = makeBox "Gamma"   (V3d(-3.0, -2.0, 0.0)) pastelColors.[2]
    let delta   = makeBox "Delta"   (V3d( 0.0,  2.5, 0.0)) pastelColors.[3]
    let epsilon = makeBox "Epsilon" (V3d( 3.0,  1.5, 0.0)) pastelColors.[4]
    let zeta    = makeBox "Zeta"    (V3d( 3.0, -0.5, 0.0)) pastelColors.[0]
    let eta     = makeBox "Eta"     (V3d( 0.0, -0.5, 0.0)) pastelColors.[1]
    let theta   = makeBox "Theta"   (V3d( 0.0, -2.5, 0.0)) pastelColors.[2]

    let allBoxes = [ alpha; beta; gamma; delta; epsilon; zeta; eta; theta ]

    // Fixed group IDs
    let rootId   = "root"
    let grpRed   = "grp_red"
    let grpBlue  = "grp_blue"
    let grpSub   = "grp_sub"

    // Tree hierarchy: parent → children (by ID)
    let hierarchy =
        HashMap.ofList [
            rootId,  [ grpRed; grpBlue; eta.id; theta.id ]
            grpRed,  [ alpha.id; beta.id; gamma.id ]
            grpBlue, [ delta.id; grpSub ]
            grpSub,  [ epsilon.id; zeta.id ]
        ]

    // Tree display data (both groups and box leaves)
    let groupItems =
        [ rootId,  { label = "Scene";       isGroup = true; color = C4b.White }
          grpRed,  { label = "Red Group";   isGroup = true; color = alpha.color }
          grpBlue, { label = "Blue Group";  isGroup = true; color = delta.color }
          grpSub,  { label = "Sub Group C"; isGroup = true; color = epsilon.color } ]

    let boxItems =
        allBoxes |> List.map (fun b -> b.id, { label = b.name; isGroup = false; color = b.color })

    let treeValues  = HashMap.ofList (groupItems @ boxItems)

    let getChildren id =
        match HashMap.tryFind id hierarchy with
        | Some children ->
            children
            |> List.sortBy (treeSortKey treeValues)
            :> seq<string>
        | None -> Seq.empty

    let treeView    = TreeView.initialize getChildren treeValues rootId

    let boxIds      = allBoxes |> List.map (fun b -> b.id) |> HashSet.ofList
    let groupLabels = groupItems |> List.map (fun (id, data) -> id, data.label) |> HashMap.ofList

    IndexList.ofList allBoxes, treeView, boxIds, groupLabels

// ---------------------------------------------------------------------------
// Golden layout
// ---------------------------------------------------------------------------

let private layoutConfig = LayoutConfig.Default

let private defaultLayout =
    let leftStack =
        { stack {
            element { id "tree";    title "Scene Tree" }
            element { id "actions"; title "Actions" }
          } with Size = Size.Weight 3 }
    layout {
        row {
            leftStack
            element { id "render"; title "3D View"; weight 7 }
        }
    }

let private initialCamera = {
    FreeFlyController.initial with
        view = CameraView.lookAt (V3d(0.0, 0.0, 14.0)) V3d.OOO V3d.OIO
}

// ---------------------------------------------------------------------------
// Tree update helpers
// ---------------------------------------------------------------------------

/// Re-applies the collapsed singletons from vt.collapsed to a new hierarchy.
let private rebuildCurrent (collapsed : HashMap<string, FlatTree<string>>) (newH : FlatTree<string>) =
    (newH, collapsed)
    ||> HashMap.fold (fun t key _ ->
        if FlatTree.contains key t then
            t |> FlatTree.replace key (FlatTree.singleton key)
        else t
    )

/// Rebuilds the visibility array for a new hierarchy, preserving per-node state by value.
let private rebuildVisibility (oldH : FlatTree<string>) (oldVis : Visibility[]) (newH : FlatTree<string>) =
    let visMap =
        List.init oldH.Count (fun i -> oldH.[i].Value, oldVis.[i])
        |> HashMap.ofList
    Array.init newH.Count (fun i ->
        newH.[i].Value
        |> fun v -> HashMap.tryFind v visMap |> Option.defaultValue Visibility.Visible
    )

// ---------------------------------------------------------------------------
// Update
// ---------------------------------------------------------------------------

let update (model : Model) (msg : Message) =
    match msg with
    | Camera m ->
        { model with camera = FreeFlyController.update model.camera m }

    | Select id ->
        // Single click: select in 3D and sync tree highlight (no scroll)
        let treeModel =
            model.treeView
            |> TreeView.update (TreeView.Message.Click (id, { shift = false; alt = false; ctrl = false }))
        { model with selectedBoxes = HashSet.single id; treeView = treeModel }

    | ScrollTo id ->
        // Double click: also uncollapse ancestors and scroll the tree to this box
        let treeModel =
            model.treeView
            |> TreeView.update (TreeView.Message.Virtual (VirtualTree.Message.ScrollTo id))
        { model with treeView = treeModel }

    | ToggleCollapse id ->
        let msg =
            if HashMap.containsKey id model.treeView.tree.collapsed
            then TreeView.Message.Uncollapse id
            else TreeView.Message.Collapse  id
        { model with treeView = model.treeView |> TreeView.update msg }

    | Hover optId ->
        { model with hoveredBox = optId }

    | TreeAction msg ->
        let treeModel = model.treeView |> TreeView.update msg
        match msg with
        | TreeView.Message.Click (id, _) when HashSet.contains id model.boxIds ->
            { model with treeView = treeModel; selectedBoxes = HashSet.single id }
        | _ ->
            { model with treeView = treeModel }

    | GoldenLayout msg ->
        { model with golden = model.golden |> GoldenLayout.update msg }

    | MoveNode (nodeId, targetParentId) ->
        let vt = model.treeView.tree
        let h  = vt.hierarchy
        let valid =
            FlatTree.contains nodeId h &&
            FlatTree.contains targetParentId h &&
            not (FlatTree.isRoot nodeId h) &&
            not (h |> FlatTree.descendants nodeId |> Seq.contains targetParentId)
        if not valid then model
        else
            let subtree = h |> FlatTree.subTree nodeId
            let newH    =
                h
                |> FlatTree.delete nodeId
                |> FlatTree.insertSubTree targetParentId subtree
                |> sortChildrenInTree model.treeView.values targetParentId
            let newVT = { vt with hierarchy = newH; current = rebuildCurrent vt.collapsed newH }
            { model with treeView = { model.treeView with tree = newVT
                                                          visibility = rebuildVisibility h model.treeView.visibility newH } }

    | RemoveSelected ->
        let selected = model.treeView.selected
        if selected.Count <> 1 then model
        else
            let nodeId = selected |> Seq.head
            let vt = model.treeView.tree
            let h  = vt.hierarchy
            if not (FlatTree.contains nodeId h) || FlatTree.isRoot nodeId h then model
            else
                let removed     = h |> FlatTree.descendants nodeId |> Seq.toArray |> HashSet.ofArray
                let newH        = h |> FlatTree.delete nodeId
                let newCollapsed = vt.collapsed |> HashMap.filter (fun k _ -> FlatTree.contains k newH)
                let newVT       = { vt with hierarchy  = newH
                                            current    = rebuildCurrent newCollapsed newH
                                            collapsed  = newCollapsed }
                let newValues      = model.treeView.values |> HashMap.filter (fun k _ -> not (HashSet.contains k removed))
                let newGroupLabels = model.groupLabels     |> HashMap.filter (fun k _ -> not (HashSet.contains k removed))
                let newBoxes       = model.boxes           |> IndexList.filter (fun b  -> not (HashSet.contains b.id removed))
                let newBoxIds      = model.boxIds          |> Seq.filter (fun id -> not (HashSet.contains id removed)) |> HashSet.ofSeq
                let tv =
                    { model.treeView with
                        tree       = newVT
                        values     = newValues
                        visibility = rebuildVisibility h model.treeView.visibility newH
                        selected   = HashSet.empty
                        hovered    = ValueNone
                        lastClick  = ValueNone }
                { model with
                    boxes         = newBoxes
                    boxIds        = newBoxIds
                    groupLabels   = newGroupLabels
                    selectedBoxes = HashSet.empty
                    treeView      = tv }

    | AddFolder ->
        let selected = model.treeView.selected
        if selected.Count <> 1 then model
        else
            let parentId = selected |> Seq.head
            if HashSet.contains parentId model.boxIds then model
            else
                let newId    = "grp_" + Guid.NewGuid().ToString("N").[..5]
                let newLabel = $"New Group {model.groupLabels.Count}"
                let newData  = { label = newLabel; isGroup = true; color = C4b.White }
                let newValues = model.treeView.values |> HashMap.add newId newData
                let vt  = model.treeView.tree
                let oldH = vt.hierarchy
                let newH =
                    oldH
                    |> FlatTree.insert parentId newId
                    |> sortChildrenInTree newValues parentId
                let newVT = { vt with hierarchy = newH; current = rebuildCurrent vt.collapsed newH }
                let tv =
                    { model.treeView with
                        tree       = newVT
                        values     = newValues
                        visibility = rebuildVisibility oldH model.treeView.visibility newH }
                { model with groupLabels = model.groupLabels |> HashMap.add newId newLabel; treeView = tv }

    | AddCube ->
        let selected = model.treeView.selected
        if selected.Count <> 1 then model
        else
            let parentId = selected |> Seq.head
            if HashSet.contains parentId model.boxIds then model
            else
                let rng    = Random()
                let pos    = V3d(rng.NextDouble() * 8.0 - 4.0, rng.NextDouble() * 8.0 - 4.0, 0.0)
                let color  = pastelColors.[model.boxes.Count % pastelColors.Length]
                let newBox = makeBox $"New Cube {model.boxes.Count + 1}" pos color
                let newData  = { label = newBox.name; isGroup = false; color = newBox.color }
                let newValues = model.treeView.values |> HashMap.add newBox.id newData
                let vt  = model.treeView.tree
                let oldH = vt.hierarchy
                let newH =
                    oldH
                    |> FlatTree.insert parentId newBox.id
                    |> sortChildrenInTree newValues parentId
                let newVT = { vt with hierarchy = newH; current = rebuildCurrent vt.collapsed newH }
                let tv =
                    { model.treeView with
                        tree       = newVT
                        values     = newValues
                        visibility = rebuildVisibility oldH model.treeView.visibility newH }
                { model with
                    boxes    = IndexList.ofList (IndexList.toList model.boxes @ [newBox])
                    boxIds   = model.boxIds |> HashSet.add newBox.id
                    treeView = tv }

    | ResetScene ->
        let boxes, treeView, boxIds, groupLabels = buildInitialScene ()
        { model with
            boxes         = boxes
            boxIds        = boxIds
            groupLabels   = groupLabels
            treeView      = treeView
            selectedBoxes = HashSet.empty
            hoveredBox    = None }

    | RenameNode (nodeId, newLabel) ->
        let newLabel = newLabel.Trim()
        if String.IsNullOrEmpty newLabel then model
        else
            match HashMap.tryFind nodeId model.treeView.values with
            | None -> model
            | Some data ->
                let newValues      = model.treeView.values |> HashMap.add nodeId { data with label = newLabel }
                let newGroupLabels =
                    if HashMap.containsKey nodeId model.groupLabels
                    then model.groupLabels |> HashMap.add nodeId newLabel
                    else model.groupLabels
                let tv = { model.treeView with values = newValues }
                { model with treeView = tv; groupLabels = newGroupLabels }

// ---------------------------------------------------------------------------
// View helpers
// ---------------------------------------------------------------------------

let private moveDropdown (selectedId : string) (currentParent : string voption) (targets : (string * string) list) : DomNode<Message> =
    div [ clazz "ui simple dropdown button inverted mini" ] [
        i [ clazz "exchange alternate icon" ] []
        text " Move to "
        i [ clazz "dropdown icon" ] []
        div [ clazz "menu" ] (
            targets |> List.map (fun (targetId, path) ->
                let isCurrent = currentParent = ValueSome targetId
                let label = if isCurrent then $"[ {path} ]" else path
                div [ clazz "item"; onClick (fun _ -> MoveNode (selectedId, targetId)) ]
                    [ text label ]
            )
        )
    ]

let private sectionLabel (txt : string) =
    div [ style "color: #888; font-size: 10px; text-transform: uppercase; letter-spacing: 0.06em; margin-bottom: 5px" ]
        [ text txt ]

let private btn (icon : string) (label : string) (color : string) (msg : Message) =
    div [ clazz $"ui mini {color} inverted button"; onClick (fun _ -> msg) ]
        [ i [ clazz $"{icon} icon" ] []; text $" {label}" ]

let private btnDisabled (icon : string) (label : string) (color : string) =
    div [ clazz $"ui mini {color} inverted disabled button" ]
        [ i [ clazz $"{icon} icon" ] []; text $" {label}" ]

let private actionsPanel (model : AdaptiveModel) : DomNode<Message> =
    let labelAval =
        model.treeView.selected |> AVal.bind (fun sel ->
            if sel.Count = 1 then
                let id = sel |> Seq.head
                (model.treeView.values |> AMap.tryFind id) |> AVal.bind (fun maybeData ->
                    match maybeData with
                    | Some data -> data.label
                    | None      -> AVal.constant "")
            else
                AVal.constant ""
        )

    Incremental.div
        (AttributeMap.ofList [ style "padding: 12px 14px; display: flex; flex-direction: column; gap: 14px" ])
        (alist {
            let! selected  = model.treeView.selected
            let! hierarchy = model.treeView.tree.hierarchy
            let  boxIds    = model.boxIds
            let  labels    = model.groupLabels

            if selected.Count = 1 then
                let selectedId     = selected |> Seq.head
                let isSingleFolder = not (HashSet.contains selectedId boxIds)
                let canRemove      = not (FlatTree.isRoot selectedId hierarchy)

                // ── Rename ────────────────────────────────────────────
                yield div [] [
                    sectionLabel "Label"
                    onBoot' ["valCh", AVal.channel labelAval]
                        "valCh.onmessage = function(v) { document.getElementById('__ID__').value = JSON.parse(v); };" (
                        input [
                            attribute "type" "text"
                            style "width: 100%; box-sizing: border-box; background: #252535; color: #ddd; border: 1px solid #555; border-radius: 4px; padding: 6px 9px; outline: none; font-size: 13px"
                            onEvent "onchange" ["event.target.value"] (fun args ->
                                args |> List.head |> Pickler.json.UnPickleOfString |> fun v -> RenameNode (selectedId, v))
                        ]
                    )
                ]

                // ── Move to ───────────────────────────────────────────
                let descendants =
                    hierarchy |> FlatTree.descendants selectedId |> Seq.toArray |> HashSet.ofArray
                let targets =
                    [ for i in 0 .. hierarchy.Count - 1 do
                        let fi  = hierarchy.[i]
                        let nid = fi.Value
                        if not (HashSet.contains nid descendants) && not (HashSet.contains nid boxIds) then
                            yield nid, buildPathStr labels hierarchy nid ]
                let currentParent = hierarchy |> FlatTree.parent selectedId
                yield div [] [
                    sectionLabel "Move to"
                    moveDropdown selectedId currentParent targets
                ]

                // ── Add / Remove ──────────────────────────────────────
                let addFolderBtn = if isSingleFolder then btn "folder plus" "Folder"  ""    AddFolder else btnDisabled "folder plus" "Folder"  ""
                let addCubeBtn   = if isSingleFolder then btn "cube"        "Cube"    ""    AddCube   else btnDisabled "cube"        "Cube"    ""
                let removeBtn    = if canRemove      then btn "trash"       "Remove"  "red" RemoveSelected else btnDisabled "trash"  "Remove"  "red"
                yield div [] [
                    sectionLabel "Actions"
                    div [ style "display: flex; flex-wrap: wrap; gap: 6px" ]
                        [ addFolderBtn; addCubeBtn; removeBtn ]
                ]

            // ── Reset (always) ────────────────────────────────────────
            yield div [] [
                btn "redo" "Reset Scene" "" ResetScene
            ]
        })

// Stops click from bubbling to the outer row's selection handler.
let private stopPropagation =
    "$('#__ID__').on('click', function(e) { e.stopPropagation(); });"

let private treeItemNode (key : string) (item : AdaptiveTreeItemData) : DomNode<Message> =
    Incremental.div AttributeMap.empty <| alist {
        let! isGroup = item.isGroup
        let! label   = item.label
        let! color   = item.color
        let rgb      = sprintf "rgb(%d,%d,%d)" color.R color.G color.B

        if isGroup then
            // Folder icon toggles collapse; stop propagation so it doesn't also select the group.
            yield onBoot stopPropagation (
                i [ clazz "folder outline link icon"
                    style $"color: {rgb}"
                    onClick (fun _ -> ToggleCollapse key) ] []
            )
        else
            yield i [ clazz "cube icon"; style $"color: {rgb}" ] []

        yield span [ style $"margin-left: 5px; color: {rgb}" ] [ text label ]
    }

let view (model : AdaptiveModel) =
    let frustum = Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant

    pages (function
        | Pages.Page "render" ->
            let sg =
                model.boxes
                |> AList.toASet
                |> ASet.map (mkISg model)
                |> Sg.set

            FreeFlyController.controlledControl model.camera Camera frustum
                (AttributeMap.ofList [
                    style "width: 100%; height: 100%; background: #1B1C1E"
                    attribute "data-samples" "8"
                ]) sg

        | Pages.Page "tree" ->
            require Html.semui (
                body [ style "width: 100%; height: 100%; margin: 0; overflow: hidden; background: #1B1C1E" ] [
                    model.treeView |> TreeView.view AttributeMap.empty TreeAction treeItemNode
                ]
            )

        | Pages.Page "actions" ->
            require Html.semui (
                body [ style "width: 100%; height: 100%; margin: 0; overflow: auto; background: #1B1C1E; color: #ccc" ] [
                    actionsPanel model
                ]
            )

        | Pages.Body ->
            Html.title false (AVal.constant "Box Tree View") (
                body [ style "width: 100%; height: 100%; overflow: hidden; margin: 0; background: #1B1C1E" ] [
                    GoldenLayout.view [ style "width: 100%; height: 100%" ] model.golden
                ]
            )

        | Pages.Page id ->
            div [ style "color: red; padding: 10px" ] [ text $"Unknown page: {id}" ]
    )

// ---------------------------------------------------------------------------
// App
// ---------------------------------------------------------------------------

let threads (model : Model) =
    FreeFlyController.threads model.camera |> ThreadPool.map Camera

let app : App<Model, AdaptiveModel, Message> =
    let boxes, treeView, boxIds, groupLabels = buildInitialScene ()
    {
        unpersist = Unpersist.instance
        threads   = threads
        initial   =
            { camera        = initialCamera
              boxes         = boxes
              selectedBoxes = HashSet.empty
              hoveredBox    = None
              treeView      = treeView
              golden        = GoldenLayout.create layoutConfig defaultLayout
              boxIds        = boxIds
              groupLabels   = groupLabels }
        update = update
        view   = view
    }
