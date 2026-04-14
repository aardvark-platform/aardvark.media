namespace Aardvark.UI

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

    /// Attributes for render controls.
    module RenderAttribute =

        /// Background color (clear color) of the render control. Default is solid black.
        let inline background (color: string) = style $"background: {color}"

        /// Samples of the render control. Default is 1.
        let inline samples (samples: int) = attribute "data-samples" <| string samples

        /// JPEG quality of the render control image. Has no effect when mapping is used. Default is 80.
        let inline quality (quality: int) = attribute "data-quality" <| string quality

        /// If true, an overlay with the current frame rate will be displayed. Default is false.
        let inline showFps (enabled: bool) = attribute "data-show-fps" <| string enabled

        /// If true, an animated loader will be displayed until the first rendered frame is available. Default is true.
        let inline showLoader (enabled: bool) = attribute "data-show-loader" <| string enabled

        /// If true, rendered frames will be transferred to the client directly via shared memory. Default is true.
        let inline useMapping (enabled: bool) = attribute "data-use-mapping" <| string enabled

        /// If true, the client requests a new frame immediately after a frame has finished processing. Default is false.
        let inline renderAlways (enabled: bool) = attribute "data-render-always" <| string enabled

        /// Custom background image of the loader.
        let inline customLoaderImage (image: string) = attribute "data-custom-loader-img" image

        /// Custom background image size of the loader.
        let inline customLoaderImageSize (size: string) = attribute "data-custom-loader-size" size


module Operators =
    let inline (=>) (key: string) (value: string) : Attribute<'msg> = attribute key value

