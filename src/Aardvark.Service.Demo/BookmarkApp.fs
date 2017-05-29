namespace UI.Composed

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Rendering.Text

open PRo3DModels

 open Aardvark.SceneGraph.SgPrimitives
 open Aardvark.SceneGraph.FShadeSceneGraph

module BookmarkApp = 
            
    type Action =
        | CameraMessage of ArcBallController.Message
        | Move of V3d
        | AddBookmark of V3d 
        | UpdateCam of string
        | KeyDown of key : Keys
        | KeyUp of key : Keys
        | Enter of string
        | Exit       
    
    let getNewBookmark (p:V3d)(m:BookmarkAppModel) = 
        let id = sprintf "%d" (PList.count m.bookmarks)
        let bm = {
            id = id
            point = p
            color = new C4b(255,0,255)
            camState = m.bookmarkCamera
            visible = true
            text = ""
        }
        bm

    let findBM (bmid:string)(m:BookmarkAppModel) =
        let bm = match m.bookmarks |> Seq.tryFind(fun x -> x.id.ToString() = bmid) with
                    | Some x -> x
                    | None -> m.bookmarks.[0]
        bm

    let update (model : BookmarkAppModel) (act : Action) =
        match act, model.draw with
            | CameraMessage m, false -> 
                    { model with bookmarkCamera = ArcBallController.update model.bookmarkCamera m }                    
            | KeyDown Keys.LeftCtrl, _ -> 
                    { model with draw = true }
            | KeyUp Keys.LeftCtrl, _ -> 
                    { model with draw = false; hoverPosition = None }
            | Move p, true -> 
                    { model with hoverPosition = Some (Trafo3d.Translation p) }
            | AddBookmark p, true ->
                    { model with bookmarks = model.bookmarks |> PList.append (getNewBookmark p model) }
            | UpdateCam id, _ -> 
                    let bm = (findBM id model)
                    { model with bookmarkCamera = bm.camState }            
            | Enter id, _-> { model with boxHovered = Some id }            
            | Exit, _ -> { model with boxHovered = None }       
            | _ -> model
            
            
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    module Draw =

        let computeScale (view : IMod<CameraView>)(p:IMod<V3d>)(size:float) =        
            adaptive {
                let! p = p
                let! v = view
                let distV = p - v.Location
                let distF = V3d.Dot(v.Forward, distV)
                return distF * size / 800.0 //needs hfov at this point
            }

        let computeScale2 (view : IMod<CameraView>)(p:IMod<V3d>)(size:float) =     
            Mod.map2 (fun (p : V3d) (v : CameraView) -> 
                let distV = p - v.Location
                let distF = V3d.Dot(v.Forward, distV)
                distF * size / 800.0 //needs hfov at this point
            ) p view

        let mkISg color size trafo (id:string) =         
            Sg.sphere 5 color size 
                    |> Sg.shader {
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.vertexColor
                        do! DefaultSurfaces.simpleLighting
                    }
                    |> Sg.noEvents
                    |> Sg.pickable (PickShape.Sphere(Sphere3d(V3d.OOO,0.05)))
                        |> Sg.withEvents [
                           Sg.onEnter (fun _ -> Enter id)
                        ]    
                    |> Sg.trafo(trafo) 
        
        let canvas =  
            let b = new Box3d( V3d(-2.0,-0.5,-2.0), V3d(2.0,0.5,2.0) )                                               
            Sg.box (Mod.constant C4b.White) (Mod.constant b)
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.requirePicking
                |> Sg.noEvents 
                    |> Sg.withEvents [
                        Sg.onMouseMove (fun p -> Move p)
                        Sg.onClick(fun p -> AddBookmark p)
                        Sg.onLeave (fun _ -> Exit)
                    ]    

        let brush (hovered : IMod<Trafo3d option>) = 
            let trafo =
                hovered |> Mod.map (function o -> match o with 
                                                    | Some t-> t
                                                    | None -> Trafo3d.Scale(V3d.Zero))

            mkISg (Mod.constant C4b.Red) (Mod.constant 0.05) trafo ""

        let dots (bm : MBookmark) (point : IMod<V3d>) (color : IMod<C4b>) (view : IMod<CameraView>) =         
            mkISg color (computeScale view point 5.0) (point |> Mod.map Trafo3d.Translation) (Mod.force bm.id)              


        let getColor (model : MBookmarkAppModel) (bm : MBookmark) =
            let hc = Mod.constant (new C4b(0, 0, 255))

            adaptive {
                let! hovered = model.boxHovered
                match hovered with
                    | Some k when k = Mod.force bm.id -> 
                        return! hc
                    | _ -> return! bm.color
            }


        let bookmark' (model : MBookmarkAppModel)(bm : MBookmark)(view : IMod<CameraView>) = 
            let point = bm.point            
            let color = getColor model bm

            dots bm point color view

    let view (model : MBookmarkAppModel) =
                    
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
        require (Html.semui) (
            body [clazz "ui"; style "background: #1B1C1E"] [
                ArcBallController.controlledControl model.bookmarkCamera CameraMessage frustum
                    (AttributeMap.ofList [ onKeyDown KeyDown; onKeyUp KeyUp
                                           attribute "style" "width:65%; height: 100%; float: left;" ]
                    )
                    (
                        let view = model.bookmarkCamera.view

                        let bookmarkSet = ASet.ofAList model.bookmarks
                        
                        let bookmarks =
                            aset {
                                for b in bookmarkSet do
                                   yield Draw.bookmark' model b view
                                
                            } |> Sg.set

                        [Draw.canvas; Draw.brush model.hoverPosition; bookmarks] 
                            |> Sg.ofList
                            |> Sg.fillMode model.rendering.fillMode
                            |> Sg.cullMode model.rendering.cullMode                                                                                           
                )

                div [style "width:35%; height: 100%; float:right"] [
                    
                    Html.SemUi.accordion "Bookmarks" "File Outline" true [
                        Incremental.div 
                            (AttributeMap.ofList [clazz "ui divided list"]) (
                            
                                alist {   
                                    for b in model.bookmarks  do                                    

                                        let attributes =
                                            amap {
                                                let! c = Draw.getColor model b
                                                yield style (sprintf "background: %s" (Html.ofC4b c))
                                                yield onClick(fun _ -> UpdateCam (Mod.force b.id))
                                                yield onMouseEnter(fun _ -> Enter (Mod.force b.id))
                                                yield onMouseLeave(fun _ -> Exit) 
                                                yield clazz "item"
                                            } |> AttributeMap.ofAMap

                                        yield Incremental.div' attributes [
                                                i [clazz "medium File Outline middle aligned icon"][]
                                                Incremental.text b.id
                                            ]
                                }     
                        )
                    ]
                ]

                
                        
            ]
        )

    let initial : BookmarkAppModel =
        {
            bookmarkCamera           = { ArcBallController.initial with view = CameraView.lookAt (6.0 * V3d.OIO) V3d.Zero V3d.OOI}
            rendering        = InitValues.rendering
            boxHovered = None
            hoverPosition = None
            draw = false            
            
            bookmarks = PList.empty
        }

    let app : App<BookmarkAppModel,MBookmarkAppModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.bookmarkCamera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app

