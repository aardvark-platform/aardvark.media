namespace Aardvark.UI.Primitives

open Aardvark.UI

/// Type that may be used as marker to serve embedded resources of Aardvark.UI.Primitives as webpart.
/// Most primitives require these resources to function correctly.
type EmbeddedResources = EmbeddedResources

module Resources =
    /// Serve embedded resources of Aardvark.UI.Primitives as webpart.
    /// Most primitives require these resources to function correctly.
    let toWebPart (http: IHttpBackend<'HttpContext, 'HttpHandler>) =
        http.assembly typeof<EmbeddedResources>.Assembly