[<AutoOpen>]
module Events =
    open Aardvark.Application

    let inline onEvent' (eventType : string) (args : list<string>) (cb : list<string> -> seq<'msg>) : Attribute<'msg> =
        eventType, AttributeValue.Event(Event.ofDynamicArgs args cb)

    let inline onEvent (eventType : string) (args : list<string>) (cb : list<string> -> 'msg) : Attribute<'msg> =
        onEvent' eventType args (cb >> Seq.singleton)

    /// <summary>
    /// Render control event triggered immediately before or after rendering.
    /// </summary>
    /// <param name="eventType">Type of the event; must be 'onBeforeRender' or 'onAfterRender'.</param>
    /// <param name="cb">Callback to be invoked.</param>
    let inline onRenderEvent' (eventType: string) (cb: RenderClientInfo -> 'msg seq) : Attribute<'msg> =
        eventType, AttributeValue.RenderEvent cb

    /// <summary>
    /// Render control event triggered immediately before or after rendering.
    /// </summary>
    /// <param name="eventType">Type of the event; must be 'onBeforeRender' or 'onAfterRender'.</param>
    /// <param name="cb">Callback to be invoked.</param>
    let inline onRenderEvent (eventType: string) (cb: RenderClientInfo -> 'msg) : Attribute<'msg> =
        onRenderEvent' eventType (cb >> Seq.singleton)

    /// <summary>
    /// Render control event triggered immediately before rendering.
    /// </summary>
    /// <param name="cb">Callback to be invoked.</param>
    let inline onBeforeRender' (cb: RenderClientInfo -> 'msg seq) : Attribute<'msg> =
        onRenderEvent' "onBeforeRender" cb

    /// <summary>
    /// Render control event triggered immediately before rendering.
    /// </summary>
    /// <param name="cb">Callback to be invoked.</param>
    let inline onBeforeRender (cb: RenderClientInfo -> 'msg) : Attribute<'msg> =
        onRenderEvent "onBeforeRender" cb

    /// <summary>
    /// Render control event triggered immediately after rendering.
    /// </summary>
    /// <param name="cb">Callback to be invoked.</param>
    let inline onAfterRender' (cb: RenderClientInfo -> 'msg seq) : Attribute<'msg> =
        onRenderEvent' "onAfterRender" cb

    /// <summary>
    /// Render control event triggered immediately after rendering.
    /// </summary>
    /// <param name="cb">Callback to be invoked.</param>
    let inline onAfterRender (cb: RenderClientInfo -> 'msg) : Attribute<'msg> =
        onRenderEvent "onAfterRender" cb

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
                        let b = MouseButtons.ofEventStr b
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
                        let b = MouseButtons.ofEventStr b
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
        
    type KeyModifiers =
        {
            shift : bool
            alt : bool
            ctrl : bool
        }

    module internal KeyModifiers =

        let ofString (shift: string) (alt: string) (ctrl: string) =
            {
                shift = shift = "true"
                alt = alt = "true"
                ctrl = ctrl = "true"
            }

    let onKeyDownModifiers (cb : KeyModifiers -> Keys -> 'msg) =
        "onkeydown" ,
        AttributeValue.Event(
            Event.ofDynamicArgs
                ["event.repeat"; "event.keyCode"; "event.shiftKey"; "event.altKey"; "event.ctrlKey"]
                (fun args ->
                    match args with
                        | rep :: keyCode :: shift :: alt :: ctrl :: _ ->
                            if rep <> "true" then
                                let keyCode = int (float keyCode)
                                let key = KeyConverter.keyFromVirtualKey keyCode
                                let m = KeyModifiers.ofString shift alt ctrl
                                Seq.delay (fun () -> Seq.singleton (cb m key))
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
        
    let onKeyUpModifiers (cb : KeyModifiers -> Keys -> 'msg) =
        "onkeyup" ,
        AttributeValue.Event(
            Event.ofDynamicArgs
                ["event.keyCode"; "event.shiftKey"; "event.altKey"; "event.ctrlKey"]
                (fun args ->
                    match args with
                        | keyCode :: shift :: alt :: ctrl :: _ ->
                            let keyCode = int (float keyCode)
                            let key = KeyConverter.keyFromVirtualKey keyCode
                            let m = 
                                { 
                                    shift = shift = "true"; 
                                    alt = alt = "true"
                                    ctrl = ctrl = "true"
                                }
                            Seq.delay (fun () -> Seq.singleton (cb m key))
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
                        let button = if needButton then MouseButtons.ofEventStr which else MouseButtons.Left
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
                        let button = if needButton then MouseButtons.ofEventStr which else MouseButtons.Left
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

    module internal PointerType =

        let ofString (str: string) =
            match str.Trim('\"', '\\') with // Is this really needed?
            | "mouse" -> Mouse
            | "touch" -> Touch
            | "pen" -> Pen
            | _ -> failwith $"PointerType '{str}' not supported"

    let onPointerEventModifiers (name: string) (needButton : bool) (preventDefault : Option<int>) (useCapture : Option<bool>)
                                (cb : PointerType -> KeyModifiers -> MouseButtons -> V2i -> 'msg) =
        name, AttributeValue.Event {
                clientSide = fun send src -> 
                    String.concat ";" [
                        yield "var rect = getBoundingClientRect(this)"
                        yield "var x = (event.clientX - rect.left)"
                        yield "var y = (event.clientY - rect.top)"
                        match preventDefault with | None -> () | Some i -> yield (sprintf "if(event.which==%d){event.preventDefault();};" i)
                        match useCapture with | None -> () | Some b -> if b then yield "this.setPointerCapture(event.pointerId)" else yield "this.releasePointerCapture(event.pointerId)"
                        yield send src ["event.pointerType";"event.which"; "x|0"; "y|0"; "event.shiftKey"; "event.altKey"; "event.ctrlKey"]

                    ]
                serverSide = fun client src args -> 
                    match args with
                        | pointertypestr :: which :: x :: y :: shift :: alt :: ctrl :: _ ->
                            let v : V2i = V2i(int x, int y)
                            let button = if needButton then MouseButtons.ofEventStr which else MouseButtons.None
                            let modifiers = KeyModifiers.ofString shift alt ctrl
                            let pointertype = PointerType.ofString pointertypestr
                            Seq.singleton (cb pointertype modifiers button v)
                        | _ ->
                            Seq.empty
            }

    let onPointerEvent name (needButton : bool) (preventDefault : Option<int>) (useCapture : Option<bool>) (f : PointerType -> MouseButtons -> V2i -> 'msg) =
        onPointerEventModifiers name needButton preventDefault useCapture (fun t _ b p -> f t b p)

    let onCapturedPointerDown preventDefault cb  =
        onPointerEvent "onpointerdown" true preventDefault (Some true) cb

    let onCapturedPointerDownModifiers preventDefault cb  =
        onPointerEventModifiers "onpointerdown" true preventDefault (Some true) cb

    let onCapturedPointerUp preventDefault cb =
        onPointerEvent "onpointerup" true preventDefault (Some false) cb

    let onCapturedPointerUpModifiers preventDefault cb =
        onPointerEventModifiers "onpointerup" true preventDefault (Some false) cb

    let onCapturedPointerMove preventDefault cb =
        onPointerEvent "onpointermove" false preventDefault None (fun t _ v -> cb t v)

    let onCapturedPointerMoveModifiers preventDefault cb =
        onPointerEventModifiers "onpointermove" false preventDefault None (fun t _ v -> cb t v)