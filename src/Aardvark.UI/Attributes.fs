namespace Aardvark.UI

open Suave
open Aardvark.Base
open FSharp.Data.Adaptive

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
        let e args () = 
            try 
                args |> cb |> Seq.singleton
            with e -> 
                Log.warn "[Media] onEvent callback faulted"
                Seq.empty

        eventType, AttributeValue.Event(Event.ofDynamicArgs args (fun args -> Seq.delay (e args)))

    let inline onEvent' (eventType : string) (args : list<string>) (cb : list<string> -> seq<'msg>) : Attribute<'msg> = 
        eventType, AttributeValue.Event(Event.ofDynamicArgs args (cb))

    let onFocus (cb : unit -> 'msg) =
        onEvent "onfocus" [] (ignore >> cb)
        
    let onBlur (cb : unit -> 'msg) =
        onEvent "onblur" [] (ignore >> cb)

    let onMouseEnter (cb : V2i -> 'msg) =
        onEvent "onmouseenter" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)

    let onMouseLeave (cb : V2i -> 'msg) =
        onEvent "onmouseleave" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)
    
    let onMouseOver (cb : V2i -> 'msg) =
        onEvent "onmouseover" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)

    let onMouseOut (cb : V2i -> 'msg) =
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
        k, AVal.constant (Some v)

    let onlyWhen (m : aval<bool>) (att : Attribute<'msg>) =
        let (k,v) = att
        k, m |> AVal.map (function true -> Some v | false -> None)


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
        
    type PointerType =
        | Mouse
        | Touch
        | Pen

    let onPointerEvent name (needButton : bool) (preventDefault : Option<int>) (useCapture : Option<bool>) (f : PointerType -> MouseButtons -> V2i -> 'msg) =
        name, AttributeValue.Event {
                clientSide = fun send src -> 
                    String.concat ";" [
                        yield "var rect = getBoundingClientRect(this)"
                        yield "var x = (event.clientX - rect.left)"
                        yield "var y = (event.clientY - rect.top)"
                        match preventDefault with | None -> () | Some i -> yield (sprintf "if(event.which==%d){event.preventDefault();};" i)
                        match useCapture with | None -> () | Some b -> if b then yield "this.setPointerCapture(event.pointerId)" else yield "this.releasePointerCapture(event.pointerId)"
                        yield send src ["event.pointerType";"event.which"; "x|0"; "y|0"]
                        
                    ]
                serverSide = fun client src args -> 
                    match args with
                        | pointertypestr :: which :: x :: y :: _ ->
                            let v : V2i = V2i(int x, int y)
                            let button = if needButton then button which else MouseButtons.None
                            let pointertypestrp = pointertypestr.Trim('\"').Trim('\\')
                            let pointertype = match pointertypestrp with | "mouse" -> Mouse | "pen" -> Pen | "touch" -> Touch | _ -> failwith "PointerType not supported"
                            Seq.singleton (f pointertype button v)
                        | _ ->
                            Seq.empty      
            }
            
    let onCapturedPointerDown (preventDefault : Option<int>) cb  = onPointerEvent "onpointerdown" true preventDefault (Some true) cb
    let onCapturedPointerUp (preventDefault : Option<int>) cb = onPointerEvent "onpointerup" true preventDefault (Some false) cb
    let onCapturedPointerMove (preventDefault : Option<int>) cb = onPointerEvent "onpointermove" false preventDefault None (fun t _ v -> cb t v)

module Gamepad =
    
    let inline private f v =
        let mutable vv = 0.0
        if System.Double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, &vv) then
            Some vv
        else
            None

    let onLeftTriggerChanged (player : int) (action : float -> #seq<'msg>) =
        let evtName = sprintf "gp_leftshoulder_changed_%d" player
        onEvent' evtName ["event"] (function
            | a :: _ ->
                match System.Double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with
                | (true, v) -> action v :> seq<_>
                | _ -> Seq.empty
            | _ ->
                Seq.empty
        )
        
    let onRightTriggerChanged (player : int) (action : float -> #seq<'msg>) =
        let evtName = sprintf "gp_rightshoulder_changed_%d" player
        onEvent' evtName ["event"] (function
            | a :: _ ->
                match f a with
                | Some v -> action v :> seq<_>
                | _ -> Seq.empty
            | _ ->
                Seq.empty
        )
        
    let onLeftStickChanged (player : int) (action : V2d -> #seq<'msg>) =
        let evtName = sprintf "gp_leftstick_changed_%d" player
        onEvent' evtName ["event.X"; "event.Y"] (function
            | x :: y :: _ ->
                match f x, f y with
                | Some x, Some y -> action(V2d(x,y)) :> seq<_>
                | _ -> Seq.empty
            | _ ->
                Seq.empty
        )
        
    let onRightStickChanged (player : int) (action : V2d -> #seq<'msg>) =
        let evtName = sprintf "gp_rightstick_changed_%d" player
        onEvent' evtName ["event.X"; "event.Y"] (function
            | x :: y :: _ ->
                match f x, f y with
                | Some x, Some y -> action(V2d(x,y)) :> seq<_>
                | _ -> Seq.empty
            | _ ->
                Seq.empty
        )

    let onButton (button : int) (player : int) (action : unit -> #seq<'msg>) =
        let evtName = sprintf "gp_press%d_%d" button player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
    let onButtonUp (button : int) (player : int) (action : unit -> #seq<'msg>) =
        let evtName = sprintf "gp_release%d_%d" button player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
    let onButton0 player action = onButton 0 player action
    let onButton0Up player action = onButtonUp 0 player action
    let onButton1 player action = onButton 1 player action
    let onButton1Up player action = onButtonUp 1 player action
    let onButton2 player action = onButton 2 player action
    let onButton2Up player action = onButtonUp 2 player action
    let onButton3 player action = onButton 3 player action
    let onButton3Up player action = onButtonUp 3 player action

    
    let onShoulderLeft player action =
        let evtName = sprintf "gp_press_shoulder_top_left_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
    let onShoulderLeftUp player action =
        let evtName = sprintf "gp_release_shoulder_top_left_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

        
    let onShoulderRight player action =
        let evtName = sprintf "gp_press_shoulder_top_right_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
    let onShoulderRightUp player action =
        let evtName = sprintf "gp_release_shoulder_top_right_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onLeft player action =
        let evtName = sprintf "gp_press_left_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onLeftUp player action =
        let evtName = sprintf "gp_release_left_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

        
    let onRight player action =
        let evtName = sprintf "gp_press_right_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onRightUp player action =
        let evtName = sprintf "gp_release_right_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

        
    let onUp player action =
        let evtName = sprintf "gp_press_up_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onUpUp player action =
        let evtName = sprintf "gp_release_up_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

        
    let onDown player action =
        let evtName = sprintf "gp_press_down_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onDownUp player action =
        let evtName = sprintf "gp_release_down_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
        
    let onStart player action =
        let evtName = sprintf "gp_press_start_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onStartUp player action =
        let evtName = sprintf "gp_release_start_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
        
    let onSelect player action =
        let evtName = sprintf "gp_press_select_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onSelectUp player action =
        let evtName = sprintf "gp_release_select_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
        
    let onHome player action =
        let evtName = sprintf "gp_press_home_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onHomeUp player action =
        let evtName = sprintf "gp_release_home_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
        
        
    let onLeftStickDown player action =
        let evtName = sprintf "gp_press_leftstick_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onLeftStickUp player action =
        let evtName = sprintf "gp_release_leftstick_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)
        
    let onRightStickDown player action =
        let evtName = sprintf "gp_press_rightstick_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

    let onRightStickUp player action =
        let evtName = sprintf "gp_release_rightstick_%d" player
        onEvent' evtName [] (fun _ -> action() :> seq<_>)

        


module Operators =
    
    let inline (=>) a b = Attributes.attribute a b