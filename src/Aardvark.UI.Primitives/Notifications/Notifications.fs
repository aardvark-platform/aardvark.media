namespace Aardvark.UI.Primitives.Notifications

open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive

[<AutoOpen>]
module NotificationsApp =

    type NotificationBuilder() =
        member inline x.Yield(()) =
            { Title         = None
              Message       = "Message"
              Icon          = None
              Progress      = None
              Theme         = Theme.Default
              Position      = Position.TopRight
              CenterContent = false
              CloseIcon     = false
              Duration      = Duration.Default }

        [<CustomOperation("title")>]
        member inline x.Title(n: Notification, title: string) =
            { n with Title = Some title }

        [<CustomOperation("message")>]
        member inline x.Message(n: Notification, message: string) =
            { n with Message = message }

        [<CustomOperation("icon")>]
        member inline x.Icon(n: Notification, icon: string) =
            { n with Icon = Some icon }

        [<CustomOperation("progress")>]
        member inline x.Progress(n: Notification, progress: Progress) =
            { n with Progress = Some progress }

        [<CustomOperation("progress")>]
        member inline x.Progress(n: Notification, show: bool) =
            { n with Progress = if show then Some Progress.Default else None }

        [<CustomOperation("theme")>]
        member inline x.Theme(n: Notification, theme: Theme) =
            { n with Theme = theme }

        [<CustomOperation("theme")>]
        member inline x.Theme(n: Notification, color: Color) =
            x.Theme(n, { Color = color; Inverted = false })

        [<CustomOperation("position")>]
        member inline x.Position(n: Notification, position: Position) =
            { n with Position = position }

        [<CustomOperation("center")>]
        member inline x.Center(n: Notification, center: bool) =
            { n with CenterContent = center }

        [<CustomOperation("closeIcon")>]
        member inline x.CloseIcon(n: Notification, show: bool) =
            { n with CloseIcon = show }

        [<CustomOperation("duration")>]
        member inline x.Duration(n: Notification, duration: Duration) =
            { n with Duration = duration }

        [<CustomOperation("duration")>]
        member inline x.Duration(n: Notification, milliseconds: int) =
            { n with Duration = Duration.Milliseconds milliseconds }

    let notification = NotificationBuilder()

    [<AutoOpen>]
    module Events =

        /// Invoked when a notification is removed.
        /// The integer argument is the ID of the removed notification.
        let onRemove (callback: int -> 'msg) : Attribute<'msg> =
            onEvent "onremove" [] (List.head >> int >> callback)

    module Notifications =

        module private Json =
            open Newtonsoft.Json
            open Newtonsoft.Json.Linq

            module JObject =

                let private theme (theme: Theme) =
                    let color =
                        match theme.Color with
                        | Color.Red    -> "red"
                        | Color.Orange -> "orange"
                        | Color.Yellow -> "yellow"
                        | Color.Olive  -> "olive"
                        | Color.Green  -> "green"
                        | Color.Teal   -> "teal"
                        | Color.Blue   -> "blue"
                        | Color.Violet -> "violet"
                        | Color.Purple -> "purple"
                        | Color.Pink   -> "pink"
                        | Color.Brown  -> "brown"
                        | Color.Grey   -> "grey"
                        | Color.Black  -> "black"
                        | _ -> ""

                    if theme.Inverted then "inverted " + color
                    else color

                let ofNotification (n: Notification) =
                    let o = JObject()

                    match n.Title with
                    | Some t -> o.["title"] <- JToken.op_Implicit t
                    | _ -> ()

                    o.["message"] <- JToken.op_Implicit n.Message

                    match n.Icon with
                    | Some i -> o.["showIcon"] <- JToken.op_Implicit i
                    | _ -> ()

                    match n.Progress with
                    | Some p ->
                        if p.Increasing then
                            o.["progressUp"] <- JToken.op_Implicit true

                        o.["showProgress"] <- JToken.op_Implicit (if p.Top then "top" else "bottom")
                        o.["classProgress"] <- JToken.op_Implicit (theme p.Theme)

                    | _ -> ()

                    let clazz =
                        [
                            theme n.Theme
                            if n.CenterContent then "centered"
                        ]
                        |> String.concat " "

                    o.["class"] <- JToken.op_Implicit clazz

                    let position =
                        match n.Position with
                        | Position.TopRight -> "top right"
                        | Position.TopLeft -> "top left"
                        | Position.TopAttached -> "top attached"
                        | Position.TopCenter -> "top center"
                        | Position.BottomRight -> "bottom right"
                        | Position.BottomLeft -> "bottom left"
                        | Position.BottomAttached -> "bottom attached"
                        | Position.BottomCenter -> "bottom center"
                        | _ -> ""

                    o.["position"] <- JToken.op_Implicit position

                    o.["closeIcon"] <- JToken.op_Implicit n.CloseIcon

                    match n.Duration with
                    | Duration.Auto (min, words) ->
                        o.["displayTime"] <- JToken.op_Implicit "auto"
                        o.["minDisplayTime"] <- JToken.op_Implicit min
                        o.["wordsPerMinute"] <- JToken.op_Implicit words

                    | Duration.Milliseconds d ->
                        o.["displayTime"] <- JToken.op_Implicit d

                    o

            let serialize (id: int) (notification: Notification option) =
                let o = JObject()
                o.["id"] <- JToken.op_Implicit id

                match notification with
                | Some n -> o.["data"] <- JObject.ofNotification n
                | _ -> ()

                o.ToString Formatting.None

        type private NotificationsChannelReader(input: amap<int, Notification>) =
            inherit ChannelReader()
            let reader = input.GetReader()

            override x.Release() = ()

            override x.ComputeMessages(token: AdaptiveToken) =
                let deltas = reader.GetChanges(token)

                deltas
                |> HashMapDelta.toHashMap
                |> HashMap.toListV
                |> List.map (fun (struct (id, op)) ->
                    match op with
                    | Set n -> Json.serialize id (Some n)
                    | Remove -> Json.serialize id None
                )

        type private NotificationsChannel(input: amap<int, Notification>) =
            inherit Channel()
            override x.GetReader() = new NotificationsChannelReader(input)

        let update (message: Notifications.Message) (notifications: Notifications) : Notifications =
            match message with
            | Notifications.Send notification ->
                { Active = notifications.Active |> HashMap.add notifications.NextId notification
                  NextId = notifications.NextId + 1 }

            | Notifications.Remove id ->
                { notifications with Active = notifications.Active |> HashMap.remove id }

            | Notifications.Clear ->
                { notifications with Active = HashMap.empty }

        let container (mapping: Notifications.Message -> 'msg)
                      (createContainer: Attribute<'msg> list -> DomNode<'msg>)
                      (notifications: AdaptiveNotifications) : DomNode<'msg> =

            let dependencies =
                Html.semui @ [ { name = "notifications"; url = "resources/notifications.js"; kind = Script }]

            let channels : (string * Channel) list = [
                "channelNotify", NotificationsChannel notifications.Active
            ]

            let boot =
                String.concat "" [
                    "const self = $('#__ID__')[0];"
                    "channelNotify.onmessage = (data) => aardvark.notifications.notify(self, data);"
                ]

            let attributes =
                [
                    style "position: relative"
                    onRemove Notifications.Remove
                ]
                |> List.map (fun (name, value) -> name, AttributeValue.map mapping value)

            require dependencies (
                let node = createContainer attributes
                node |> onBoot' channels boot
            )