namespace Aardvark.Service

open System

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
 
module Pickler =
    open MBrace.FsPickler
    open MBrace.FsPickler.Json
    let json = FsPickler.CreateJsonSerializer(false, true)


module Resources =

    open System.Reflection


    let (|LocalResourceName|_|) (ass : Assembly) (n : string) =
        let myNamespace = ass.GetName().Name + "."
        let myNamespaceResources = myNamespace + "resources."
    
        match n with 
        | n when n.StartsWith myNamespaceResources -> 
            let name = n.Substring (myNamespaceResources.Length)
            Some ("resources/" + name) // resources/name.min.js
        | n when n.StartsWith myNamespace -> 
            let name = n.Substring (myNamespace.Length)
            Some name   // resources/name.min.js
        | n when n.StartsWith "resources" -> 
            Some n // fallback for logicalName to prevent resource name mangling (https://github.com/aardvark-platform/aardvark.media/issues/35)
        | _ -> 
            None


    let private isNetFramework (assembly : Assembly) =
        let attributeValue = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()
        attributeValue.FrameworkName.ToLower().Contains("framework")

    let (|PlainFrameworkEmbedding|_|) (assembly : Assembly) (resName : string) =
        if assembly |> isNetFramework then Some resName
        else None