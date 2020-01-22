module LayoutingApp

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives

open LayoutingModel

let update (m : Model) (action : Action) =
    m

let view (m : MModel) =
    Incremental.div AttributeMap.empty <|
        alist {
            for tab in m.tabs do
                let attributes = 
                    AttributeMap.ofAMap <|
                        amap {
                            let! url = tab.url
                            yield attribute "src" (string url)
                        }
                yield Incremental.voidElem "iframe" attributes
        }

// finally provide an app instance.
let app rootUrl =
    {
        unpersist = Unpersist.instance 
        threads = constF ThreadPool.empty 
        initial = 
            { 
               tabs = 
                IndexList.ofList [
                    for i in 0 .. 5 do
                        yield {name="3D"; url = sprintf "%s/threeD/" rootUrl }
                        yield {name="2D"; url = sprintf "%s/twoD/" rootUrl }
               ]
            }
        update = update // use our update and view function
        view = view
    }
