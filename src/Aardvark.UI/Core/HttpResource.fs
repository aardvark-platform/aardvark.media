namespace Aardvark.UI

open System
open System.IO
open System.Reflection
open Aardvark.Base

/// Descriptor for an HTTP resource.
type HttpResource =
    {
        /// Path of the resource.
        Path : string

        /// Content type (can be null or empty).
        /// File extension of the path is used as fallback.
        MimeType : string
    }

module HttpResource =

    [<return: Struct>]
    let private (|StartsWith|_|) (prefix: string) (str: string) =
        if String.IsNullOrEmpty prefix then ValueSome str
        elif String.IsNullOrEmpty str then ValueNone
        elif str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
            ValueSome <| str.Substring(prefix.Length)
        else
            ValueNone

    let private isNetFramework (assembly: Assembly) =
        let attribute = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()
        if notNull attribute then attribute.FrameworkName.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase) else true

    /// <summary>
    /// Tries to resolve an HTTP resource from the given assembly manifest resource name.
    /// </summary>
    /// <remarks>
    /// For .NET Framework assemblies all manifest resource names are used as path as-is.
    /// Otherwise, the path is determined as follows:
    /// <list type="bullet">
    /// <item>AssemblyName.resources.name -&gt; resources/name</item>
    /// <item>AssemblyName.name -&gt; name</item>
    /// <item>resources/name -&gt; resources/name</item>
    /// <item>resources\name -&gt; resources/name</item>
    /// </list>
    /// The rules are case-insensitive; if no rule applies, <c>None</c> is returned.
    /// MIME type is determined from the file extension of the path.
    /// </remarks>
    let ofAssemblyResource (assembly: Assembly) (resourceName: string) : HttpResource option =
        let root = assembly.GetName().Name + "."
        let rootResources = root + "resources."

        let path =
            if isNetFramework assembly then
                match resourceName with
                | StartsWith "FSharpSignatureData." _ | StartsWith "FSharpOptimizationData." _ ->
                    None
                | _ ->
                    Some resourceName // For .NET Framework assemblies we keep the resource name as-is and serve everything except the ones above
            else
                match resourceName with
                | StartsWith rootResources name  // e.g., Aardvark.UI.resources.name.min.js -> resources/name.min.js
                | StartsWith "resources/" name
                | StartsWith "resources\\" name -> // e.g., resources/name.min.js (via LogicalName)
                    Some <| "resources/" + name

                | StartsWith root name -> // e.g., Aardvark.UI.name.min.js -> name.min.js
                    Some name

                | _ ->
                    None

        match path with
        | Some path when not <| String.IsNullOrEmpty path ->
            let mimeType = Path.GetExtension path |> MimeType.ofFileExtension |> Option.defaultValue null
            Some { Path = path; MimeType = mimeType }
        | _ ->
            None