namespace Aardvark.UI

open Suave
open Aardvark.Base
open Aardvark.Base.Incremental

type Attribute<'msg> = string * AttributeValue<'msg>

[<AutoOpen>]
module Attributes =
    let inline attribute (key : string) (value : string) : Attribute<'msg> = 
        key, AttributeValue.String value

    let inline clazz value = attribute "class" value
    
    let inline style value = attribute "style" value
    
    let inline js (name : string) (code : string) : Attribute<'msg> =
        name, 
        AttributeValue.Event { 
            clientSide = fun send id -> code.Replace("__ID__", id)
            serverSide = fun _ _ _ -> Seq.empty
        }
    

module Helpers = 
    open Aardvark.Application

    let button (str : string) =
        match int (float str) with
            | 1 -> MouseButtons.Left
            | 2 -> MouseButtons.Middle
            | 3 -> MouseButtons.Right
            | _ -> MouseButtons.None
[<AutoOpen>]
module Events =
    open Aardvark.Application
    open Helpers

    let inline onEvent (eventType : string) (args : list<string>) (cb : list<string> -> 'msg) : Attribute<'msg> = 
        eventType, AttributeValue.Event(Event.ofDynamicArgs args (cb >> Seq.singleton))

    let inline onEvent' (eventType : string) (args : list<string>) (cb : list<string> -> seq<'msg>) : Attribute<'msg> = 
        eventType, AttributeValue.Event(Event.ofDynamicArgs args (cb))

    let onFocus (cb : unit -> 'msg) =
        onEvent "onfocus" [] (ignore >> cb)
        
    let onBlur (cb : unit -> 'msg) =
        onEvent "onblur" [] (ignore >> cb)

    let onMouseEnter (cb : V2i -> 'msg) =
        onEvent "onmouseenter" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)

    let onMouseLeave (cb : V2i -> 'msg) =
        onEvent "onmouseout" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)
        
    let onMouseMove (cb : V2i -> 'msg) = 
        onEvent "onmousemove" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)

    let onMouseClick (cb : V2i -> 'msg) = 
        onEvent "onclick" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)

    let onMouseDoubleClick (cb : V2i -> 'msg) = 
        onEvent "ondblclick" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)
    
    let onContextMenu (cb : unit -> 'msg) = 
        onEvent "oncontextmenu" [] (ignore >> cb)
    
    let onChange (cb : string -> 'msg) = 
        onEvent "onchange" ["event.target.value"] (List.head >> Pickler.json.UnPickleOfString >> cb)

    /// for continous updates (e.g. see http://stackoverflow.com/questions/18544890/onchange-event-on-input-type-range-is-not-triggering-in-firefox-while-dragging)
    let onInput (cb : string -> 'msg) = 
        onEvent "oninput" ["event.target.value"] (List.head >> Pickler.json.UnPickleOfString >> cb)
            
    let onChange' (cb : string -> seq<'msg>) = 
        onEvent' "onchange" ["event.target.value"] (List.head >> Pickler.json.UnPickleOfString >> cb)

    /// for continous updates (e.g. see http://stackoverflow.com/questions/18544890/onchange-event-on-input-type-range-is-not-triggering-in-firefox-while-dragging)
    let onInput' (cb : string -> seq<'msg>) = 
        onEvent' "oninput" ["event.target.value"] (List.head >> Pickler.json.UnPickleOfString >> cb)
        
    let onWheelPrevent (prevent : bool) (f : Aardvark.Base.V2d -> 'msg) =
        let serverClick (args : list<string>) : Aardvark.Base.V2d = 
            let delta = List.head args |> Pickler.json.UnPickleOfString
            delta  / Aardvark.Base.V2d(-100.0,-100.0) // up is down in mouse wheel events

        let args =
            {
                clientSide = fun send id -> (if prevent then "event.preventDefault();" else "")+send id ["{ X: event.deltaX.toFixed(), Y: event.deltaY.toFixed() }"]
                serverSide = fun session id args -> (serverClick >> f >> Seq.singleton) args
            }
        "onwheel" , AttributeValue.Event(args)

    let onWheel (f : Aardvark.Base.V2d -> 'msg) = onWheelPrevent false f

    let onMouseDown (cb : MouseButtons -> V2i -> 'msg) = 
        onEvent 
            "onmousedown" 
            ["event.clientX"; "event.clientY"; "event.which"] 
            (fun args ->
                match args with
                    | x :: y :: b :: _ ->
                        let x = int (float x)
                        let y = int (float y)
                        let b = button b
                        cb b (V2i(x,y))
                    | _ ->
                        failwith "asdasd"
            )

    let onMouseUp (cb : MouseButtons -> V2i -> 'msg) = 
        onEvent 
            "onmouseup" 
            ["event.clientX"; "event.clientY"; "event.which"] 
            (fun args ->
                match args with
                    | x :: y :: b :: _ ->
                        let x = int (float x)
                        let y = int (float y)
                        let b = button b
                        cb b (V2i(x,y))
                    | _ ->
                        failwith "asdasd"
            )

    let onClick (cb : unit -> 'msg) = onEvent "onclick" [] (ignore >> cb)

    let clientEvent (name : string) (cb : string) = js name cb

    let onKeyDown (cb : Keys -> 'msg) =
        "onkeydown" ,
        AttributeValue.Event(
            Event.ofDynamicArgs
                ["event.repeat"; "event.keyCode"]
                (fun args ->
                    match args with
                        | rep :: keyCode :: _ ->
                            if rep <> "true" then
                                let keyCode = int (float keyCode)
                                let key = KeyConverter.keyFromVirtualKey keyCode
                                Seq.delay (fun () -> Seq.singleton (cb key))
                            else
                                Seq.empty
                        | _ ->
                            Seq.empty
                )
        )

    let onKeyUp (cb : Keys -> 'msg) =
        "onkeyup" ,
        AttributeValue.Event(
            Event.ofDynamicArgs
                ["event.keyCode"]
                (fun args ->
                    match args with
                        | keyCode :: _ ->
                            let keyCode = int (float keyCode)
                            let key = KeyConverter.keyFromVirtualKey keyCode
                            Seq.delay (fun () -> Seq.singleton (cb key))
                        | _ ->
                            Seq.empty
                )
        )

    let always (att : Attribute<'msg>) =
        let (k,v) = att
        k, Mod.constant (Some v)

    let onlyWhen (m : IMod<bool>) (att : Attribute<'msg>) =
        let (k,v) = att
        k, m |> Mod.map (function true -> Some v | false -> None)


    let internal onMouseRel (kind : string) (needButton : bool) (f : MouseButtons -> V2d -> 'msg) =
        kind, AttributeValue.Event {
            clientSide = fun send src -> 
                String.concat ";" [
                    "var rect = getBoundingClientRect(event.target)"
                    "var x = (event.clientX - rect.left) / rect.width"
                    "var y = (event.clientY - rect.top) / rect.height"
                    send src ["event.which"; "{ X: x.toFixed(10), Y: y.toFixed(10) }"]
                        
                ]
            serverSide = fun client src args -> 
                match args with
                    | which :: pos :: _ ->
                        let v : V2d = Pickler.json.UnPickleOfString pos
                        let button = if needButton then button which else MouseButtons.Left
                        Seq.singleton (f button v)
                    | _ ->
                        Seq.empty      
        }

    let internal onMouseAbs (kind : string) (needButton : bool) (f : MouseButtons -> V2d -> V2d -> 'msg) =
        kind, AttributeValue.Event {
            clientSide = fun send src -> 
                String.concat ";" [
                    "var rect = getBoundingClientRect(event.target)"
                    "var x = (event.clientX - rect.left)"
                    "var y = (event.clientY - rect.top)"
                    send src ["event.which"; "{ X: x.toFixed(10), Y: y.toFixed(10) }"; "{ X: rect.width.toFixed(10), Y: rect.height.toFixed(10) }"]
                        
                ]
            serverSide = fun client src args -> 
                match args with
                    | which :: pos :: size :: _ ->
                        let pos : V2d = Pickler.json.UnPickleOfString pos
                        let size : V2d = Pickler.json.UnPickleOfString size
                        let button = if needButton then button which else MouseButtons.Left
                        Seq.singleton (f button pos size)
                    | _ ->
                        Seq.empty      
        }

    let onMouseDownAbs (f : MouseButtons -> V2d -> V2d -> 'msg) =
        onMouseAbs "onmousedown" true f

    let onMouseUpAbs (f : MouseButtons -> V2d -> V2d -> 'msg) =
        onMouseAbs "onmouseup" true f

    let onMouseMoveAbs (f : V2d -> V2d -> 'msg) =
        onMouseAbs "onmousemove" false (fun _ -> f)

    let onMouseClickAbs (f : MouseButtons -> V2d -> V2d -> 'msg) =
        onMouseAbs "onclick" true f
        
    let onMouseDoubleClickAbs (f : MouseButtons -> V2d -> V2d -> 'msg) =
        onMouseAbs "ondblclick" true f



    let onMouseDownRel (f : MouseButtons -> V2d -> 'msg) =
        onMouseRel "onmousedown" true f

    let onMouseUpRel (f : MouseButtons -> V2d -> 'msg) =
        onMouseRel "onmouseup" true f

    let onMouseMoveRel (f : V2d -> 'msg) =
        onMouseRel "onmousemove" false (fun _ -> f)

    let onMouseClickRel (f : MouseButtons -> V2d -> 'msg) =
        onMouseRel "onclick" true f

    let onWheel' (f : V2d -> V2d -> 'msg) =

        "onwheel", AttributeValue.Event {
            clientSide = fun send src -> 
                String.concat ";" [
                    "var rect = getBoundingClientRect(event.target)"
                    "var x = (event.clientX - rect.left) / rect.width"
                    "var y = (event.clientY - rect.top) / rect.height"
                    send src ["{ X: event.deltaX.toFixed(), Y : event.deltaY.toFixed() }"; "{ X: x.toFixed(10), Y: y.toFixed(10) }"]
                        
                ]
            serverSide = fun client src args -> 
                match args with
                    | delta :: pos :: _ ->
                        let v : V2d = Pickler.json.UnPickleOfString pos
                        let delta : V2d = Pickler.json.UnPickleOfString delta
                        Seq.singleton (f delta v)
                    | _ ->
                        Seq.empty      
        }
        
    let onPointerEvent name (needButton : bool) (preventDefault : Option<int>) (useCapture : Option<bool>) (f : MouseButtons -> V2i -> 'msg) =
        name, AttributeValue.Event {
                clientSide = fun send src -> 
                    String.concat ";" [
                        yield "var rect = getBoundingClientRect(this)"
                        yield "var x = (event.clientX - rect.left)"
                        yield "var y = (event.clientY - rect.top)"
                        match preventDefault with | None -> () | Some i -> yield (sprintf "if(event.which==%d){event.preventDefault();};" i)
                        match useCapture with | None -> () | Some b -> if b then yield "this.setPointerCapture(event.pointerId)" else yield "this.releasePointerCapture(event.pointerId)"
                        yield send src ["event.which"; "x|0"; "y|0"]
                        
                    ]
                serverSide = fun client src args -> 
                    match args with
                        | which :: x :: y :: _ ->
                            let v : V2i = V2i(int x, int y)
                            let button = if needButton then button which else MouseButtons.None
                            Seq.singleton (f button v)
                        | _ ->
                            Seq.empty      
            }
            
    let onCapturedPointerDown (preventDefault : Option<int>) (cb : MouseButtons -> V2i -> 'msg)  = onPointerEvent "onpointerdown" true preventDefault (Some true) cb
    let onCapturedPointerUp (preventDefault : Option<int>) cb = onPointerEvent "onpointerup" true preventDefault (Some false) cb
    let onCapturedPointerMove (preventDefault : Option<int>) cb = onPointerEvent "onpointermove" false preventDefault None (fun _ v -> cb v)



module Operators =
    
    let inline (=>) a b = Attributes.attribute a b