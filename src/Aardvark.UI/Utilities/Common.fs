namespace Aardvark.UI

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Application

[<AbstractClass; Sealed; Extension>]
type RayPartExtensions =

    [<Extension>]
    static member Transformed(this: RayPart, matrix: M44d) =
        RayPart(FastRay3d(this.Ray.Ray.Transformed(matrix)), this.TMin, this.TMax)

module MouseButtons =

    /// See: https://developer.mozilla.org/en-US/docs/Web/API/MouseEvent/button
    let ofEventButton (button: int) =
        match button with
        | 0 -> MouseButtons.Left
        | 1 -> MouseButtons.Middle
        | 2 -> MouseButtons.Right
        | _ -> MouseButtons.None

    /// See: https://developer.mozilla.org/en-US/docs/Web/API/MouseEvent/button
    let toEventButton (button: MouseButtons) =
        match button with
        | MouseButtons.Left   -> 0
        | MouseButtons.Middle -> 1
        | MouseButtons.Right  -> 2
        | _ -> 0

    /// See: https://developer.mozilla.org/en-US/docs/Web/API/MouseEvent/button
    let parseEventButton (button: string) =
        button |> float |> int |> ofEventButton

    /// See: https://developer.mozilla.org/en-US/docs/Web/API/MouseEvent/buttons
    let ofEventButtons (buttons: int) =
        let mutable result = MouseButtons.None
        if buttons &&& 1 = 1 then &result |||= MouseButtons.Left
        if buttons &&& 2 = 2 then &result |||= MouseButtons.Right
        if buttons &&& 4 = 4 then &result |||= MouseButtons.Middle
        result

    /// See: https://developer.mozilla.org/en-US/docs/Web/API/MouseEvent/buttons
    let parseEventButtons (buttons: string) =
        buttons |> float |> int |> ofEventButtons

    /// See: https://developer.mozilla.org/en-US/docs/Web/API/MouseEvent/buttons
    let toEventButtons (buttons: MouseButtons) =
        let mutable result = 0
        if buttons.HasFlag MouseButtons.Left then &result |||= 1
        if buttons.HasFlag MouseButtons.Right then &result |||= 2
        if buttons.HasFlag MouseButtons.Middle then &result |||= 4
        result

[<AutoOpen>]
module ``Path Utilities`` =

    module Path =
        open System.Text.RegularExpressions

        let private driveRx      = Regex @"^(?<drive>[A-Za-z_0-9]+)\:\\"
        let private rootFolderRx = Regex @"^/(?<drive>[A-Za-z_0-9]+)"

        let toUnixStyle (path : string) =
            match Environment.OSVersion.Platform with
            | PlatformID.Unix | PlatformID.MacOSX ->
                path
            | _ ->
                let m = driveRx.Match path
                if m.Success then
                    let drive = m.Groups.["drive"].Value
                    let rest = path.Substring(m.Length).Split('\\')

                    let all = Array.append [| drive |] rest
                    "/" + String.concat "/" all
                else
                    path.Split('\\') |> String.concat "/"

        let ofUnixStyle (path : string) =
            match Environment.OSVersion.Platform with
            | PlatformID.Unix | PlatformID.MacOSX ->
                path
            | _ ->
                let m = rootFolderRx.Match path
                if m.Success then
                    let drive = m.Groups.["drive"].Value
                    let rest = path.Substring(m.Length)
                    let rest =
                        if rest.StartsWith "/" then rest.Substring 1
                        else rest

                    let rest = rest.Split('/') |> String.concat "\\"
                    $"{drive}:\\{rest}"
                else
                    path.Split('/') |> String.concat "\\"