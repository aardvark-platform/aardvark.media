module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model

// media port of: https://www.w3schools.com/html/html5_draganddrop.asp

let update (model : Model) (msg : Message) =
    match msg with
        | DropTop -> { model with location = Position.Top }
        | DropBottom -> { model with location = Position.Bottom }

let view (model : MModel) =
    let aard name = 
        img [
            // standard image stuff
            attribute "src" "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d0/Ardvark_The_Aardvark_Original.png/800px-Ardvark_The_Aardvark_Original.png"; 
            attribute "alt" "aardvark"
            // if we start drag, use drag(event) defined in js to activate dragging
            attribute "ondragstart" "drag(event)"
            style "width: 200px"
            // just a class to identify this element in human readable way
            clazz name
        ]
    let dependencies = 
        [ 
            { kind = Script; name = "dragDrop"; url = "dragDrop.js" }
        ]    

    require dependencies (
        body [] [
            div [ style "width: 290px; height: 100px; border:1px solid black;"; 
                  // allow dropping
                  attribute "ondragover" "allowDrop(event)" 
                  // on drop, optionally lookup who was dragged here (in this example there is only one draggable thing so we do not need this actually)
                  // and fire event (for demonstration purposes we print the source...
                  onEvent "ondrop" ["{ name : event.dataTransfer.getData('source')}"] (fun args -> printfn "dragged thing: %A" args; DropTop )
                ] [
                Incremental.div AttributeMap.empty <| alist {
                    let! position = model.location
                    if position = Position.Top then 
                        yield aard "top"
                }
            ]
            div [ style "width: 290px; height: 100px; border:1px solid black;"; 
                  attribute "ondragover" "allowDrop(event)"
                  onEvent "ondrop" ["{ name : event.dataTransfer.getData('source')}"] (fun args -> printfn "dragged thing: %A" args; DropBottom )
                ] [
                Incremental.div AttributeMap.empty <| alist {
                    let! position = model.location
                    if position = Position.Bottom then 
                        yield aard "bottom"
                }
            ]
        
        ]
    )

let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               location = Position.Top
            }
        update = update 
        view = view
    }
