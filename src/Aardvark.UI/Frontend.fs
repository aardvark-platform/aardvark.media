namespace Aardvark.UI.Frontend

open Aardvark.UI
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application

type HTMLPointerType =
    | Unknown = 0
    | Mouse = 1
    | Touch = 2
    | Pen = 3

[<AutoOpen>]
module private Patterns =
    
    let (|String|_|) (value : string) =
        try 
            let t : string = Newtonsoft.Json.JsonConvert.DeserializeObject<string> value
            Some t
        with _ ->
            None

    let (|Button|_|) (value : string) =
        match System.Int32.TryParse value with
        | (true, v) -> 
            match v with
            | 0 -> Some MouseButtons.Left
            | 1 -> Some MouseButtons.Middle
            | 2 -> Some MouseButtons.Right
            | _ -> Some MouseButtons.None
        | _ ->
            None

    let (|Key|_|) (value : string) =
        match System.Int32.TryParse value with
        | (true, v) -> 
            let key = KeyConverter.keyFromVirtualKey v
            Some key
        | _ ->
            None

    let (|Buttons|_|) (value : string) =
        match System.Int32.TryParse value with
        | (true, v) -> 
            let mutable res = MouseButtons.None
            if v &&& 1 <> 0 then res <- res ||| MouseButtons.Left
            if v &&& 2 <> 0 then res <- res ||| MouseButtons.Right
            if v &&& 4 <> 0 then res <- res ||| MouseButtons.Middle
            Some res
        | _ ->
            None
            
    let (|PointerType|_|) (v : string) =
        match v with
        | String v ->
            match v.Trim().ToLower() with
            | "mouse" -> Some HTMLPointerType.Mouse
            | "touch" -> Some HTMLPointerType.Touch
            | "pen" -> Some HTMLPointerType.Pen
            | _ -> Some HTMLPointerType.Unknown
        | _ ->
            None

    let inline (|Int|_|) (v : string) =
        match System.Int32.TryParse v with
        | (true, v) -> Some v
        | _ -> None

    let inline (|Float|_|) (v : string) =
        match System.Double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)  with
        | (true, v) -> Some v
        | _ -> None

    let inline (|Bool|_|) (v : string) =
        match v.ToLower().Trim() with
        | "true" -> Some true
        | "false" -> Some false
        | _ -> None


type HTMLEvent (targetId : string, eventType : string) =
    member x.Target = targetId
    member x.Type = eventType

type HTMLPointerEvent(targetId : string, eventType : string,
                      button : MouseButtons, buttons : MouseButtons, 
                      pointerType : HTMLPointerType, pointerId : int, 
                      pointerSize : V2d,
                      wheelDelta : float,
                      client : V2d, page : V2d, screen : V2d, move : V2d,
                      altKey : bool, ctrlKey: bool, shiftKey: bool, metaKey : bool, 
                      viewport : V2d) =
    inherit HTMLEvent(targetId, eventType)

    //let button = match pointerType with | HTMLPointerType.Mouse -> button | _ -> MouseButtons.None
    //let buttons = match pointerType with | HTMLPointerType.Mouse -> buttons | _ -> MouseButtons.None

    member x.Ndc =
        V2d(
            2.0 * (client.X + 0.5) / viewport.X - 1.0,
            1.0 - 2.0 * (client.Y + 0.5) / viewport.Y
        )

    member x.WheelDelta = wheelDelta

    member x.Button = button
    member x.Buttons = buttons
    member x.ClientLocation = client
    member x.PageLocation = page
    member x.ScreenLocation = screen
    member x.Alt = altKey
    member x.Ctrl = ctrlKey
    member x.Shift = shiftKey
    member x.Meta = metaKey
    member x.ViewportSize = viewport
    member x.PointerSize = pointerSize
    member x.Movement = move
    member x.PointerType = pointerType
    member x.PointerId = pointerId

    override x.ToString() =
        String.concat "; " [
            sprintf "targetId: %s" targetId
            sprintf "button: %s" (
                if button = MouseButtons.Left then "left"
                elif button = MouseButtons.Middle then "middle"
                elif button = MouseButtons.Right then "right"
                else "none"
            )

            sprintf "buttons: %s" (
                String.concat ", " [ 
                    if buttons &&& MouseButtons.Left <> MouseButtons.None then yield "left"
                    if buttons &&& MouseButtons.Middle <> MouseButtons.None then yield "middle"
                    if buttons &&& MouseButtons.Right <> MouseButtons.None then yield "right"
                ]
            )

            sprintf "pointerId: %d" pointerId
            sprintf "pointerType: %A" pointerType
            sprintf "wheelDelta: %A" wheelDelta

            sprintf "client: %A" client
            sprintf "page: %A" page
            sprintf "screen: %A" screen
            
            sprintf "alt: %A" altKey
            sprintf "shift: %A" shiftKey
            sprintf "ctrl: %A" ctrlKey
            sprintf "meta: %A" metaKey
            sprintf "size: %A" viewport
        ] |> sprintf "%s { %s }" x.Type


    static member internal Pickle (hasButton : bool, capture : bool, send : list<string> -> string) =
        let id = newId() |> string
        let str = 
            String.concat "" [
                sprintf "const rect__ID__ = this.getBoundingClientRect();"

                match capture with
                | true -> 
                    "(function(self, event){"
                    "    self.focus();"
                    "    if(event.buttons == 0) return;"
                    "    const downButton = event.button;"
                    "    const downPointer = event.pointerId;"
                    "    self.requestPointerLock();"
                    "    let blubber = "
                    "       { handleEvent: function(evt) { "
                    "             if(evt.pointerId != downPointer) return;"
                    "             if(evt.buttons == 0) document.exitPointerLock();"
                    "             if(evt.button == downButton) { self.removeEventListener('pointerup', blubber, true); }"
                    "         }"
                    "       };"
                    "    self.addEventListener('pointerup', blubber, true);"
                    "})(this, event);"
                | false -> 
                    ()

                "event.preventDefault();"

                send [
                    "this.id || ''"
                    (if hasButton then "event.button" else "-1"); "event.buttons || 0"; 
                    "event.pointerType || ''"; "event.pointerId || 0";
                    "event.width || 0"; "event.height || 0";
                    "(event.clientX || 0) - (rect__ID__.left || 0)"; "(event.clientY || 0) - (rect__ID__.top || 0)"
                    "event.pageX || 0"; "event.pageY || 0"
                    "event.screenX || 0"; "event.screenY || 0"
                    "event.movementX || 0"; "event.movementY || 0"
                    "event.altKey || false"; "event.ctrlKey || false"; "event.shiftKey || false"; "event.metaKey || false"
                    "rect__ID__.width || 0"; "rect__ID__.height || 0";
                    "(event.deltaY || 0) / -100"
                ]
            ]
        str.Replace("__ID__", id)

    static member internal Unpickle (eventType : string, args : list<string>) =
        match args with
        | [String targetId; Button button; Buttons buttons; 
           PointerType pointerType; Int pointerId;
           Float pw; Float ph;
           Float clientX; Float clientY; Float pageX; Float pageY; Float screenX; Float screenY;
           Float moveX; Float moveY;
           Bool altKey; Bool ctrlKey; Bool shiftKey; Bool metaKey;
           Float width; Float height; Float wheelDelta
          ] ->  
            Some (
                HTMLPointerEvent(
                    targetId, eventType, 
                    button, buttons, 
                    pointerType, pointerId, 
                    V2d(pw, ph),
                    wheelDelta, 
                    V2d(clientX, clientY),
                    V2d(pageX, pageY),
                    V2d(screenX, screenY),
                    V2d(moveX, moveY),
                    altKey, ctrlKey, shiftKey, metaKey,
                    V2d(width, height)
                )
            )
        | _ -> 
            None
    
