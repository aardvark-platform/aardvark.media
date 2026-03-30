namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.UI

module Gamepad =

    let withGamepad element =
        element |> require [
            { kind = ReferenceKind.Script; name = "gamepad"; url = "resources/gamepad.js" }
        ]

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
                | true, v -> action v :> seq<_>
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