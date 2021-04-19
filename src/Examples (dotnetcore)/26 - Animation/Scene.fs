namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.UI
open FSharp.Data.Adaptive

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Scene =

    module private ColorPalette =

        // http://vrl.cs.brown.edu/color
        let private data = [|
                C3b(117,234,182)
                C3b(175,22,128)
                C3b(134,204,49)
                C3b(254,116,254)
                C3b(52,245,14)
                C3b(162,37,226)
                C3b(66,134,33)
                C3b(239,68,75)
                C3b(20,186,225)
                C3b(144,14,8)
                C3b(218,185,255)
                C3b(110,57,1)
                C3b(185,205,161)
                C3b(100,49,118)
                C3b(242,192,41)
                C3b(49,91,243)
                C3b(234,130,68)
                C3b(17,93,82)
            |]

        let mutable private index = 0

        let getNext() =
            let c = data.[index]
            index <- (index + 1) % data.Length
            C3d c


    module Properties =

        module Floor =
            let scale = V3d(20.0, 20.0, 1.0)
            let height = -1.5

        module Grid =
            let size = V2i(6, 6)

        module Tile =
            let size = V3d(1.2, 1.2, 0.25)
            let margin = V2d(1.5, 1.5)
            let colorNeutral = C3d.PaleGreen
            let colorHovered = C3d.DodgerBlue

        module Cube =
            let size = V3d(0.5)


    [<AutoOpen>]
    module private Internal =

        module Sg =
            open FShade

            let floor (shadowMap : aval<#ITexture>) (scene : AdaptiveScene) =
                let bb = Box3d.FromCenterAndSize(V3d.Zero, V3d(1.0, 1.0, 1.0))
                Sg.box' C4b.White bb
                |> Sg.diffuseTexture DefaultTextures.checkerboard
                |> Sg.scaling' Properties.Floor.scale
                |> Sg.translate 0.0 0.0 Properties.Floor.height
                |> Sg.uniform "LightViewProj" (Shadows.lightViewProj scene.lightDirection)
                |> Sg.uniform "LightDirection" scene.lightDirection
                |> Sg.texture "ShadowTexture" shadowMap
                |> Sg.cullMode' CullMode.Back
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.diffuseTexture
                    do! Shader.lighting
                }

            let getBoxes (fragmentShaders : List<Effect>) (map : amap<V2i, AdaptiveEntity>) =
                let boxSg =
                    let bb = Box3d.FromCenterAndSize(V3d.Zero, V3d.One)
                    Sg.box' C4b.White bb

                aset {
                    for (id, b) in map |> AMap.toASet do
                        let visible =
                            b.flag |> AVal.map (function
                                | AdaptiveEntityFlag.AdaptiveResolved -> false
                                | _ -> true
                            )

                        let trafo =
                            (b.position, b.rotation, b.scale) |||> AVal.map3 (fun t r s ->
                                Trafo3d(Shift3d t * Rot3d.RotationEuler r * Scale3d s)
                            )

                        boxSg
                        |> Sg.trafo trafo
                        |> Sg.uniform "Color" b.color
                        |> Sg.effect [
                            yield DefaultSurfaces.trafo |> toEffect
                            yield! fragmentShaders
                        ]
                        |> Sg.requirePicking
                        |> Sg.withEvents [
                            Sg.onClick (fun _ -> Select id)
                            Sg.onEnter (fun _ -> Hover id)
                            Sg.onLeave (fun _ -> Unhover id)
                        ]
                        |> Sg.onOff visible
                }
                |> Sg.set

            let boxes (shadowMap : aval<#ITexture>) (scene : AdaptiveScene) =
                scene.entities |> getBoxes [
                    DefaultSurfaces.sgColor |> toEffect
                    Shader.lighting         |> toEffect
                ]
                |> Sg.uniform "LightViewProj" (Shadows.lightViewProj scene.lightDirection)
                |> Sg.uniform "LightDirection" scene.lightDirection
                |> Sg.texture "ShadowTexture" shadowMap
                |> Sg.cullMode' CullMode.Back

        let createShadowMap (runtime : IRuntime) (scene : AdaptiveScene) =
            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Depth, { format = RenderbufferFormat.DepthComponent32; samples = 1 }
                ]

            let sg =
                scene.entities
                |> Sg.getBoxes [DefaultSurfaces.constantColor C4f.White |> toEffect]
                |> Sg.cullMode' CullMode.Front

            sg |> Shadows.computeShadowMap signature scene.lightDirection


    let view (runtime : IRuntime) (scene : AdaptiveScene) =
        let shadowMap = createShadowMap runtime scene

        Sg.ofList [
            scene |> Sg.floor shadowMap
            scene |> Sg.boxes shadowMap
        ]

    let entityIndices =
        let grid = Properties.Grid.size
        [0 .. grid.X * grid.Y - 1] |> List.map (fun i ->
            V2i(i % grid.X, i / grid.X)
        )

    let initial =
        let h = V2d Properties.Grid.size * 0.5
        let p0 = -(h - 0.5) * (Properties.Tile.size.XY + Properties.Tile.margin)

        let getPosition (i : V2i) =
            p0 + V2d i * (Properties.Tile.size.XY + Properties.Tile.margin)

        let createEntitity (color : C3d) (i : V2i) =
            { position = V3d(getPosition i, 0.0)
              rotation = V3d.Zero
              scale = Properties.Tile.size
              color = Properties.Tile.colorNeutral
              flag = Active
              identity = color }

        let entities =

            let colors =
                let rnd = RandomSystem()

                let mutable available =
                    List.init (entityIndices.Length / 2) (fun _ ->
                        ColorPalette.getNext(), 2
                    )

                entityIndices |> List.map (fun id ->
                    let i = rnd.UniformInt available.Length
                    let (c, n) = available.[i]

                    available <- available |> List.except [(c, n)]
                    if n > 1 then
                        available <- (c, n - 1) :: available

                    id, c
                )
                |> HashMap.ofList

            entityIndices |> List.map (fun i ->
                i, createEntitity colors.[i] i
            )
            |> HashMap.ofList

        { entities = entities
          lightDirection = V3d(1, 2, 4)
          selected = None }