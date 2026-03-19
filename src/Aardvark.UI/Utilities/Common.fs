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

    let ofEvent (button: int) =
        match button with
        | 1 -> MouseButtons.Left
        | 2 -> MouseButtons.Middle
        | 3 -> MouseButtons.Right
        | _ -> MouseButtons.None

    let ofEventStr (button: string) =
        button |> float |> int |> ofEvent

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