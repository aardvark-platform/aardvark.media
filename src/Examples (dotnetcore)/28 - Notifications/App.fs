module NotificationsExample.App

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Notifications

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open NotificationsExample.Model

module private Notification =
    let private rnd = RandomSystem()

    let private getRandom (cases : 'T[]) =
        fun () -> cases.[rnd.UniformInt cases.Length]

    let private getTitle =
        [|
            Some "Attention"
            Some "Warning"
            Some "Error"
            Some "Information"
            Some "As per my last email"
            None
        |]
        |> getRandom

    let private getMessage =
        [|
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat."
            "Ceterum censeo Carthaginem esse delendam."
            "The quick brown fox jumps over the lazy dog."
            "Pack my box with five dozen liquor jugs."
            "Sphinx of black quartz, judge my vow."
        |]
        |> getRandom

    let private getIcon =
        [|
            Some "exclamation circle"
            Some "exclamation triangle"
            Some "bell outline"
            None
        |]
        |> getRandom

    let private getColor =
        Enum.GetValues<Color>()
        |> getRandom

    let private getBool =
        [| false; true |]
        |> getRandom

    let private getTheme() =
        { Color = getColor(); Inverted = getBool() }

    let private getProgress() =
        if getBool() then None
        else
            Some {
                Top = getBool()
                Theme = getTheme()
                Increasing = getBool()
            }

    let private getDuration =
        [| Duration.Default; Duration.DefaultAuto; Duration.Milliseconds 1500 |]
        |> getRandom

    let generate (position: Position) =
        { Title         = getTitle()
          Message       = getMessage()
          Icon          = getIcon()
          Progress      = getProgress()
          Theme         = getTheme()
          Position      = position
          CenterContent = getBool()
          CloseIcon     = getBool()
          Duration      = getDuration() }

let initialCamera = {
        FreeFlyController.initial with
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
    | SetPosition p ->
        { model with position = p }

    | Notify m ->
        { model with notifications = model.notifications |> Notifications.update m }

let view (model : AdaptiveModel) =
    body [
        style "width: 100%; height: 100%; border: 0; padding: 0; margin: 0; overflow: hidden"
        style $"display: flex; flex-direction: column; background: linen"
    ] [
        model.notifications |> Notifications.container Notify (fun attributes ->
            div (attributes @ [ style $"flex: 1 1 auto; margin: 32px; background: aliceblue; border-style: dashed" ]) [
            ]
        )

        div [ style $"flex: 0 0 40px; background: darkslategray" ] [
            button [
                clazz "ui button inverted"
                style "margin: 10px"
                onClick (fun _ -> Notify <| Notifications.Send (Notification.generate <| model.position.GetValue()))
            ] [
                text "Send"
            ]

            button [
                clazz "ui button inverted"
                style "margin: 10px"
                onClick (fun _ -> Notify <| Notifications.Clear)
            ] [
                text "Clear"
            ]

            button [
                clazz "ui button inverted"
                style "margin: 10px"
                onClick (fun _ ->
                    let keys = model.notifications.Active.Content.GetValue() |> HashMap.toKeyList
                    let id = match keys with [] -> 0 | _ -> Seq.max keys
                    Notify <| Notifications.Remove id
                )
            ] [
                text "Remove"
            ]

            let values =
                Enum.GetValues<Position>()
                |> Array.map (fun p ->
                    let n =
                        match p with
                        | Position.TopRight     -> text "Top Right"
                        | Position.TopLeft      -> text "Top Left"
                        | Position.TopCenter    -> text "Top Center"
                        | Position.TopAttached  -> text "Top Attached"
                        | Position.BottomRight  -> text "Bottom Right"
                        | Position.BottomLeft   -> text "Bottom Left"
                        | Position.BottomCenter -> text "Bottom Center"
                        | _                     -> text "Bottom Attached"
                    p, n
                )
                |> AMap.ofArray

            dropdownUnclearable [clazz "selection"; style "margin: 10px"] values model.position SetPosition
        ]
    ]

let app =
    {
        unpersist = Unpersist.instance
        threads = fun _ -> ThreadPool.empty
        initial =
            {
               notifications = Notifications.Empty
               position = Position.TopRight
            }
        update = update
        view = view
    }