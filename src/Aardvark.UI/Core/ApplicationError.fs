namespace Aardvark.UI

open System

/// <summary>
/// Identifies the specific execution point within the application where an error occurred.
/// </summary>
/// <typeparam name="msg">The message type processed by the application loop.</typeparam>
[<RequireQualifiedAccess>]
type ApplicationErrorSource<'msg> =

    /// <summary>
    /// The core update function threw an unhandled exception.
    /// </summary>
    /// <param name="message">The message that was being processed when the exception occurred.</param>
    | Update of message: 'msg

    /// <summary>
    /// An event handler threw an unhandled exception.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the active application session.</param>
    /// <param name="name">The name of the event that was being dispatched.</param>
    /// <param name="sender">The identifier of the element that broadcasted the event.</param>
    /// <param name="args">The list of arguments passed to the event handler.</param>
    | EventHandler of sessionId: Guid * name: string * sender: string * args: string list

    /// <summary>
    /// A channel reader threw an unhandled exception.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the active application session.</param>
    /// <param name="elementId">The identifier of the element associated with the channel.</param>
    /// <param name="channelName">The name of the channel.</param>
    | ChannelUpdate of sessionId: Guid * elementId: string * channelName: string

/// Provides diagnostic data for an application error event.
type ApplicationErrorEventArgs<'msg>(source: ApplicationErrorSource<'msg>, exn: Exception) =
    inherit EventArgs()

    /// The specific execution point where the error occurred.
    member _.Source = source

    /// The unhandled exception thrown by application logic.
    member _.Exception = exn