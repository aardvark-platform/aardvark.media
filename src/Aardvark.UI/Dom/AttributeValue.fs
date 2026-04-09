namespace Aardvark.UI

[<RequireQualifiedAccess>]
type AttributeValue<'msg> =
    | String      of string
    | Event       of Event<'msg>
    | RenderEvent of (RenderClientInfo -> seq<'msg>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttributeValue =

    let combine (name : string) (l : AttributeValue<'msg>) (r : AttributeValue<'msg>) =
        match name, l, r with
        | _, AttributeValue.Event l, AttributeValue.Event r ->
            AttributeValue.Event (Event.combine l r)

        | "class", AttributeValue.String l, AttributeValue.String r ->
            AttributeValue.String (l + " " + r)

        | "style", AttributeValue.String l, AttributeValue.String r ->
            AttributeValue.String (l + "; " + r)

        | _, AttributeValue.RenderEvent l, AttributeValue.RenderEvent r ->
            AttributeValue.RenderEvent (fun clientInfo ->
                seq {
                    yield! l clientInfo
                    yield! r clientInfo
                }
            )

        | _ ->
            r

    let map (f : 'a -> 'b) (v : AttributeValue<'a>) =
        match v with
        | AttributeValue.Event e -> AttributeValue.Event (Event.map f e)
        | AttributeValue.String s -> AttributeValue.String s
        | AttributeValue.RenderEvent r -> AttributeValue.RenderEvent (r >> Seq.map f)