module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open RenderControl.Model
open System.Runtime.CompilerServices


let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }

[<AutoOpen>]
module Deff =
   
    type DeferredNode<'msg>(signature : IFramebufferSignature, size : aval<V2i>, sg : ISg<'msg>) =
        interface ISg<'msg>

        member x.Signature = signature
        member x.Size = size
        member x.Inner = sg

    module Sem =
        open Aardvark.Base.Ag
        open Aardvark.SceneGraph
        open Aardvark.SceneGraph.Semantics

        module Shader =
            open FShade
            type Vertex  =
                {
                    [<FragCoord>] fc : V4d
                }

            let sam =
                sampler2d {
                    texture uniform?DiffuseColorTexture
                }
                
            let samMS =
                sampler2dMS {
                    texture uniform?DiffuseColorTexture
                }

            let blit (v : Vertex) =
                fragment {
                    return sam.[V2i v.fc]
                }
                
            type VertexMS  =
                {
                    [<FragCoord>] fc : V4d
                    [<SampleId>] s : int
                }

            let blitMS  (v : VertexMS) =
                fragment {
                    let p = V2i v.fc
                    return samMS.Read(p, v.s)
                }


        [<Rule>]
        type Semmy() =
            member x.RenderObjects(d : DeferredNode<'msg>, scope : Ag.Scope) =   
                let sems =
                    d.Signature.ColorAttachments 
                    |> Map.toSeq 
                    |> Seq.map (snd >> fst)
                    |> Set.ofSeq
                    |> (fun s -> if Option.isSome d.Signature.DepthAttachment then Set.add DefaultSemantic.Depth s else s)

                let textures = 
                    let o = d.Inner.RenderObjects(scope)
                    let task = d.Signature.Runtime.CompileRender(d.Signature, BackendConfiguration.Default, o)
                    RenderTask.renderSemantics sems d.Size task

                let fin =
                    let samples = d.Signature.ColorAttachments |> Map.toSeq |> Seq.pick (fun (_,(_,a)) -> Some a.samples)
                    Sg.fullScreenQuad
                    |> Sg.diffuseTexture textures.[DefaultSemantic.Colors]
                    |> Sg.shader {
                        if samples > 1 then do! Shader.blitMS
                        else do! Shader.blit
                    }
                    |> Sg.cullMode (AVal.constant CullMode.Back)
                    |> Sg.depthTest (AVal.constant DepthTestMode.None)
                
                fin.RenderObjects(scope)

            member x.PickObjects(d : DeferredNode<'msg>, scope : Ag.Scope) =
                d.Inner.PickObjects(scope)
                
            member x.GlobalBoundingBox(d : DeferredNode<'msg>, scope : Ag.Scope) =
                d.Inner.GlobalBoundingBox(scope)
                
            member x.LocalBoundingBox(d : DeferredNode<'msg>, scope : Ag.Scope) =
                d.Inner.LocalBoundingBox(scope)

let viewScene (model : AdaptiveModel) (values : Aardvark.Service.ClientValues) =

    let s = values.signature
    DeferredNode(s, values.size, 
        Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }
            
    ) :> ISg<_>

    //|> Sg.withEvents [
    //    Sg.onClick (fun p -> Log.warn "%A" p; CenterScene)
    //]


let view (model : AdaptiveModel) = 
    let renderControl =
       FreeFlyController.controlledControlWithClientValues model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
                    (AttributeMap.ofList [ 
                        style "width: 1024px; grid-row: 2; height:768px"; 
                        attribute "showFPS" "true";         // optional, default is false
                        attribute "useMapping" "false"
                        attribute "data-quality" "90"
                        //attribute "showLoader" "false"    // optional, default is true
                        //attribute "data-renderalways" "1" // optional, default is incremental rendering
                        attribute "data-samples" "1"        // optional, default is 1
                    ]) 
            RenderControlConfig.standard
            (viewScene model)


    div [style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
        div [style "grid-row: 1"] [
            text "Hello 3D"
            br []
            button [onClick (fun _ -> CenterScene)] [text "Center Scene"]
        ]
        renderControl
        br []
        text "use first person shooter WASD + mouse controls to control the 3d scene"
    ]

let threads (model : Model) = 
    FreeFlyController.threads model.cameraState |> ThreadPool.map Camera


let app =      
    {
        unpersist = { create = id; update = fun _ _ -> () }
        threads = fun _ -> ThreadPool.empty
        initial = ()
        update = fun _ _ -> ()
        view =
            fun () ->
                subApp
                    {
                        unpersist = Unpersist.instance     
                        threads = threads 
                        initial = 
                            { 
                               cameraState = initialCamera
                            }
                        update = update 
                        view = view
                    }
    }
