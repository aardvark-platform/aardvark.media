namespace Aardvark.UI.Frontend

/// <summary>
/// HTML5 Attributes according to: <br />
/// <a href="https://www.w3schools.com/tags/ref_attributes.asp">w3schools.com</a>
/// </summary>
type HTMLAttribute =
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
    /// <summary>
    /// &lt;audio&gt;, &lt;embed&gt;, &lt;img&gt;, &lt;object&gt;, &lt;video&gt;
    /// Script to be run on abort
    //| onabort
    ///// &lt;body&gt;
    ///// Script to be run after the document is printed
    //| onafterprint
    ///// &lt;body&gt;
    ///// Script to be run before the document is printed
    //| onbeforeprint
    ///// &lt;body&gt;
    ///// Script to be run when the document is about to be unloaded
    //| onbeforeunload
    ///// All visible elements.
    ///// Script to be run when the element loses focus
    //| onblur
    ///// &lt;audio&gt;, &lt;embed&gt;, &lt;object&gt;, &lt;video&gt;
    ///// Script to be run when a file is ready to start playing (when it has buffered enough to begin)
    //| oncanplay
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when a file can be played all the way to the end without pausing for buffering
    //| oncanplaythrough
    ///// All visible elements.
    ///// Script to be run when the value of the element is changed
    //| onchange
    ///// All visible elements.
    ///// Script to be run when the element is being clicked
    //| onclick
    ///// All visible elements.
    ///// Script to be run when a context menu is triggered
    //| oncontextmenu
    ///// All visible elements.
    ///// Script to be run when the content of the element is being copied
    //| oncopy
    ///// &lt;track&gt;
    ///// Script to be run when the cue changes in a &lt;track&gt; element
    //| oncuechange
    ///// All visible elements.
    ///// Script to be run when the content of the element is being cut
    //| oncut
    ///// All visible elements.
    ///// Script to be run when the element is being double-clicked
    //| ondblclick
    ///// All visible elements.
    ///// Script to be run when the element is being dragged
    //| ondrag
    ///// All visible elements.
    ///// Script to be run at the end of a drag operation
    //| ondragend
    ///// All visible elements.
    ///// Script to be run when an element has been dragged to a valid drop target
    //| ondragenter
    ///// All visible elements.
    ///// Script to be run when an element leaves a valid drop target
    //| ondragleave
    ///// All visible elements.
    ///// Script to be run when an element is being dragged over a valid drop target
    //| ondragover
    ///// All visible elements.
    ///// Script to be run at the start of a drag operation
    //| ondragstart
    ///// All visible elements.
    ///// Script to be run when dragged element is being dropped
    //| ondrop
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the length of the media changes
    //| ondurationchange
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when something bad happens and the file is suddenly unavailable (like unexpectedly disconnects)
    //| onemptied
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the media has reach the end (a useful event for messages like "thanks for listening")
    //| onended
    ///// &lt;audio&gt;, &lt;body&gt;, &lt;embed&gt;, &lt;img&gt;, &lt;object&gt;, &lt;script&gt;, &lt;style&gt;, &lt;video&gt;
    ///// Script to be run when an error occurs
    //| onerror
    ///// All visible elements.
    ///// Script to be run when the element gets focus
    //| onfocus
    ///// &lt;body&gt;
    ///// Script to be run when there has been changes to the anchor part of the a URL
    //| onhashchange
    ///// All visible elements.
    ///// Script to be run when the element gets user input
    //| oninput
    ///// All visible elements.
    ///// Script to be run when the element is invalid
    //| oninvalid
    ///// All visible elements.
    ///// Script to be run when a user is pressing a key
    //| onkeydown
    ///// All visible elements.
    ///// Script to be run when a user presses a key
    //| onkeypress
    ///// All visible elements.
    ///// Script to be run when a user releases a key
    //| onkeyup
    ///// &lt;body&gt;, &lt;iframe&gt;, &lt;img&gt;, &lt;input&gt;, &lt;link&gt;, &lt;script&gt;, &lt;style&gt;
    ///// Script to be run when the element is finished loading
    //| onload
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when media data is loaded
    //| onloadeddata
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when meta data (like dimensions and duration) are loaded
    //| onloadedmetadata
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run just as the file begins to load before anything is actually loaded
    //| onloadstart
    ///// All visible elements.
    ///// Script to be run when a mouse button is pressed down on an element
    //| onmousedown
    ///// All visible elements.
    ///// Script to be run as long as the  mouse pointer is moving over an element
    //| onmousemove
    ///// All visible elements.
    ///// Script to be run when a mouse pointer moves out of an element
    //| onmouseout
    ///// All visible elements.
    ///// Script to be run when a mouse pointer moves over an element
    //| onmouseover
    ///// All visible elements.
    ///// Script to be run when a mouse button is released over an element
    //| onmouseup
    ///// All visible elements.
    ///// Script to be run when a mouse wheel is being scrolled over an element
    //| onmousewheel
    ///// &lt;body&gt;
    ///// Script to be run when the browser starts to work offline
    //| onoffline
    ///// &lt;body&gt;
    ///// Script to be run when the browser starts to work online
    //| ononline
    ///// &lt;body&gt;
    ///// Script to be run when a user navigates away from a page
    //| onpagehide
    ///// &lt;body&gt;
    ///// Script to be run when a user navigates to a page
    //| onpageshow
    ///// All visible elements.
    ///// Script to be run when the user pastes some content in an element
    //| onpaste
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the media is paused either by the user or programmatically
    //| onpause
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the media has started playing
    //| onplay
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the media has started playing
    //| onplaying
    ///// &lt;body&gt;
    ///// Script to be run when the window's history changes.
    //| onpopstate
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the browser is in the process of getting the media data
    //| onprogress
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run each time the playback rate changes (like when a user switches to a slow motion or fast forward mode).
    //| onratechange
    ///// &lt;form&gt;
    ///// Script to be run when a reset button in a form is clicked.
    //| onreset
    ///// &lt;body&gt;
    ///// Script to be run when the browser window is being resized.
    //| onresize
    ///// All visible elements.
    ///// Script to be run when an element's scrollbar is being scrolled
    //| onscroll
    ///// &lt;input&gt;
    ///// Script to be run when the user writes something in a search field (for &lt;input="search"&gt;)
    //| onsearch
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the seeking attribute is set to false indicating that seeking has ended
    //| onseeked
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the seeking attribute is set to true indicating that seeking is active
    //| onseeking
    ///// All visible elements.
    ///// Script to be run when the element gets selected
    //| onselect
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the browser is unable to fetch the media data for whatever reason
    //| onstalled
    ///// &lt;body&gt;
    ///// Script to be run when a Web Storage area is updated
    //| onstorage
    ///// &lt;form&gt;
    ///// Script to be run when a form is submitted
    //| onsubmit
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when fetching the media data is stopped before it is completely loaded for whatever reason
    //| onsuspend
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the playing position has changed (like when the user fast forwards to a different point in the media)
    //| ontimeupdate
    ///// &lt;details&gt;
    ///// Script to be run when the user opens or closes the &lt;details&gt; element
    //| ontoggle
    ///// &lt;body&gt;
    ///// Script to be run when a page has unloaded (or the browser window has been closed)
    //| onunload
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run each time the volume of a video/audio has been changed
    //| onvolumechange
    ///// &lt;audio&gt;, &lt;video&gt;
    ///// Script to be run when the media has paused but is expected to resume (like when the media pauses to buffer more data)
    //| onwaiting
    ///// All visible elements.
    ///// Script to be run when the mouse wheel rolls up or down over an element
    //| onwheel
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

module Bla =
    let test = HTMLAttribute.Autocomplete true