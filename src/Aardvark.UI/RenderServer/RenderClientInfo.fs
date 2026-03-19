namespace Aardvark.UI

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

type RenderQuality =
    {
        quality   : float
        scale     : float
        framerate : float
    }

module RenderQuality =
    let full =
        {
            quality = 90.0
            scale = 1.0
            framerate = 60.0
        }

type RenderState =
    {
        viewTrafo : Trafo3d
        projTrafo : Trafo3d
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderState =

    let identity =
        { viewTrafo = Trafo3d.Identity; projTrafo = Trafo3d.Identity }

    let pickRay (pixelPosition: PixelPosition) (state: RenderState) =
        let n = pixelPosition.NormalizedPosition
        let ndc = V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, 0.0)
        let ndcNeg = V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, -1.0)

        let p = state.projTrafo.Backward.TransformPosProj ndc
        let pNeg = state.projTrafo.Backward.TransformPosProj ndcNeg

        let viewDir = (p - pNeg) |> Vec.normalize
        let ray = Ray3d(pNeg, viewDir)
        ray.Transformed(state.viewTrafo.Backward)

type RenderClientId =
    {
        session   : Guid
        elementId : string
    }

type RenderClientInfo =
    {
        id         : RenderClientId
        token      : AdaptiveToken
        signature  : IFramebufferSignature
        sceneName  : string
        size       : V2i
        samples    : int
        quality    : RenderQuality
        state      : RenderState
        time       : MicroTime
        clearColor : C4f
    }

    member inline this.session = this.id.session

module RenderClientInfo =

    let withState (getState: RenderClientInfo -> RenderState) (clientInfo: RenderClientInfo) =
        { clientInfo with state = getState clientInfo }

type RenderClientValues internal (signature: IFramebufferSignature) =
    let _time      = AVal.init MicroTime.Zero
    let _session   = AVal.init Guid.Empty
    let _size      = AVal.init V2i.II
    let _viewTrafo = AVal.init Trafo3d.Identity
    let _projTrafo = AVal.init Trafo3d.Identity
    let _samples   = AVal.init 1

    member internal x.Update(info: RenderClientInfo) =
        _time.Value      <- info.time
        _session.Value   <- info.id.session
        _size.Value      <- info.size
        _viewTrafo.Value <- info.state.viewTrafo
        _projTrafo.Value <- info.state.projTrafo
        _samples.Value   <- info.samples

    member x.runtime   = signature.Runtime :?> IRuntime
    member x.signature = signature
    member x.size      = _size :> aval<_>
    member x.time      = _time :> aval<_>
    member x.session   = _session :> aval<_>
    member x.viewTrafo = _viewTrafo :> aval<_>
    member x.projTrafo = _projTrafo :> aval<_>
    member x.samples   = _samples :> aval<_>

[<Obsolete("Renamed to RenderClientInfo.")>]
type ClientInfo = RenderClientInfo

[<Obsolete("Renamed to RenderState.")>]
type ClientState = RenderState

[<Obsolete("Renamed to RenderClientValues.")>]
type ClientValues = RenderClientValues