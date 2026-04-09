namespace Aardvark.UI

open Aardvark.Base

[<Struct>]
type internal ImageRequest =
    { size       : V2i
      background : C4b }

module internal ImageRequest =
    let empty = { size = V2i.Zero; background = C4b.Zero }

/// Messages sent from the render server to the JS frontend.
[<RequireQualifiedAccess>]
type internal RenderClientMessage =
    | Invalidate

/// Messages sent from the JS frontend to the render server.
[<RequireQualifiedAccess>]
type internal RenderServerMessage =
    | RequestImage of request: ImageRequest
    | Rendered

module internal RenderServerMessage =
    open System
    open System.Text.Json

    [<Struct>]
    type private JsonReadState =
        { mutable case       : string
          mutable size       : V2i
          mutable background : C4b }

    type private JsonRenderServerMessageConverter() =
        inherit JsonRecordConverter<RenderServerMessage, JsonReadState>()

        override _.GetValue(state) =
            match state.case with
            | "RequestImage" -> RenderServerMessage.RequestImage { size = state.size; background = state.background }
            | "Rendered"     -> RenderServerMessage.Rendered
            | _ ->
                raise <| JsonException($"Unknown RenderServerMessage: {state.case}")

        override this.ReadField(reader, name, state, options) =
            match name with
            | "case"       -> state.case <- reader.GetString()
            | "size"       -> state.size <- JsonSerializer.Deserialize<V2i>(&reader, options)
            | "background" -> state.background <- JsonSerializer.Deserialize<C4b>(&reader, options)
            | _            -> reader.Skip()

    let private serializerOptions =
        let opts = JsonSerializerOptions()
        opts.Converters.Add(JsonV2iConverter())
        opts.Converters.Add(JsonC4bConverter())
        opts.Converters.Add(JsonRenderServerMessageConverter())
        opts

    let fromJson (data: ArraySegment<byte>) =
        JsonSerializer.Deserialize<RenderServerMessage>(data, serializerOptions)