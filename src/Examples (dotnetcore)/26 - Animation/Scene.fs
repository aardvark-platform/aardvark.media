namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.UI
open FSharp.Data.Adaptive

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Scene =

    module Dimensions =

        module Floor =
            let scale = V3d(18.0, 18.0, 1.0)
            let height = -1.5

        module Grid =
            let size = V2i(8, 8)

        module Box =
            let size = V3d(1.0, 1.0, 0.25)
            let margin = V2d(0.75, 0.75)


    [<AutoOpen>]
    module private Internal =

        module Sg =
            open FShade

            let floor (shadowMap : aval<#ITexture>) (scene : AdaptiveScene) =
                let bb = Box3d.FromCenterAndSize(V3d.Zero, V3d(1.0, 1.0, 1.0))
                Sg.box' C4b.White bb
                |> Sg.diffuseTexture DefaultTextures.checkerboard
                |> Sg.scaling' Dimensions.Floor.scale
                |> Sg.translate 0.0 0.0 Dimensions.Floor.height
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
                        let trafo =
                            (b.position, b.scale, b.rotation) |||> AVal.map3 (fun t s r ->
                                Trafo3d(Shift3d t * Scale3d s * Rot3d.RotationEuler r)
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
                            Sg.onEnter (fun _ -> Hover id)
                            Sg.onLeave (fun () -> Unhover)
                        ]
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
        let grid = Dimensions.Grid.size
        [0 .. grid.X * grid.Y - 1] |> List.map (fun i ->
            V2i(i % grid.X, i / grid.X)
        )

    let initial =
        let h = V2d Dimensions.Grid.size * 0.5
        let p0 = -(h - 0.5) * (Dimensions.Box.size.XY + Dimensions.Box.margin)

        let getPosition (i : V2i) =
            p0 + V2d i * (Dimensions.Box.size.XY + Dimensions.Box.margin)

        let createEntitity (i : V2i) =
            { position = V3d(getPosition i, 0.0)
              rotation = V3d.Zero
              scale = Dimensions.Box.size
              color = C3d.PaleGreen
              alpha = 1.0 }

        let entities =
            entityIndices |> List.map (fun i ->
                i, createEntitity i
            )
            |> HashMap.ofList

        { entities = entities
          lightDirection = V3d(1, 2, 4)
          selected = None
          hovered = None }
