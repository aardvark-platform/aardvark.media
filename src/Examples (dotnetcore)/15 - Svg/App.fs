module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model


let update (model : Model) (msg : Message) =
    printfn "%A" msg
    match msg with
        | StartDrag s -> { model with startPos = Some s }
        | Drag p -> { model with pos = p }


let attach (s : string) (t : string) =
    clientEvent s <| """
            var parent = document.getElementsByClassName('mySvg')[0];
            var o = document.getElementById('__ID__');
            var p = parent.createSVGPoint();
            p.x = event.clientX;
            p.y = event.clientY;
            if(o)
            {
                var m = o.getScreenCTM();
                p = p.matrixTransform(m.inverse());
                aardvark.processEvent('__ID__', '__TARGET__', [toFixedV2d(p)]);
            }
        """.Replace("__SOURCE__",s).Replace("__TARGET__",t).Replace("\n","").Replace("\r","")
   
let onDrag (s : string) (cb : V2d -> 'msg) =
    onEvent s [] (List.head >> Aardvark.UI.Pickler.unpickleOfJson >> List.head >> cb)

let view (model : MModel) =

    let (=>) n v = attribute n v

    body [] [
        Svg.svg [clazz "mySvg"] [
            Incremental.Svg.circle <| 
                AttributeMap.ofListCond [
                    always <| attribute "r" "20"
                    always <| attribute "cx" "20"
                    always <| attribute "cy" "20"
                    always <| attach "onmousedown" "startDragPos"
                    always <| attach "onmousemove" "dragPos"
                    onlyWhen (Mod.map Option.isNone model.startPos) (onDrag "startDragPos" StartDrag)
                    onlyWhen (Mod.map Option.isSome model.startPos) (onDrag "dragPos" Drag)
                ] 
        ]
    ]

let threads (model : Model) = ThreadPool.empty

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               startPos = None
               pos = V2d.OO
            }
        update = update 
        view = view
    }