type HTMLKeyLocation =
    | Standard = 0
    | Left = 1
    | Right = 2
    | NumPad = 3
    | Mobile = 4
    | Joystick = 5

type HTMLKeyEvent(targetId : string, eventType : string,
                  key : Aardvark.Application.Keys, location : HTMLKeyLocation, repeat : bool,
                  altKey : bool, ctrlKey: bool, shiftKey: bool, metaKey : bool
                  ) =
    inherit HTMLEvent(targetId, eventType)
    
    member x.Key = key
    member x.Repeat = repeat
    member x.Alt = altKey
    member x.Ctrl = ctrlKey
    member x.Shift = shiftKey
    member x.Meta = metaKey
    
    override x.ToString() =
        sprintf "%s { target: %s; key: %A; location: %A; repeat: %A; alt: %A; ctrl: %A; shift: %A; meta: %A }" x.Type targetId key location repeat altKey ctrlKey shiftKey metaKey

    static member internal Pickle (send : list<string> -> string) =
        String.concat "" [
            
            "event.preventDefault();"

            send [
                "this.id || ''"
                "event.keyCode || 0"; "event.location || 0"
                "event.repeat || false"
                "event.altKey || false"; "event.ctrlKey || false"; "event.shiftKey || false"; "event.metaKey || false"
            ]
        
        ]

    static member internal Unpickle (eventType : string, args : list<string>) =
        match args with
        | [String id; Key key; Int loc; Bool repeat; Bool altKey; Bool ctrlKey; Bool shiftKey; Bool metaKey] ->
            
            Some (
                HTMLKeyEvent(
                    id, eventType, key, unbox loc, repeat, altKey, ctrlKey, shiftKey, metaKey
                )
            )
        | _ ->
            None

type HTMLRenderedEvent(targetId : string, viewportSize : V2i, samples : int) =
    inherit HTMLEvent(targetId, "Rendered")
    member x.ViewportSize = viewportSize
    member x.Samples = samples

    override x.ToString() =
        sprintf "%s { target: %s; viewportSize: %A; samples: %A }" x.Type targetId viewportSize samples







