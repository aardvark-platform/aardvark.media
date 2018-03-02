namespace Aardvark.Cef

open System
open Xilium.CefGlue
open Xilium.CefGlue.Platform.Windows
open Xilium.CefGlue.WindowsForms
open Aardvark.Base

module PathUtils =
    open System
    open System.Text.RegularExpressions

    let private driveRx = Regex @"^(?<drive>[A-Za-z_0-9]+)\:\\"
    
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

                    let rest = rest.Split('/')
                    
                    sprintf "%s:\\%s" drive (String.concat "\\" rest)
                else
                    path.Split('/') |> String.concat "\\"
    
[<AutoOpen>]
module private CefGlobalThings =
    let inline check str v = if not v then failwithf "[CEF] %s" str
   
              