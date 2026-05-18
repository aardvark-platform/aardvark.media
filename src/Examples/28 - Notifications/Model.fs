namespace NotificationsExample.Model

open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Notifications
open Adaptify

type Message =
    | SetPosition of Position
    | Notify of Notifications.Message

[<ModelType>]
type Model =
    {
        notifications : Notifications
        position : Position
    }