/// <summary>
/// HTML5 Attributes according to: <br />
/// <a href="https://www.w3schools.com/tags/ref_attributes.asp">w3schools.com</a>
/// </summary>
type HTMLAttribute<'msg> =
    /// <summary>
    /// &lt;input&gt;
    /// Specifies the types of files that the server accepts (only for type="file")
    /// </summary>
    | Accept of mimeTypes : list<string>
    /// <summary>
    /// &lt;form&gt;
    /// Specifies the character encodings that are to be used for the form submission
    /// </summary>
    | AcceptCharset of charset : string
    /// <summary>
    /// Global Attributes
    /// Specifies a shortcut key to activate/focus an element
    /// </summary>
    | AccessKey of key : string
    /// <summary>
    /// &lt;form&gt;
    /// Specifies where to send the form-data when a form is submitted
    /// </summary>
    | Action of action : string
    /// <summary>
    /// &lt;area&gt;, &lt;img&gt;, &lt;input&gt;
    /// Specifies an alternate text when the original element fails to display
    /// </summary>
    | Alt of alt : string
    /// <summary>
    /// &lt;script&gt;
    /// Specifies that the script is executed asynchronously (only for external scripts)
    /// </summary>
    | Async
    /// <summary>
    /// &lt;form&gt;, &lt;input&gt;
    /// Specifies whether the &lt;form&gt; or the &lt;input&gt; element should have autocomplete enabled
    /// </summary>
    | Autocomplete of bool
    /// <summary>
    /// &lt;button&gt;, &lt;input&gt;, &lt;select&gt;, &lt;textarea&gt;
    /// Specifies that the element should automatically get focus when the page loads
    /// </summary>
    | Autofocus of bool
    /// <summary>
    /// &lt;audio&gt;, &lt;video&gt;
    /// Specifies that the audio/video will start playing as soon as it is ready
    /// </summary>
    | Autoplay
    /// <summary>
    /// &lt;meta&gt;, &lt;script&gt;
    /// Specifies the character encoding
    /// </summary>
    | Charset of charset : string
    /// <summary>
    /// &lt;input&gt;
    /// Specifies that an &lt;input&gt; element should be pre-selected when the page loads (for type="checkbox" or type="radio")
    /// </summary>
    | Checked
    /// <summary>
    /// &lt;blockquote&gt;, &lt;del&gt;, &lt;ins&gt;, &lt;q&gt;
    /// Specifies a URL which explains the quote/deleted/inserted text
    /// </summary>
    | Cite of url : string
    /// <summary>
    /// Global Attributes
    /// Specifies one or more classnames for an element (refers to a class in a style sheet)
    /// </summary>
    | Class of clazz : string
    /// <summary>
    /// &lt;textarea&gt;
    /// Specifies the visible width of a text area
    /// </summary>
    | Cols of int
    /// <summary>
    /// &lt;td&gt;, &lt;th&gt;
    /// Specifies the number of columns a table cell should span
    /// </summary>
    | Colspan of int
    /// <summary>
    /// &lt;meta&gt;
    /// Gives the value associated with the http-equiv or name attribute
    /// </summary>
    | Content of string
    /// <summary>
    /// Global Attributes
    /// Specifies whether the content of an element is editable or not
    /// </summary>
    | ContentEditable of bool
    /// <summary>
    /// &lt;audio&gt;, &lt;video&gt;
    /// Specifies that audio/video controls should be displayed (such as a play/pause button etc)
    /// </summary>
    | Controls
    /// <summary>
    /// &lt;area&gt;
    /// Specifies the coordinates of the area
    /// </summary>
    | Coords of string
    /// <summary>
    /// &lt;object&gt;
    /// Specifies the URL of the resource to be used by the object
    /// </summary>
    | Data of url : string
    /// <summary>
    /// &lt;del&gt;, &lt;ins&gt;, &lt;time&gt;
    /// Specifies the date and time
    /// </summary>
    | Datetime of System.DateTime
    /// <summary>
    /// &lt;track&gt;
    /// Specifies that the track is to be enabled if the user's preferences do not indicate that another track would be more appropriate
    /// </summary>
    | Default
    /// <summary>
    /// &lt;script&gt;
    /// Specifies that the script is executed when the page has finished parsing (only for external scripts)
    /// </summary>
    | Defer
    /// <summary>
    /// Global Attributes
    /// Specifies the text direction for the content in an element
    /// </summary>
    | Dir of dir : string
    /// <summary>
    /// &lt;input&gt;, &lt;textarea&gt;
    /// Specifies that the text direction will be submitted
    /// </summary>
    | Dirname of dir : string
    /// <summary>
    /// &lt;button&gt;, &lt;fieldset&gt;, &lt;input&gt;, &lt;optgroup&gt;, &lt;option&gt;, &lt;select&gt;, &lt;textarea&gt;
    /// Specifies that the specified element/group of elements should be disabled
    /// </summary>
    | Disabled
    /// <summary>
    /// &lt;a&gt;, &lt;area&gt;
    /// Specifies that the target will be downloaded when a user clicks on the hyperlink
    /// </summary>
    | Download of url : string
    /// <summary>
    /// Global Attributes
    /// Specifies whether an element is draggable or not
    /// </summary>
    | Draggable of bool
    /// <summary>
    /// Global Attributes
    /// Specifies whether the dragged data is copied, moved, or linked, when dropped
    /// </summary>
    | Dropzone of string
    /// <summary>
    /// &lt;form&gt;
    /// Specifies how the form-data should be encoded when submitting it to the server (only for method="post")
    /// </summary>
    | Enctype of string
    /// <summary>
    /// &lt;label&gt;, &lt;output&gt;
    /// Specifies which form element(s) a label/calculation is bound to
    /// </summary>
    | For of selector : string
    /// <summary>
    /// &lt;button&gt;, &lt;fieldset&gt;, &lt;input&gt;, &lt;label&gt;, &lt;meter&gt;, &lt;object&gt;, &lt;output&gt;, &lt;select&gt;, &lt;textarea&gt;
    /// Specifies the name of the form the element belongs to
    /// </summary>
    | Form of string
    /// <summary>
    /// &lt;button&gt;, &lt;input&gt;
    /// Specifies where to send the form-data when a form is submitted. Only for type="submit"
    /// </summary>
    | FormAction of string
    /// <summary>
    /// &lt;td&gt;, &lt;th&gt;
    /// Specifies one or more headers cells a cell is related to
    /// </summary>
    | Headers of list<string>
    /// <summary>
    /// &lt;canvas&gt;, &lt;embed&gt;, &lt;iframe&gt;, &lt;img&gt;, &lt;input&gt;, &lt;object&gt;, &lt;video&gt;
    /// Specifies the height of the element
    /// </summary>
    | Height of int
    /// <summary>
    /// Global Attributes
    /// Specifies that an element is not yet, or is no longer, relevant
    /// </summary>
    | Hidden
    /// <summary>
    /// &lt;meter&gt;
    /// Specifies the range that is considered to be a high value
    /// </summary>
    | High of float
    /// <summary>
    /// &lt;a&gt;, &lt;area&gt;, &lt;base&gt;, &lt;link&gt;
    /// Specifies the URL of the page the link goes to
    /// </summary>
    | Href of url : string
    /// <summary>
    /// &lt;a&gt;, &lt;area&gt;, &lt;link&gt;
    /// Specifies the language of the linked document
    /// </summary>
    | HrefLang of lang : string
    /// <summary>
    /// &lt;meta&gt;
    /// Provides an HTTP header for the information/value of the content attribute
    /// </summary>
    | HttpEquiv of string
    /// <summary>
    /// Global Attributes
    /// Specifies a unique id for an element
    /// </summary>
    | Id of string
    /// <summary>
    /// &lt;img&gt;
    /// Specifies an image as a server-side image-map
    /// </summary>
    | IsMap
    /// <summary>
    /// &lt;track&gt;
    /// Specifies the kind of text track
    /// </summary>
    | Kind of string
    /// <summary>
    /// &lt;track&gt;, &lt;option&gt;, &lt;optgroup&gt;
    /// Specifies the title of the text track
    /// </summary>
    | Label of string
    /// <summary>
    /// Global Attributes
    /// Specifies the language of the element's content
    /// </summary>
    | Lang of string
    /// <summary>
    /// &lt;input&gt;
    /// Refers to a &lt;datalist&gt; element that contains pre-defined options for an &lt;input&gt; element
    /// </summary>
    | List of string
    /// <summary>
    /// &lt;audio&gt;, &lt;video&gt;
    /// Specifies that the audio/video will start over again, every time it is finished
    /// </summary>
    | Loop
    /// <summary>
    /// &lt;meter&gt;
    /// Specifies the range that is considered to be a low value
    /// </summary>
    | Low of float
    /// <summary>
    /// &lt;input&gt;, &lt;meter&gt;, &lt;progress&gt;
    /// Specifies the maximum value
    /// </summary>
    | Max of float
    /// <summary>
    /// &lt;input&gt;, &lt;textarea&gt;
    /// Specifies the maximum number of characters allowed in an element
    /// </summary>
    | MaxLength of int
    /// <summary>
    /// &lt;a&gt;, &lt;area&gt;, &lt;link&gt;, &lt;source&gt;, &lt;style&gt;
    /// Specifies what media/device the linked document is optimized for
    /// </summary>
    | Media of string
    /// <summary>
    /// &lt;form&gt;
    /// Specifies the HTTP method to use when sending form-data
    /// </summary>
    | Method of string
    /// <summary>
    /// &lt;input&gt;, &lt;meter&gt;
    /// Specifies a minimum value
    /// </summary>
    | Min of float
    /// <summary>
    /// &lt;input&gt;, &lt;select&gt;
    /// Specifies that a user can enter more than one value
    /// </summary>
    | Multiple
    /// <summary>
    /// &lt;video&gt;, &lt;audio&gt;
    /// Specifies that the audio output of the video should be muted
    /// </summary>
    | Muted
    /// <summary>
    /// &lt;button&gt;, &lt;fieldset&gt;, &lt;form&gt;, &lt;iframe&gt;, &lt;input&gt;, &lt;map&gt;, &lt;meta&gt;, &lt;object&gt;, &lt;output&gt;, &lt;param&gt;, &lt;select&gt;, &lt;textarea&gt;
    /// Specifies the name of the element
    /// </summary>
    | Name of string
    /// <summary>
    /// &lt;form&gt;
    /// Specifies that the form should not be validated when submitted
    /// </summary>
    | NoValidate

    /// &lt;details&gt;
    /// Specifies that the details should be visible (open) to the user
    /// </summary>
    | Open
    /// <summary>
    /// &lt;meter&gt;
    /// Specifies what value is the optimal value for the gauge
    /// </summary>
    | Optimum of float
    /// <summary>
    /// &lt;input&gt;
    /// Specifies a regular expression that an &lt;input&gt; element's value is checked against
    /// </summary>
    | Pattern of string
    /// <summary>
    /// &lt;input&gt;, &lt;textarea&gt;
    /// Specifies a short hint that describes the expected value of the element
    /// </summary>
    | Placeholder of string
    /// <summary>
    /// &lt;video&gt;
    /// Specifies an image to be shown while the video is downloading, or until the user hits the play button
    /// </summary>
    | Poster of url : string
    /// <summary>
    /// &lt;audio&gt;, &lt;video&gt;
    /// Specifies if and how the author thinks the audio/video should be loaded when the page loads
    /// </summary>
    | Preload of string
    /// <summary>
    /// &lt;input&gt;, &lt;textarea&gt;
    /// Specifies that the element is read-only
    /// </summary>
    | ReadOnly
    /// <summary>
    /// &lt;a&gt;, &lt;area&gt;, &lt;link&gt;
    /// Specifies the relationship between the current document and the linked document
    /// </summary>
    | Rel of string
    /// <summary>
    /// &lt;input&gt;, &lt;select&gt;, &lt;textarea&gt;
    /// Specifies that the element must be filled out before submitting the form
    /// </summary>
    | Required
    /// <summary>
    /// &lt;ol&gt;
    /// Specifies that the list order should be descending (9,8,7...)
    /// </summary>
    | Reversed
    /// <summary>
    /// &lt;textarea&gt;
    /// Specifies the visible number of lines in a text area
    /// </summary>
    | Rows of int
    /// <summary>
    /// &lt;td&gt;, &lt;th&gt;
    /// Specifies the number of rows a table cell should span
    /// </summary>
    | RowSpan of int
    /// <summary>
    /// &lt;iframe&gt;
    /// Enables an extra set of restrictions for the content in an &lt;iframe&gt;
    /// </summary>
    | Sandbox
    /// <summary>
    /// &lt;th&gt;
    /// Specifies whether a header cell is a header for a column, row, or group of columns or rows
    /// </summary>
    | Scope of bool
    /// <summary>
    /// &lt;option&gt;
    /// Specifies that an option should be pre-selected when the page loads
    /// </summary>
    | Selected
    /// <summary>
    /// &lt;area&gt;
    /// Specifies the shape of the area
    /// </summary>
    | Shape of string
    /// <summary>
    /// &lt;input&gt;, &lt;select&gt;
    /// Specifies the width, in characters (for &lt;input&gt;) or specifies the number of visible options (for &lt;select&gt;)
    /// </summary>
    | Size of int
    /// <summary>
    /// &lt;img&gt;, &lt;link&gt;, &lt;source&gt;
    /// Specifies the size of the linked resource
    /// </summary>
    | Sizes of string
    /// <summary>
    /// &lt;col&gt;, &lt;colgroup&gt;
    /// Specifies the number of columns to span
    /// </summary>
    | Span of int
    /// <summary>
    /// Global Attributes
    /// Specifies whether the element is to have its spelling and grammar checked or not
    /// </summary>
    | Spellcheck
    /// <summary>
    /// &lt;audio&gt;, &lt;embed&gt;, &lt;iframe&gt;, &lt;img&gt;, &lt;input&gt;, &lt;script&gt;, &lt;source&gt;, &lt;track&gt;, &lt;video&gt;
    /// Specifies the URL of the media file
    /// </summary>
    | Src of string
    /// <summary>
    /// &lt;iframe&gt;
    /// Specifies the HTML content of the page to show in the &lt;iframe&gt;
    /// </summary>
    | SrcDoc of string
    /// <summary>
    /// &lt;track&gt;
    /// Specifies the language of the track text data (required if kind="subtitles")
    /// </summary>
    | SrcLang of string
    /// <summary>
    /// &lt;img&gt;, &lt;source&gt;
    /// Specifies the URL of the image to use in different situations
    /// </summary>
    | SrcSet of string
    /// <summary>
    /// &lt;ol&gt;
    /// Specifies the start value of an ordered list
    /// </summary>
    | Start of int
    /// <summary>
    /// &lt;input&gt;
    /// Specifies the legal number intervals for an input field
    /// </summary>
    | Step of float
    /// <summary>
    /// Global Attributes
    /// Specifies an inline CSS style for an element
    /// </summary>
    | Style of string
    /// <summary>
    /// Global Attributes
    /// Specifies the tabbing order of an element
    /// </summary>
    | TabIndex of int
    /// <summary>
    /// &lt;a&gt;, &lt;area&gt;, &lt;base&gt;, &lt;form&gt;
    /// Specifies the target for where to open the linked document or where to submit the form
    /// </summary>
    | Target of string
    /// <summary>
    /// Global Attributes
    /// Specifies extra information about an element
    /// </summary>
    | Title of string
    /// <summary>
    /// Global Attributes
    /// Specifies whether the content of an element should be translated or not
    /// </summary>
    | Translate of bool
    /// <summary>
    /// &lt;a&gt;, &lt;button&gt;, &lt;embed&gt;, &lt;input&gt;, &lt;link&gt;, &lt;menu&gt;, &lt;object&gt;, &lt;script&gt;, &lt;source&gt;, &lt;style&gt;
    /// Specifies the type of element
    /// </summary>
    | Type of string
    /// <summary>
    /// &lt;img&gt;, &lt;object&gt;
    /// Specifies an image as a client-side image-map
    /// </summary>
    | UseMap of string
    /// <summary>
    /// &lt;button&gt;, &lt;input&gt;, &lt;li&gt;, &lt;option&gt;, &lt;meter&gt;, &lt;progress&gt;, &lt;param&gt;
    /// Specifies the value of the element
    /// </summary>
    | Value of string
    /// <summary>
    /// &lt;canvas&gt;, &lt;embed&gt;, &lt;iframe&gt;, &lt;img&gt;, &lt;input&gt;, &lt;object&gt;, &lt;video&gt;
    /// Specifies the width of the element
    /// </summary>
    | Width of int
    /// <summary>
    /// &lt;textarea&gt;
    /// Specifies how the text in a text area is to be wrapped when submitted in a form
    /// </summary>
    | Wrap of string

    
    /// All visible elements.
    /// Script to be run when a mouse button is pressed down on an element
    | OnPointerDownEvt of capture : bool * pointerCapture : bool * callback : (HTMLPointerEvent -> Continuation * seq<'msg>)

    /// All visible elements.
    /// Script to be run as long as the  mouse pointer is moving over an element
    | OnPointerMoveEvt  of capture : bool * callback : (HTMLPointerEvent -> Continuation * seq<'msg>)

    /// All visible elements.
    /// Script to be run when a mouse button is released over an element
    | OnPointerUpEvt of capture : bool * callback : (HTMLPointerEvent -> Continuation * seq<'msg>)

    /// All visible elements.
    /// Script to be run when a mouse button is clicked on an element
    | OnPointerClickEvt of capture : bool * callback : (HTMLPointerEvent -> Continuation * seq<'msg>)
    
    /// All visible elements.
    /// Script to be run when a mouse button is clicked twice on an element
    | OnPointerDoubleClickEvt of capture : bool * callback : (HTMLPointerEvent -> Continuation * seq<'msg>)
    
    /// All visible elements.
    /// Script to be run when a pointer enters the elements bounds
    | OnPointerEnterEvt of callback : (HTMLPointerEvent -> Continuation * seq<'msg>)
    
    /// All visible elements.
    /// Script to be run when a pointer leaves the elements bounds
    | OnPointerLeaveEvt of callback : (HTMLPointerEvent -> Continuation * seq<'msg>)
    
    /// All visible elements.
    /// Script to be run when a pointer-wheel is changed
    | OnPointerWheelEvt of capture : bool * callback : (HTMLPointerEvent -> Continuation * seq<'msg>)

    | OnKeyDownEvt of capture : bool * callback : (HTMLKeyEvent -> Continuation * seq<'msg>)
    | OnKeyUpEvt of capture : bool * callback : (HTMLKeyEvent -> Continuation * seq<'msg>)

    | OnRenderedEvt of callback : (HTMLRenderedEvent -> Continuation * seq<'msg>)

    | OnClickEvt of capture : bool * callback : (HTMLEvent -> Continuation * seq<'msg>)






    static member OnPointerDown(callback : HTMLPointerEvent -> Continuation * seq<'msg>) =
        OnPointerDownEvt(false, false, callback)
        
    static member OnPointerDown(callback : HTMLPointerEvent -> Continuation * list<'msg>) =
        OnPointerDownEvt(false, false, fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnPointerDown(callback : HTMLPointerEvent -> Continuation * option<'msg>) =
        OnPointerDownEvt(false, false, fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnPointerDown(callback : HTMLPointerEvent -> seq<'msg>) =
        OnPointerDownEvt(false, false, fun e -> Continue, callback e)
        
    static member OnPointerDown(callback : HTMLPointerEvent -> list<'msg>) =
        OnPointerDownEvt(false, false, fun e -> Continue, callback e :> seq<_>)

    static member OnPointerDown(callback : HTMLPointerEvent -> option<'msg>) =
        OnPointerDownEvt(false, false, fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))
        

    static member OnPointerUp(callback : HTMLPointerEvent -> Continuation * seq<'msg>) =
        OnPointerUpEvt(false, callback)
        
    static member OnPointerUp(callback : HTMLPointerEvent -> Continuation * list<'msg>) =
        OnPointerUpEvt(false, fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnPointerUp(callback : HTMLPointerEvent -> Continuation * option<'msg>) =
        OnPointerUpEvt(false, fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnPointerUp(callback : HTMLPointerEvent -> seq<'msg>) =
        OnPointerUpEvt(false, fun e -> Continue, callback e)

    static member OnPointerUp(callback : HTMLPointerEvent -> list<'msg>) =
        OnPointerUpEvt(false, fun e -> Continue, callback e :> seq<_>)
        
    static member OnPointerUp(callback : HTMLPointerEvent -> option<'msg>) =
        OnPointerUpEvt(false, fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))
        

    static member OnPointerMove(callback : HTMLPointerEvent -> Continuation * seq<'msg>) =
        OnPointerMoveEvt(false, callback)

    static member OnPointerMove(callback : HTMLPointerEvent -> Continuation * list<'msg>) =
        OnPointerMoveEvt(false, fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnPointerMove(callback : HTMLPointerEvent -> Continuation * option<'msg>) =
        OnPointerMoveEvt(false, fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnPointerMove(callback : HTMLPointerEvent -> seq<'msg>) =
        OnPointerMoveEvt(false, fun e -> Continue, callback e)
        
    static member OnPointerMove(callback : HTMLPointerEvent -> list<'msg>) =
        OnPointerMoveEvt(false, fun e -> Continue, callback e :> seq<_>)
        
    static member OnPointerMove(callback : HTMLPointerEvent -> option<'msg>) =
        OnPointerMoveEvt(false, fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))

        
    static member OnPointerEnter(callback : HTMLPointerEvent -> Continuation * seq<'msg>) =
        OnPointerEnterEvt(callback)

    static member OnPointerEnter(callback : HTMLPointerEvent -> Continuation * list<'msg>) =
        OnPointerEnterEvt(fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnPointerEnter(callback : HTMLPointerEvent -> Continuation * option<'msg>) =
        OnPointerEnterEvt(fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnPointerEnter(callback : HTMLPointerEvent -> seq<'msg>) =
        OnPointerEnterEvt(fun e -> Continue, callback e)
        
    static member OnPointerEnter(callback : HTMLPointerEvent -> list<'msg>) =
        OnPointerEnterEvt(fun e -> Continue, callback e :> seq<_>)
        
    static member OnPointerEnter(callback : HTMLPointerEvent -> option<'msg>) =
        OnPointerEnterEvt(fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))

        
    static member OnPointerLeave(callback : HTMLPointerEvent -> Continuation * seq<'msg>) =
        OnPointerLeaveEvt(callback)

    static member OnPointerLeave(callback : HTMLPointerEvent -> Continuation * list<'msg>) =
        OnPointerLeaveEvt(fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnPointerLeave(callback : HTMLPointerEvent -> Continuation * option<'msg>) =
        OnPointerLeaveEvt(fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnPointerLeave(callback : HTMLPointerEvent -> seq<'msg>) =
        OnPointerLeaveEvt(fun e -> Continue, callback e)
        
    static member OnPointerLeave(callback : HTMLPointerEvent -> list<'msg>) =
        OnPointerLeaveEvt(fun e -> Continue, callback e :> seq<_>)
        
    static member OnPointerLeave(callback : HTMLPointerEvent -> option<'msg>) =
        OnPointerLeaveEvt(fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))


        
    static member OnPointerClick(callback : HTMLPointerEvent -> Continuation * seq<'msg>) =
        OnPointerClickEvt(false, callback)
        
    static member OnPointerClick(callback : HTMLPointerEvent -> Continuation * list<'msg>) =
        OnPointerClickEvt(false, fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnPointerClick(callback : HTMLPointerEvent -> Continuation * option<'msg>) =
        OnPointerClickEvt(false, fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnPointerClick(callback : HTMLPointerEvent -> seq<'msg>) =
        OnPointerClickEvt(false, fun e -> Continue, callback e)

    static member OnPointerClick(callback : HTMLPointerEvent -> list<'msg>) =
        OnPointerClickEvt(false, fun e -> Continue, callback e :> seq<_>)
        
    static member OnPointerClick(callback : HTMLPointerEvent -> option<'msg>) =
        OnPointerClickEvt(false, fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))
        
        
    static member OnPointerDoubleClick(callback : HTMLPointerEvent -> Continuation * seq<'msg>) =
        OnPointerDoubleClickEvt(false, callback)
        
    static member OnPointerDoubleClick(callback : HTMLPointerEvent -> Continuation * list<'msg>) =
        OnPointerDoubleClickEvt(false, fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnPointerDoubleClick(callback : HTMLPointerEvent -> Continuation * option<'msg>) =
        OnPointerDoubleClickEvt(false, fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnPointerDoubleClick(callback : HTMLPointerEvent -> seq<'msg>) =
        OnPointerDoubleClickEvt(false, fun e -> Continue, callback e)

    static member OnPointerDoubleClick(callback : HTMLPointerEvent -> list<'msg>) =
        OnPointerDoubleClickEvt(false, fun e -> Continue, callback e :> seq<_>)
        
    static member OnPointerDoubleClick(callback : HTMLPointerEvent -> option<'msg>) =
        OnPointerWheelEvt(false, fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))
        
    static member OnPointerWheel(callback : HTMLPointerEvent -> Continuation * seq<'msg>) =
        OnPointerWheelEvt(false, callback)
        
    static member OnPointerWheel(callback : HTMLPointerEvent -> Continuation * list<'msg>) =
        OnPointerWheelEvt(false, fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnPointerWheel(callback : HTMLPointerEvent -> Continuation * option<'msg>) =
        OnPointerWheelEvt(false, fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnPointerWheel(callback : HTMLPointerEvent -> seq<'msg>) =
        OnPointerWheelEvt(false, fun e -> Continue, callback e)

    static member OnPointerWheel(callback : HTMLPointerEvent -> list<'msg>) =
        OnPointerWheelEvt(false, fun e -> Continue, callback e :> seq<_>)
        
    static member OnPointerWheel(callback : HTMLPointerEvent -> option<'msg>) =
        OnPointerWheelEvt(false, fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))
   

    static member OnKeyUp(callback : HTMLKeyEvent -> Continuation * seq<'msg>) =
        OnKeyUpEvt(false, callback)
        
    static member OnKeyUp(callback : HTMLKeyEvent -> Continuation * list<'msg>) =
        OnKeyUpEvt(false, fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnKeyUp(callback : HTMLKeyEvent -> Continuation * option<'msg>) =
        OnKeyUpEvt(false, fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnKeyUp(callback : HTMLKeyEvent -> seq<'msg>) =
        OnKeyUpEvt(false, fun e -> Continue, callback e)

    static member OnKeyUp(callback : HTMLKeyEvent -> list<'msg>) =
        OnKeyUpEvt(false, fun e -> Continue, callback e :> seq<_>)
        
    static member OnKeyUp(callback : HTMLKeyEvent -> option<'msg>) =
        OnKeyUpEvt(false, fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))
        

    static member OnKeyDown(callback : HTMLKeyEvent -> Continuation * seq<'msg>) =
        OnKeyDownEvt(false, callback)
        
    static member OnKeyDown(callback : HTMLKeyEvent -> Continuation * list<'msg>) =
        OnKeyDownEvt(false, fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnKeyDown(callback : HTMLKeyEvent -> Continuation * option<'msg>) =
        OnKeyDownEvt(false, fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnKeyDown(callback : HTMLKeyEvent -> seq<'msg>) =
        OnKeyDownEvt(false, fun e -> Continue, callback e)

    static member OnKeyDown(callback : HTMLKeyEvent -> list<'msg>) =
        OnKeyDownEvt(false, fun e -> Continue, callback e :> seq<_>)
        
    static member OnKeyDown(callback : HTMLKeyEvent -> option<'msg>) =
        OnKeyDownEvt(false, fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))
   
   

    static member OnRendered(callback : HTMLRenderedEvent -> Continuation * seq<'msg>) =
        OnRenderedEvt(callback)

    static member OnRendered(callback : HTMLRenderedEvent -> Continuation * list<'msg>) =
        OnRenderedEvt(fun e -> let (c, m) = callback e in c, m :> seq<_>)
        
    static member OnRendered(callback : HTMLRenderedEvent -> Continuation * option<'msg>) =
        OnRenderedEvt(fun e -> match callback e with | (cont, Some msg) -> cont, Seq.singleton msg | (cont, None) -> cont, Seq.empty)
        
    static member OnRendered(callback : HTMLRenderedEvent -> seq<'msg>) =
        OnRenderedEvt(fun e -> Continue, callback e)
        
    static member OnRendered(callback : HTMLRenderedEvent -> list<'msg>) =
        OnRenderedEvt(fun e -> Continue, callback e :> seq<_>)
        
    static member OnRendered(callback : HTMLRenderedEvent -> option<'msg>) =
        OnRenderedEvt(fun e -> Continue, (match callback e with | Some m -> Seq.singleton m | None -> Seq.empty))




    member x.WithPointerCapture =
        match x with
        | OnPointerDownEvt(capture, _, callback) -> OnPointerDownEvt(capture, true, callback)
        | _ -> x
        
    member x.WithoutPointerCapture =
        match x with
        | OnPointerDownEvt(capture, _, callback) -> OnPointerDownEvt(capture, false, callback)
        | _ -> x

    member e.Captured =
        match e with
        | OnPointerDownEvt(_, pointerCapture, callback) -> OnPointerDownEvt(true, pointerCapture, callback)
        | OnPointerUpEvt(_, callback) -> OnPointerUpEvt(true, callback)
        | OnPointerMoveEvt(_, callback) -> OnPointerMoveEvt(true, callback)
        | OnPointerClickEvt(_, callback) -> OnPointerClickEvt(true, callback)
        | OnPointerDoubleClickEvt(_, callback) -> OnPointerDoubleClickEvt(true, callback)
        | OnPointerWheelEvt(_, callback) -> OnPointerWheelEvt(true, callback)
        | OnKeyUpEvt(_, callback) -> OnKeyUpEvt(true, callback)
        | OnKeyDownEvt(_, callback) -> OnKeyDownEvt(true, callback)
        | _ -> e
        
    member e.Bubbling =
        match e with
        | OnPointerDownEvt(_, pointerCapture, callback) -> OnPointerDownEvt(false, pointerCapture, callback)
        | OnPointerUpEvt(_, callback) -> OnPointerUpEvt(false, callback)
        | OnPointerMoveEvt(_, callback) -> OnPointerMoveEvt(false, callback)
        | OnPointerClickEvt(_, callback) -> OnPointerClickEvt(false, callback)
        | OnPointerDoubleClickEvt(_, callback) -> OnPointerDoubleClickEvt(false, callback)
        | OnPointerWheelEvt(_, callback) -> OnPointerWheelEvt(false, callback)
        | OnKeyUpEvt(_, callback) -> OnKeyUpEvt(false, callback)
        | OnKeyDownEvt(_, callback) -> OnKeyDownEvt(false, callback)
        | _ -> e

    member att.ToAttributes() : list<string * AttributeValue<'msg>> =
        match att with
        | Accept mimeTypes ->           ["accept", AttributeValue.String (String.concat ", " mimeTypes)]
        | AcceptCharset charset ->      ["acceptcharset", AttributeValue.String charset]
        | AccessKey key ->              ["accesskey", AttributeValue.String key]
        | Action action ->              ["action", AttributeValue.String action]
        | Alt alt ->                    ["alt", AttributeValue.String alt]
        | Async ->                      ["async", AttributeValue.String "true"]
        | Autocomplete comp ->          ["autocomplete", AttributeValue.String (string comp)]
        | Autofocus focus ->            ["autofocus", AttributeValue.String (string focus)]
        | Autoplay ->                   ["autoplay", AttributeValue.String "true"]
        | Charset charset ->            ["charset", AttributeValue.String charset]
        | Checked ->                    ["checked", AttributeValue.String "true"]
        | Cite url ->                   ["cite", AttributeValue.String url]
        | Class clazz ->                ["class", AttributeValue.String clazz]
        | Cols cols ->                  ["cols", AttributeValue.String (string cols)]
        | Colspan cols ->               ["colspan", AttributeValue.String (string cols)]
        | Content content ->            ["content", AttributeValue.String content]
        | ContentEditable edit ->       ["contenteditable", AttributeValue.String (string edit)]
        | Controls ->                   ["controls", AttributeValue.String "true"]
        | Coords coords ->              ["coords", AttributeValue.String coords]
        | Data data ->                  ["data", AttributeValue.String data]
        | Datetime time ->              ["datetime", AttributeValue.String (time.ToUniversalTime().ToString("o"))]
        | Default ->                    ["default", AttributeValue.String "true"]
        | Defer ->                      ["defer", AttributeValue.String "true"]
        | Dir dir ->                    ["dir", AttributeValue.String dir]
        | Dirname dir ->                ["dirname", AttributeValue.String dir]
        | Disabled ->                   ["disabled", AttributeValue.String "true"]
        | Download url ->               ["download", AttributeValue.String url]
        | Draggable drag ->             ["draggable", AttributeValue.String (string drag)]
        | Dropzone drop ->              ["dropzone", AttributeValue.String drop]
        | Enctype enc ->                ["enctye", AttributeValue.String enc]
        | For name ->                   ["for", AttributeValue.String name]
        | Form name ->                  ["form", AttributeValue.String name]
        | FormAction action ->          ["formaction", AttributeValue.String action]
        | Headers headers ->            ["headers", AttributeValue.String (String.concat ", " headers)]
        | Height height ->              ["height", AttributeValue.String (string height)]
        | Hidden ->                     ["hidden", AttributeValue.String "true"]
        | High value ->                 ["high", AttributeValue.String (string value)]
        | Href url ->                   ["href", AttributeValue.String url]
        | HrefLang lang ->              ["hreflang", AttributeValue.String lang]
        | HttpEquiv equiv ->            ["http-equiv", AttributeValue.String equiv]
        | Id id ->                      ["id", AttributeValue.String id]
        | IsMap ->                      ["ismap", AttributeValue.String "true"]
        | Kind kind ->                  ["kind", AttributeValue.String kind]
        | Label title ->                ["label", AttributeValue.String title]
        | Lang lang ->                  ["lang", AttributeValue.String lang]
        | List list ->                  ["list", AttributeValue.String list]
        | Loop ->                       ["loop", AttributeValue.String "true"]
        | Low value ->                  ["low", AttributeValue.String (string value)]
        | Max value ->                  ["max", AttributeValue.String (string value)]
        | MaxLength length ->           ["maxlength", AttributeValue.String (string length)]
        | Media media ->                ["media", AttributeValue.String media]
        | Method meth ->                ["method", AttributeValue.String meth]
        | Min value ->                  ["min", AttributeValue.String (string value)]
        | Multiple ->                   ["multiple", AttributeValue.String "true"]
        | Muted ->                      ["muted", AttributeValue.String "true"]
        | Name name ->                  ["name", AttributeValue.String name]
        | NoValidate ->                 ["novalidate", AttributeValue.String "true"]
        | Open ->                       ["open", AttributeValue.String "true"]
        | Optimum value ->              ["optimium", AttributeValue.String (string value)]
        | Pattern pat ->                ["pattern", AttributeValue.String pat]
        | Placeholder value ->          ["placeholder", AttributeValue.String value]
        | Poster url ->                 ["poster", AttributeValue.String url]
        | Preload kind ->               ["preload", AttributeValue.String kind]
        | ReadOnly ->                   ["readonly", AttributeValue.String "true"]
        | Rel url ->                    ["rel", AttributeValue.String url]
        | Required ->                   ["requires", AttributeValue.String "true"]
        | Reversed ->                   ["reversed", AttributeValue.String "true"]
        | Rows rows ->                  ["rows", AttributeValue.String (string rows)]
        | RowSpan rows ->               ["rowspan", AttributeValue.String (string rows)]
        | Sandbox ->                    ["sandbox", AttributeValue.String "true"]
        | Scope scope ->                ["scope", AttributeValue.String (string scope)]
        | Selected ->                   ["selected", AttributeValue.String "true"]
        | Shape shape ->                ["shape", AttributeValue.String shape]
        | Size size ->                  ["size", AttributeValue.String (string size)]
        | Sizes sizes ->                ["sizes", AttributeValue.String sizes]
        | Span value ->                 ["span", AttributeValue.String (string value)]
        | Spellcheck ->                 ["spellcheck", AttributeValue.String "true"]
        | Src url ->                    ["src", AttributeValue.String url]
        | SrcDoc html ->                ["srcdoc", AttributeValue.String html]
        | SrcLang lang ->               ["srclang", AttributeValue.String lang]
        | SrcSet value ->               ["srcset", AttributeValue.String value]
        | Start start ->                ["start", AttributeValue.String (string start)]
        | Step value ->                 ["step", AttributeValue.String (string value)]
        | Style style ->                ["style", AttributeValue.String style]
        | TabIndex idx ->               ["tabindex", AttributeValue.String (string idx)]
        | Target target ->              ["target", AttributeValue.String target]
        | Title title ->                ["title", AttributeValue.String title]
        | Translate trans ->            ["translate", AttributeValue.String (string trans)]
        | Type typ ->                   ["type", AttributeValue.String typ]
        | UseMap map ->                 ["usemap", AttributeValue.String map]
        | Value value ->                ["value", AttributeValue.String (string value)]
        | Width width ->                ["width", AttributeValue.String (string width)]
        | Wrap mode ->                  ["wrap", AttributeValue.String mode]

        | OnPointerDownEvt(capture, pointerCapture, callback) ->
            let create = if capture then AttributeValue.Capture else AttributeValue.Bubble
            [
                "onpointerdown", create {
                    clientSide = fun send id -> HTMLPointerEvent.Pickle (true, pointerCapture, send id)
                    serverSide = fun _ _ args -> match HTMLPointerEvent.Unpickle("PointerDown", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]

        | OnPointerUpEvt(capture, callback) ->
            let create = if capture then AttributeValue.Capture else AttributeValue.Bubble
            [
                "onpointerup", create {
                    clientSide = fun send id -> HTMLPointerEvent.Pickle (true, false, send id)
                    serverSide = fun _ _ args -> match HTMLPointerEvent.Unpickle("PointerUp", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]

        | OnPointerMoveEvt(capture, callback) ->
            let create = if capture then AttributeValue.Capture else AttributeValue.Bubble
            [
                "onpointermove", create {
                    clientSide = fun send id -> HTMLPointerEvent.Pickle (false, false, send id)
                    serverSide = fun _ _ args -> match HTMLPointerEvent.Unpickle("PointerMove", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]
  
        | OnPointerEnterEvt(callback) ->
            [
                "onpointerenter", AttributeValue.Bubble {
                    clientSide = fun send id -> HTMLPointerEvent.Pickle (false, false, send id)
                    serverSide = fun _ _ args -> match HTMLPointerEvent.Unpickle("PointerEnter", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]
  
        | OnPointerLeaveEvt(callback) ->
            [
                "onpointerleave", AttributeValue.Bubble {
                    clientSide = fun send id -> HTMLPointerEvent.Pickle (false, false, send id)
                    serverSide = fun _ _ args -> match HTMLPointerEvent.Unpickle("PointerLeave", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]
  
        | OnPointerClickEvt(capture, callback) ->
            let create = if capture then AttributeValue.Capture else AttributeValue.Bubble
            [
                "onpointerclick", create {
                    clientSide = fun send id -> HTMLPointerEvent.Pickle (true, false, send id)
                    serverSide = fun _ _ args -> match HTMLPointerEvent.Unpickle("PointerClick", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]
  
        | OnPointerDoubleClickEvt(capture, callback) ->
            let create = if capture then AttributeValue.Capture else AttributeValue.Bubble
            [
                "onpointerdblclick", create {
                    clientSide = fun send id -> HTMLPointerEvent.Pickle (true, false, send id)
                    serverSide = fun _ _ args -> match HTMLPointerEvent.Unpickle("PointerDoubleClick", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]
  
        | OnPointerWheelEvt(capture, callback) ->
            let create = if capture then AttributeValue.Capture else AttributeValue.Bubble
            [
                "onwheel", create {
                    clientSide = fun send id -> HTMLPointerEvent.Pickle (false, false, send id)
                    serverSide = fun _ _ args -> match HTMLPointerEvent.Unpickle("PointerWheel", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]
            
        | OnKeyDownEvt(capture, callback) ->
            let create = if capture then AttributeValue.Capture else AttributeValue.Bubble
            [
                "onkeydown", create {
                    clientSide = fun send id -> HTMLKeyEvent.Pickle (send id)
                    serverSide = fun _ _ args -> match HTMLKeyEvent.Unpickle("KeyDown", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]
            
        | OnKeyUpEvt(capture, callback) ->
            let create = if capture then AttributeValue.Capture else AttributeValue.Bubble
            [
                "onkeyup", create {
                    clientSide = fun send id -> HTMLKeyEvent.Pickle (send id)
                    serverSide = fun _ _ args -> match HTMLKeyEvent.Unpickle("KeyUp", args) with | Some evt -> callback evt | None -> Continue, Seq.empty
                }
            ]

        | OnRenderedEvt(callback) ->
            [
                "onrendered", AttributeValue.Bubble {
                    clientSide = fun send id -> ""
                    serverSide = fun _ _ args -> 
                        match args with
                        | [String target; Int w; Int h; Int samples] ->
                            callback (HTMLRenderedEvent(target, V2i(w,h), samples))
                        | _ ->
                            Continue, Seq.empty
                }
            ]


    static member ToAttributeMap (attributes : list<HTMLAttribute<'msg>>) =
        attributes |> List.collect (fun a -> a.ToAttributes()) |> AttributeMap.ofList

    static member ToAttributeMap (attributes : alist<HTMLAttribute<'msg>>) =
        attributes |> AList.collect (fun a -> a.ToAttributes() |> AList.ofList) |> AttributeMap.ofAList
        
    static member ToAttributeMap (attributes : alist<IMod<option<HTMLAttribute<'msg>>>>) =
        attributes |> AList.collect (fun a -> 
            a |> AList.bind (function
                | Some att -> att.ToAttributes() |> AList.ofList
                | None -> AList.empty
            )
        )
        |> AttributeMap.ofAList
        
    static member ToAttributeMap (attributes : list<IMod<option<HTMLAttribute<'msg>>>>) =
        HTMLAttribute.ToAttributeMap(AList.ofList attributes)

module HTMLAttribute =
    let inline toAttributes (e : HTMLAttribute<'msg>) = e.ToAttributes()
    let inline captured (e : HTMLAttribute<'msg>) = e.Captured
    let inline bubbling (e : HTMLAttribute<'msg>) = e.Bubbling
    let inline capturePointer (e : HTMLAttribute<'msg>) = e.WithPointerCapture
    let inline noCapturePointer (e : HTMLAttribute<'msg>) = e.WithoutPointerCapture

    let onlyWhen (condition : IMod<bool>) (attribute : HTMLAttribute<'msg>) =
        condition |> Mod.map (function
            | true -> Some attribute
            | false -> None
        )
    let always (attribute : HTMLAttribute<'msg>) =
        Mod.constant (Some attribute)



[<AutoOpen>]
module HTMLAttributeExtensions = 

    let inline private onPointerDownAux (_dummy : ^a) (callback : HTMLPointerEvent -> ^b) =
        ((^a or ^b) : (static member OnPointerDown : (HTMLPointerEvent -> ^b) -> ^a) (callback))

    let inline private onPointerUpAux (_dummy : ^a) (callback : HTMLPointerEvent -> ^b) =
        ((^a or ^b) : (static member OnPointerUp : (HTMLPointerEvent -> ^b) -> ^a) (callback))

    let inline private onPointerMoveAux (_dummy : ^a) (callback : HTMLPointerEvent -> ^b) =
        ((^a or ^b) : (static member OnPointerMove : (HTMLPointerEvent -> ^b) -> ^a) (callback))
        
    let inline private onPointerEnterAux (_dummy : ^a) (callback : HTMLPointerEvent -> ^b) =
        ((^a or ^b) : (static member OnPointerEnter : (HTMLPointerEvent -> ^b) -> ^a) (callback))
        
    let inline private onPointerLeaveAux (_dummy : ^a) (callback : HTMLPointerEvent -> ^b) =
        ((^a or ^b) : (static member OnPointerLeave : (HTMLPointerEvent -> ^b) -> ^a) (callback))
        
    let inline private onPointerClickAux (_dummy : ^a) (callback : HTMLPointerEvent -> ^b) =
        ((^a or ^b) : (static member OnPointerClick : (HTMLPointerEvent -> ^b) -> ^a) (callback))
        
    let inline private onPointerDoubleClickAux (_dummy : ^a) (callback : HTMLPointerEvent -> ^b) =
        ((^a or ^b) : (static member OnPointerDoubleClick : (HTMLPointerEvent -> ^b) -> ^a) (callback))
        
    let inline private onKeyDownAux (_dummy : ^a) (callback : HTMLKeyEvent -> ^b) =
        ((^a or ^b) : (static member OnKeyDown : (HTMLKeyEvent -> ^b) -> ^a) (callback))
        
    let inline private onKeyUpAux (_dummy : ^a) (callback : HTMLKeyEvent -> ^b) =
        ((^a or ^b) : (static member OnKeyUp : (HTMLKeyEvent -> ^b) -> ^a) (callback))

    let inline OnPointerDown (callback : HTMLPointerEvent -> ^b) = 
        onPointerDownAux Unchecked.defaultof<HTMLAttribute<_>> callback
        
    let inline OnPointerUp (callback : HTMLPointerEvent -> ^b) = 
        onPointerUpAux Unchecked.defaultof<HTMLAttribute<_>> callback
        
    let inline OnPointerMove (callback : HTMLPointerEvent -> ^b) = 
        onPointerMoveAux Unchecked.defaultof<HTMLAttribute<_>> callback
        
    let inline OnPointerEnter (callback : HTMLPointerEvent -> ^b) = 
        onPointerEnterAux Unchecked.defaultof<HTMLAttribute<_>> callback
        
    let inline OnPointerLeave (callback : HTMLPointerEvent -> ^b) = 
        onPointerLeaveAux Unchecked.defaultof<HTMLAttribute<_>> callback
        
    let inline OnPointerClick (callback : HTMLPointerEvent -> ^b) = 
        onPointerClickAux Unchecked.defaultof<HTMLAttribute<_>> callback
        
    let inline OnPointerDoubleClick (callback : HTMLPointerEvent -> ^b) = 
        onPointerDoubleClickAux Unchecked.defaultof<HTMLAttribute<_>> callback
        
    let inline OnKeyDown (callback : HTMLKeyEvent -> ^b) = 
        onKeyDownAux Unchecked.defaultof<HTMLAttribute<_>> callback
        
    let inline OnKeyUp (callback : HTMLKeyEvent -> ^b) = 
        onKeyUpAux Unchecked.defaultof<HTMLAttribute<_>> callback

          
    type AttributeBuilderBase() =
        
        member x.Zero() : list<AttributeMap<'msg>> = []
        member x.Delay (action : unit -> list<AttributeMap<'msg>>) = action
        member x.Combine(l : list<AttributeMap<'msg>>, r : unit -> list<AttributeMap<'msg>>) = l @ r()

        member x.For(elements : seq<'a>, mapping : 'a -> list<AttributeMap<'msg>>) =
            elements |> Seq.toList |> List.collect mapping
            
        member x.While(guard : unit -> bool, mapping : unit -> list<AttributeMap<'msg>>) =
            if guard() then mapping() @ x.While(guard, mapping)
            else []
                
        member x.Yield (value : HTMLAttribute<'msg>) = [AttributeMap.ofList (HTMLAttribute.toAttributes value)]
        member x.Yield (value : option<HTMLAttribute<'msg>>) = match value with | Some v -> x.Yield v | None -> []
        member x.Yield (value : list<HTMLAttribute<'msg>>) = [AttributeMap.ofList (List.collect HTMLAttribute.toAttributes value)]
        member x.Yield (value : seq<HTMLAttribute<'msg>>) = [AttributeMap.ofList (List.collect HTMLAttribute.toAttributes (Seq.toList value))]

        member x.Yield (value : IMod<HTMLAttribute<'msg>>) = [value |> AMap.bind (HTMLAttribute.toAttributes >> AMap.ofList) |> AttributeMap]
        member x.Yield (value : IMod<option<HTMLAttribute<'msg>>>) = 
            [
                value |> AMap.bind (function 
                    | Some att -> (att |> HTMLAttribute.toAttributes |> AttributeMap.ofList).AMap
                    | None -> AMap.empty
                ) |> AttributeMap
            ]
        member x.Yield (value : IMod<list<HTMLAttribute<'msg>>>) = 
            [
                value |> AMap.bind (fun l -> HTMLAttribute.ToAttributeMap(l).AMap) |> AttributeMap
            ]
        member x.Yield (value : IMod<seq<HTMLAttribute<'msg>>>) = 
            [
                value |> AMap.bind (fun l -> HTMLAttribute.ToAttributeMap(Seq.toList l).AMap) |> AttributeMap
            ]

        member x.Yield (set : aset<HTMLAttribute<'msg>>) = 
            set
            |> ASet.toAList
            |> HTMLAttribute.ToAttributeMap       
            |> List.singleton
            
        member x.Yield (list : alist<HTMLAttribute<'msg>>) = 
            HTMLAttribute.ToAttributeMap list    
            |> List.singleton

        member x.Yield (map : AttributeMap<'msg>) = [map]

    type AttributeBuilder<'msg, 'r>(run : AttributeMap<'msg> -> 'r) =
        inherit AttributeBuilderBase()

        member x.Run(action : unit -> list<AttributeMap<'msg>>) = action() |> AttributeMap.unionMany |> run

            
    type AttributeBuilder() =
        inherit AttributeBuilderBase()
        
        member x.Run(action : unit -> list<AttributeMap<'msg>>) = action() |> AttributeMap.unionMany

    let att = AttributeBuilder()

    let inline onlyWhen (cond : IMod<bool>) =
        AttributeBuilder<'msg, AttributeMap<'msg>>(fun map ->
            map.AMap |> AMap.chooseM (fun _ att ->
                cond |> Mod.map (function
                    | true -> Some att
                    | false -> None
                )
            ) |> AttributeMap
        )



    let private test : AttributeMap<int> =
        att {
            OnPointerDown (fun e -> Stop, Some 1)
            Style "position: absolute"
            Width 100
            Height 100

            for i in 1 .. 10 do
                Width 100

            let mutable a = 0
            while a < 1 do
                Src "sadasdsad"
                a <- a + 1

            
            Some Autoplay
            Seq.singleton Autoplay
            [Autoplay]
            
            if a % 2 <> 0 then
                Mod.constant Autoplay
                Mod.constant (Some Autoplay)
                Mod.constant (Seq.singleton Autoplay)
                Mod.constant [ Autoplay; Alt "hugo" ]

            ASet.ofList [
                Autoplay
                Alt "hugo"
            ]
            
            AList.ofList [
                Autoplay
                Alt "hugo"
            ]
            
            alist {
                let! a = Mod.constant 10
                if a < 100 then
                    Style "asdasdasd"
            }
           
            HTMLAttribute.onlyWhen (Mod.constant true) (
                Style "border: none"
            )

            onlyWhen (Mod.constant true) {
                Style "background: red"
                OnPointerMove (fun e -> [1])
                OnPointerUp (fun e -> Stop, Some 2)
            }

        
        }

