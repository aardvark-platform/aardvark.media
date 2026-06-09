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

// ---------------------------------------------------------------------------
// Scene helpers
// ---------------------------------------------------------------------------

let private makeBox name (pos : V3d) (color : C4b) =
    { id       = Guid.NewGuid().ToString("N").[..7]
      name     = name
      geometry = Box3d.FromCenterAndSize(pos, V3d.III * 0.8)
      color    = color }

/// Highlight colours used for selection / hover in the 3D view.
let private selectedColor = C4b(255, 240, 60, 255)
let private hoveredColor  = C4b(255, 240, 180, 255)

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
        Sg.onClick  (fun _  -> Select box.id)
        Sg.onEnter  (fun _  -> Hover (Some box.id))
        Sg.onLeave  (fun () -> Hover None)
    ]

// ---------------------------------------------------------------------------
// Initial state
// ---------------------------------------------------------------------------

let private buildInitialScene () =
    let alpha   = makeBox "Alpha"   (V3d(-3.0,  2.0, 0.0)) (C4b(220,  60,  60, 255))
    let beta    = makeBox "Beta"    (V3d(-3.0,  0.0, 0.0)) (C4b(180,  30,  30, 255))
    let gamma   = makeBox "Gamma"   (V3d(-3.0, -2.0, 0.0)) (C4b(240, 140, 140, 255))
    let delta   = makeBox "Delta"   (V3d( 0.0,  2.5, 0.0)) (C4b( 60, 100, 220, 255))
    let epsilon = makeBox "Epsilon" (V3d( 3.0,  1.5, 0.0)) (C4b( 50, 210, 210, 255))
    let zeta    = makeBox "Zeta"    (V3d( 3.0, -0.5, 0.0)) (C4b( 30, 160, 160, 255))
    let eta     = makeBox "Eta"     (V3d( 0.0, -0.5, 0.0)) (C4b(255, 165,   0, 255))
    let theta   = makeBox "Theta"   (V3d( 0.0, -2.5, 0.0)) (C4b(255, 215,   0, 255))

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

    let getChildren id =
        match HashMap.tryFind id hierarchy with
        | Some children -> children :> seq<string>
        | None          -> Seq.empty

    // Tree display data (both groups and box leaves)
    let groupItems =
        [ rootId,  { label = "Scene";       isGroup = true; color = C4b.White }
          grpRed,  { label = "Red Group";   isGroup = true; color = C4b(220, 60, 60, 255) }
          grpBlue, { label = "Blue Group";  isGroup = true; color = C4b(60, 100, 220, 255) }
          grpSub,  { label = "Sub Group C"; isGroup = true; color = C4b(50, 210, 210, 255) } ]

    let boxItems =
        allBoxes |> List.map (fun b -> b.id, { label = b.name; isGroup = false; color = b.color })

    let treeValues = HashMap.ofList (groupItems @ boxItems)
    let treeView   = TreeView.initialize getChildren treeValues rootId

    let boxIds = allBoxes |> List.map (fun b -> b.id) |> HashSet.ofList

    IndexList.ofList allBoxes, treeView, boxIds

// ---------------------------------------------------------------------------
// Golden layout
// ---------------------------------------------------------------------------

let private layoutConfig = LayoutConfig.Default

let private defaultLayout =
    layout {
        row {
            element { id "tree";   title "Scene Tree"; weight 3 }
            element { id "render"; title "3D View";    weight 7 }
        }
    }

let private initialCamera = {
    FreeFlyController.initial with
        view = CameraView.lookAt (V3d(0.0, 0.0, 14.0)) V3d.OOO V3d.OIO
}

// ---------------------------------------------------------------------------
// Update
// ---------------------------------------------------------------------------

let update (model : Model) (msg : Message) =
    match msg with
    | Camera m ->
        { model with camera = FreeFlyController.update model.camera m }

    | Select id ->
        let newSelected = HashSet.single id
        // Sync tree: select the clicked box and scroll to it
        let treeModel =
            model.treeView
            |> TreeView.update (TreeView.Message.Click (id, { shift = false; alt = false; ctrl = false }))
            |> TreeView.update (TreeView.Message.Virtual (VirtualTree.Message.ScrollTo id))
        { model with selectedBoxes = newSelected; treeView = treeModel }

    | Hover optId ->
        { model with hoveredBox = optId }

    | TreeAction msg ->
        let treeModel = model.treeView |> TreeView.update msg
        // When a box leaf is clicked in the tree, update the 3D selection too
        match msg with
        | TreeView.Message.Click (id, _) when HashSet.contains id model.boxIds ->
            { model with treeView = treeModel; selectedBoxes = HashSet.single id }
        | _ ->
            { model with treeView = treeModel }

    | GoldenLayout msg ->
        { model with golden = model.golden |> GoldenLayout.update msg }

// ---------------------------------------------------------------------------
// View
// ---------------------------------------------------------------------------

let private treeItemNode (key : string) (item : AdaptiveTreeItemData) : DomNode<Message> =
    Incremental.div AttributeMap.empty <| alist {
        let! isGroup = item.isGroup
        let! label   = item.label
        let! color   = item.color
        let icon     = if isGroup then "folder outline" else "cube"
        let rgb      = sprintf "rgb(%d,%d,%d)" color.R color.G color.B
        yield i    [ clazz $"{icon} icon"; style $"color: {rgb}" ] []
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
            body [ style "width: 100%; height: 100%; margin: 0; overflow: hidden; background: #1B1C1E" ] [
                model.treeView |> TreeView.view AttributeMap.empty TreeAction treeItemNode
            ]

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
    let boxes, treeView, boxIds = buildInitialScene ()
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
              boxIds        = boxIds }
        update = update
        view   = view
    }
