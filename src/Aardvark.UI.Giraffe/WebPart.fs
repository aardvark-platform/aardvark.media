namespace Aardvark.UI.Giraffe

open System.Reflection
open Aardvark.Rendering
open Aardvark.UI

type WebPart = Giraffe.Core.HttpHandler

module WebPart =

    /// <summary>
    /// Serves the embedded resources of an assembly according to the given chooser function.
    /// </summary>
    /// <param name="chooser">A function that maps a manifest resource name to an <c>HttpResource option</c>; returns <c>None</c> if the resource is not to be served.</param>
    /// <param name="assembly">The assembly containing the embedded resources to serve.</param>
    let ofAssemblyWith (chooser: string -> HttpResource option) (assembly: Assembly) =
        HttpBackend.Instance.assemblyWith chooser assembly

    /// <summary>
    /// Serves the embedded resources of an assembly.
    /// The MIME type is determined from the file extension of the resource name.
    /// </summary>
    /// <remarks>
    /// For .NET Framework assemblies all manifest resources are served using the resource name as path as-is.
    /// Otherwise, the path is determined as follows:
    /// <list type="bullet">
    /// <item>AssemblyName.resources.name -&gt; resources/name</item>
    /// <item>AssemblyName.name -&gt; name</item>
    /// <item>resources/name -&gt; resources/name</item>
    /// <item>resources\name -&gt; resources/name</item>
    /// </list>
    /// The rules are case-insensitive; if no rule applies, the resource is ignored.
    /// </remarks>
    /// <param name="assembly">The assembly containing the embedded resources to serve.</param>
    let ofAssembly (assembly: Assembly) : WebPart =
        HttpBackend.Instance.assembly assembly

    /// <summary>
    /// Serves the embedded resources of an assembly.
    /// The MIME type is determined from the file extension of the resource name.
    /// </summary>
    /// <remarks>
    /// For .NET Framework assemblies all manifest resources are served using the resource name as path as-is.
    /// Otherwise, the path is determined as follows:
    /// <list type="bullet">
    /// <item>AssemblyName.resources.name -&gt; resources/name</item>
    /// <item>AssemblyName.name -&gt; name</item>
    /// <item>resources/name -&gt; resources/name</item>
    /// <item>resources\name -&gt; resources/name</item>
    /// </list>
    /// The rules are case-insensitive; if no rule applies, the resource is ignored.
    /// </remarks>
    /// <typeparam name="'T">Type of the assembly containing the embedded resources to serve.</typeparam>
    let ofType<'T> : WebPart =
        ofAssembly typeof<'T>.Assembly

module MutableApp =
    let toWebPart' (runtime : IRuntime) (useGpuCompression: bool) (app: MutableApp<'model, 'mmodel, 'msg>) : WebPart =
        MutableApp.toWebPart' HttpBackend.Instance runtime useGpuCompression app

    let toWebPart (runtime : IRuntime) (app: MutableApp<'model, 'mmodel, 'msg>) : WebPart =
        MutableApp.toWebPart HttpBackend.Instance runtime app