module SvgDrawingApp

open SvgDrawing
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives


    
let update m (a : int) = m

let view (mm  : MModel) =
    let circle =
        Svg.circle [ 
                attribute "cx" "50"; attribute "cy" "50";
                attribute "r" "40"; attribute "stroke" "green"; attribute "stroke-width" "4"; attribute "fill" "yellow"
            ]

    let svg =
        Svg.svg [attribute "width" "98"; attribute "height" "120" ] [
            circle
        ]

    svg

let initial = { nixi = 1 }

let app =
    {
        unpersist = Unpersist.instance
        threads =  fun m -> ThreadPool.empty 
        initial = initial
        update = update
        view = view
    }

let start() = App.start app