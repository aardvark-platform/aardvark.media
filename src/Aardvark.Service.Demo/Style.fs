namespace Aardvark.UI

open Aardvark.Base

type Css = Map<string, string>

type AttachmentStyle =
    | Scroll = 0
    | Fixed = 1
    | Local = 2
    | Initial = 3
    | Inherit = 4

type BlendingMode =
    | Normal = 0
    | Multiply = 1
    | Screen = 2
    | Overlay = 3
    | Darken = 4
    | Lighten = 5
    | ColorDodge = 6
    | Saturation = 7
    | Color = 8
    | Luminosity = 9


[<AutoOpen>]
module ``Css Builder`` =

    type CssBuilder() =
        static let rgba (c : C4b) =
            if c.A <> 255uy then
                sprintf "#%02X%02X%02X%02X" c.R c.G c.B c.A
            else
                sprintf "#%02X%02X%02X" c.R c.G c.B
            
        static let lowerString (v : obj) = string(v).ToLower()

        static let urlString (str : string) =
            sprintf "url(\"%s\")" (str.Replace("\"", "\\\""))

        member x.Yield(()) = Map.empty<string, string>

        [<CustomOperation("backgroundColor")>]
        member x.BackgroundColor(m : Css, color : C4b) =
            m |> Map.add "background-color" (rgba color)

        [<CustomOperation("backgroundAttachment")>]
        member x.BackgroundAttachment(m : Css, style : AttachmentStyle) =
            m |> Map.add "background-attachment" (lowerString style)
            
        [<CustomOperation("backgroundBlendMode")>]
        member x.BackgroundBlendMode(m : Css, style : BlendingMode) =
            m |> Map.add "background-blend-mode" (lowerString style)
            
        [<CustomOperation("backgroundImage")>]
        member x.BackgroundImage(m : Css, url : string) =
            m |> Map.add "background-image" (urlString url)

        member x.Run(m : Css) =
            let value = m |> Map.toSeq |> Seq.map (fun (n,v) -> sprintf "%s: %s;" n v) |> String.concat " "
            attribute "style" value


    let style = CssBuilder()
