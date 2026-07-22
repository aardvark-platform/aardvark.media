namespace Aardvark.UI

open System

/// Identifies the specific execution point within the framework infrastructure where an error occurred.
type InternalErrorSource =

    /// <summary>
    /// An error occurred while establishing, maintaining, or authenticating a network connection.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the active application session.</param>
    /// <param name="description">Description of the operation that caused the error.</param>
    | Connection of sessionId: Guid * description: string

    /// <summary>
    /// An error occurred while executing the render task of a render control.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the active application session.</param>
    /// <param name="elementId">The identifier of the render control.</param>
    | Rendering of sessionId: Guid * elementId: string

    /// <summary>
    /// An error occured while parsing an incoming network message.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the active application session.</param>
    /// <param name="data">The raw, unparsed string data that caused the formatting or parsing violation (can be null).</param>
    | MessageParsing of sessionId: Guid * data: string

/// Provides diagnostic data for an internal framework error event.
type InternalErrorEventArgs(source: InternalErrorSource, exn: Exception) =
    inherit EventArgs()

    /// The specific execution point within the framework infrastructure where the error occurred.
    member _.Source = source

    /// The internal framework or infrastructure exception.
    member _.Exception = exn