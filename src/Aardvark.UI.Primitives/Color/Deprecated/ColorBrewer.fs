namespace Aardvark.UI

open System
open Aardvark.Base

#nowarn "44"

module ColorHelper = 
    
    [<Obsolete("Use Col.ParseHex instead.")>]
    let colorFromHex (hex:string) : C4b =
        
        let hex = hex.ToCharArray()
        let r = Convert.ToInt32(hex.[2].ToString() + hex.[3].ToString(), 16) |> byte
        let g = Convert.ToInt32(hex.[4].ToString() + hex.[5].ToString(), 16) |> byte
        let b = Convert.ToInt32(hex.[6].ToString() + hex.[7].ToString(), 16) |> byte
        C4b(r, g, b, 255uy)
    
    [<Obsolete("Use Html.color instead.")>]
    let colorToHex (color : C4b) : string = 
        let bytes = [| color.R; color.G; color.B |]
        bytes 
            |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
            |> String.concat System.String.Empty

[<Obsolete("Use ColorBrewer.SchemeType from Aardvark.Base instead.")>]
type PaletteType = 
    | Diverging
    | Qualitative
    | SequentialSingle
    | SequentialMulti
   
// https://github.com/rcsb/colorbrewer/blob/master/src/main/java/org/jcolorbrewer/ColorBrewer.java

[<Obsolete("Use ColorBrewer.Scheme from Aardvark.Base instead.")>]
type BrewerPalette = {
    paletteType : PaletteType
    paletteDescription: String
    colorBlindSave : bool
    hexColors : string[][] 
} with 
    member x.C4b : C4b[][] = 
        x.hexColors |> Array.map (fun level -> level |> Array.map ColorHelper.colorFromHex)
    member x.GetSamples (colorCount : int) : C4b[] = 
        if colorCount < x.C4b.Length then
            x.C4b.[colorCount]
        else
            let maxIndex : int = x.hexColors.Length-1 
            let scale : float = (float maxIndex) / (float (colorCount-1))
            [|
                for i in 0..colorCount-1 do
                    let value = float i * scale
                    let index = value |> int
                    let c1 : C4b = x.C4b.[maxIndex].[index]
                    let c2, remainder = 
                        if index+1 < x.hexColors.Length then 
                            x.C4b.[maxIndex].[index+1], value - (float index)
                        else 
                            x.C4b.[maxIndex].[index], 0.0
                
                    lerp c1 c2 remainder
            |]
    member x.SpectrumRow (count: int) : string = 
        let row = x.GetSamples count |> Array.map (fun x -> "'#" + ColorHelper.colorToHex x + "'") |> String.concat ","
        "[" + row + "]"

[<Obsolete("Use ColorBrewer from Aardvark.Base instead.")>]
module BrewerPalette = 
    
    let spectrumRow (count: int) (palette: BrewerPalette) : string = 
        palette.SpectrumRow count
    
    let getSamples (count: int) (palette: BrewerPalette) : C4b[] = 
        palette.GetSamples count

[<Obsolete("Use ColorBrewer.Scheme from Aardvark.Base instead.")>]
module ColorBrewerPalettes = 
    
    let BrownBlueGreen = {
         paletteType = Diverging
         paletteDescription = "Brown-Blue-Green"
         colorBlindSave = true
         hexColors = [|
            [|"0xD8B365"|];
            [|"0xD8B365"; "0x5AB4AC"|];
            [|"0xD8B365"; "0xF5F5F5"; "0x5AB4AC"|];
            [|"0xA6611A"; "0xDFC27D"; "0x80CDC1"; "0x018571"|];
            [|"0xA6611A"; "0xDFC27D"; "0xF5F5F5"; "0x80CDC1"; "0x018571"|];
            [|"0x8C510A"; "0xD8B365"; "0xF6E8C3"; "0xC7EAE5"; "0x5AB4AC"; "0x01665E"|];
            [|"0x8C510A"; "0xD8B365"; "0xF6E8C3"; "0xF5F5F5"; "0xC7EAE5"; "0x5AB4AC"; "0x01665E"|];
            [|"0x8C510A"; "0xBF812D"; "0xDFC27D"; "0xF6E8C3"; "0xC7EAE5"; "0x80CDC1"; "0x35978F"; "0x01665E"|]; 
            [|"0x8C510A"; "0xBF812D"; "0xDFC27D"; "0xF6E8C3"; "0xF5F5F5"; "0xC7EAE5"; "0x80CDC1"; "0x35978F"; "0x01665E"|]; 
            [|"0x543005"; "0x8C510A"; "0xBF812D"; "0xDFC27D"; "0xF6E8C3"; "0xC7EAE5"; "0x80CDC1"; "0x35978F"; "0x01665E"; "0x003C30"|]; 
            [|"0x543005"; "0x8C510A"; "0xBF812D"; "0xDFC27D"; "0xF6E8C3"; "0xF5F5F5"; "0xC7EAE5"; "0x80CDC1"; "0x35978F"; "0x01665E"; "0x003C30"|];
        |]
    
    }
    
    let MagentaYellowGreen = {
        paletteType = Diverging
        paletteDescription = "Magenta-Yellow-Green"
        colorBlindSave = true
        hexColors = [|
            [|"0xE9A3C9"|];
            [|"0xE9A3C9"; "0xA1D76A"|];
            [|"0xE9A3C9"; "0xF7F7F7"; "0xA1D76A"|];
            [|"0xD01C8B"; "0xF1B6DA"; "0xB8E186"; "0x4DAC26"|];
            [|"0xD01C8B"; "0xF1B6DA"; "0xF7F7F7"; "0xB8E186"; "0x4DAC26"|];
            [|"0xC51B7D"; "0xE9A3C9"; "0xFDE0EF"; "0xE6F5D0"; "0xA1D76A"; "0x4D9221"|]; 
            [|"0xC51B7D"; "0xE9A3C9"; "0xFDE0EF"; "0xF7F7F7"; "0xE6F5D0"; "0xA1D76A"; "0x4D9221"|];
            [|"0xC51B7D"; "0xDE77AE"; "0xF1B6DA"; "0xFDE0EF"; "0xE6F5D0"; "0xB8E186"; "0x7FBC41"; "0x4D9221"|]; 
            [|"0xC51B7D"; "0xDE77AE"; "0xF1B6DA"; "0xFDE0EF"; "0xF7F7F7"; "0xE6F5D0"; "0xB8E186"; "0x7FBC41"; "0x4D9221"|]; 
            [|"0x8E0152"; "0xC51B7D"; "0xDE77AE"; "0xF1B6DA"; "0xFDE0EF"; "0xE6F5D0"; "0xB8E186"; "0x7FBC41"; "0x4D9221"; "0x276419"|]; 
            [|"0x8E0152"; "0xC51B7D"; "0xDE77AE"; "0xF1B6DA"; "0xFDE0EF"; "0xF7F7F7"; "0xE6F5D0"; "0xB8E186"; "0x7FBC41"; "0x4D9221"; "0x276419"|];
        |]
    }
    
    let PurpleRedGreen = {
        paletteType = Diverging
        paletteDescription = "Purple-Red-Green"
        colorBlindSave = true
        hexColors = [|
            [|"0xAF8DC3"|];
            [|"0xAF8DC3"; "0x7FBF7B"|];
            [|"0xAF8DC3"; "0xF7F7F7"; "0x7FBF7B"|];
            [|"0x7B3294"; "0xC2A5CF"; "0xA6DBA0"; "0x008837"|];
            [|"0x7B3294"; "0xC2A5CF"; "0xF7F7F7"; "0xA6DBA0"; "0x008837"|];
            [|"0x762A83"; "0xAF8DC3"; "0xE7D4E8"; "0xD9F0D3"; "0x7FBF7B"; "0x1B7837"|];
            [|"0x762A83"; "0xAF8DC3"; "0xE7D4E8"; "0xF7F7F7"; "0xD9F0D3"; "0x7FBF7B"; "0x1B7837"|];
            [|"0x762A83"; "0x9970AB"; "0xC2A5CF"; "0xE7D4E8"; "0xD9F0D3"; "0xA6DBA0"; "0x5AAE61"; "0x1B7837"|];
            [|"0x762A83"; "0x9970AB"; "0xC2A5CF"; "0xE7D4E8"; "0xF7F7F7"; "0xD9F0D3"; "0xA6DBA0"; "0x5AAE61"; "0x1B7837"|];
            [|"0x40004B"; "0x762A83"; "0x9970AB"; "0xC2A5CF"; "0xE7D4E8"; "0xD9F0D3"; "0xA6DBA0"; "0x5AAE61"; "0x1B7837"; "0x00441B"|];
            [|"0x40004B"; "0x762A83"; "0x9970AB"; "0xC2A5CF"; "0xE7D4E8"; "0xF7F7F7"; "0xD9F0D3"; "0xA6DBA0"; "0x5AAE61"; "0x1B7837"; "0x00441B"|];
        |]
    
    }      

    let PurpleOrange = {
        paletteType = Diverging
        paletteDescription = "Purple-Orange"
        colorBlindSave = true
        hexColors = [|
            [|"0xF1A340"|];
            [|"0xF1A340"; "0x998EC3"|];
            [|"0xF1A340"; "0xF7F7F7"; "0x998EC3"|]; 
            [|"0xE66101"; "0xFDB863"; "0xB2ABD2"; "0x5E3C99"|]; 
            [|"0xE66101"; "0xFDB863"; "0xF7F7F7"; "0xB2ABD2"; "0x5E3C99"|];
            [|"0xB35806"; "0xF1A340"; "0xFEE0B6"; "0xD8DAEB"; "0x998EC3"; "0x542788"|];
            [|"0xB35806"; "0xF1A340"; "0xFEE0B6"; "0xF7F7F7"; "0xD8DAEB"; "0x998EC3"; "0x542788"|];
            [|"0xB35806"; "0xE08214"; "0xFDB863"; "0xFEE0B6"; "0xD8DAEB"; "0xB2ABD2"; "0x8073AC"; "0x542788"|];
            [|"0xB35806"; "0xE08214"; "0xFDB863"; "0xFEE0B6"; "0xF7F7F7"; "0xD8DAEB"; "0xB2ABD2"; "0x8073AC"; "0x542788"|];
            [|"0x7F3B08"; "0xB35806"; "0xE08214"; "0xFDB863"; "0xFEE0B6"; "0xD8DAEB"; "0xB2ABD2"; "0x8073AC"; "0x542788"; "0x2D004B"|];
            [|"0x7F3B08"; "0xB35806"; "0xE08214"; "0xFDB863"; "0xFEE0B6"; "0xF7F7F7"; "0xD8DAEB"; "0xB2ABD2"; "0x8073AC"; "0x542788"; "0x2D004B"|];
            |]
        }
        
    let RedBlue = {
        paletteType = Diverging
        paletteDescription = "Red-Blue"
        colorBlindSave = true
        hexColors = [|
            [|"0xEF8A62"|];
            [|"0xEF8A62"; "0x67A9CF"|];
            [|"0xEF8A62"; "0xF7F7F7"; "0x67A9CF"|];
            [|"0xCA0020"; "0xF4A582"; "0x92C5DE"; "0x0571B0"|];
            [|"0xCA0020"; "0xF4A582"; "0xF7F7F7"; "0x92C5DE"; "0x0571B0"|];
            [|"0xB2182B"; "0xEF8A62"; "0xFDDBC7"; "0xD1E5F0"; "0x67A9CF"; "0x2166AC"|];
            [|"0xB2182B"; "0xEF8A62"; "0xFDDBC7"; "0xF7F7F7"; "0xD1E5F0"; "0x67A9CF"; "0x2166AC"|];
            [|"0xB2182B"; "0xD6604D"; "0xF4A582"; "0xFDDBC7"; "0xD1E5F0"; "0x92C5DE"; "0x4393C3"; "0x2166AC"|];
            [|"0xB2182B"; "0xD6604D"; "0xF4A582"; "0xFDDBC7"; "0xF7F7F7"; "0xD1E5F0"; "0x92C5DE"; "0x4393C3"; "0x2166AC"|];
            [|"0x67001F"; "0xB2182B"; "0xD6604D"; "0xF4A582"; "0xFDDBC7"; "0xD1E5F0"; "0x92C5DE"; "0x4393C3"; "0x2166AC"; "0x053061"|];
            [|"0x67001F"; "0xB2182B"; "0xD6604D"; "0xF4A582"; "0xFDDBC7"; "0xF7F7F7"; "0xD1E5F0"; "0x92C5DE"; "0x4393C3"; "0x2166AC"; "0x053061"|];
        |]
    }
    
    let RedGrey = {
        paletteType = Diverging
        paletteDescription = "Red-Grey"
        colorBlindSave = false
        hexColors = [|
            [|"0xEF8A62"|];
            [|"0xEF8A62"; "0x999999"|];
            [|"0xEF8A62"; "0xFFFFFF"; "0x999999"|];
            [|"0xCA0020"; "0xF4A582"; "0xBABABA"; "0x404040"|];
            [|"0xCA0020"; "0xF4A582"; "0xFFFFFF"; "0xBABABA"; "0x404040"|];
            [|"0xB2182B"; "0xEF8A62"; "0xFDDBC7"; "0xE0E0E0"; "0x999999"; "0x4D4D4D"|];
            [|"0xB2182B"; "0xEF8A62"; "0xFDDBC7"; "0xFFFFFF"; "0xE0E0E0"; "0x999999"; "0x4D4D4D"|];
            [|"0xB2182B"; "0xD6604D"; "0xF4A582"; "0xFDDBC7"; "0xE0E0E0"; "0xBABABA"; "0x878787"; "0x4D4D4D"|];
            [|"0xB2182B"; "0xD6604D"; "0xF4A582"; "0xFDDBC7"; "0xFFFFFF"; "0xE0E0E0"; "0xBABABA"; "0x878787"; "0x4D4D4D"|];
            [|"0x67001F"; "0xB2182B"; "0xD6604D"; "0xF4A582"; "0xFDDBC7"; "0xE0E0E0"; "0xBABABA"; "0x878787"; "0x4D4D4D"; "0x1A1A1A"|];
            [|"0x67001F"; "0xB2182B"; "0xD6604D"; "0xF4A582"; "0xFDDBC7"; "0xFFFFFF"; "0xE0E0E0"; "0xBABABA"; "0x878787"; "0x4D4D4D"; "0x1A1A1A"|];
        |]
    }   
            
    let RedYellowBlue = {
        paletteType = Diverging
        paletteDescription = "Red-Yellow-Blue"
        colorBlindSave = true
        hexColors = [|
            [|"0xFC8D59"|];
            [|"0xFC8D59"; "0x91BFDB"|];
            [|"0xFC8D59"; "0xFFFFBF"; "0x91BFDB"|];
            [|"0xD7191C"; "0xFDAE61"; "0xABD9E9"; "0x2C7BB6"|];
            [|"0xD7191C"; "0xFDAE61"; "0xFFFFBF"; "0xABD9E9"; "0x2C7BB6"|];
            [|"0xD73027"; "0xFC8D59"; "0xFEE090"; "0xE0F3F8"; "0x91BFDB"; "0x4575B4"|];
            [|"0xD73027"; "0xFC8D59"; "0xFEE090"; "0xFFFFBF"; "0xE0F3F8"; "0x91BFDB"; "0x4575B4"|];
            [|"0xD73027"; "0xF46D43"; "0xFDAE61"; "0xFEE090"; "0xE0F3F8"; "0xABD9E9"; "0x74ADD1"; "0x4575B4"|];
            [|"0xD73027"; "0xF46D43"; "0xFDAE61"; "0xFEE090"; "0xFFFFBF"; "0xE0F3F8"; "0xABD9E9"; "0x74ADD1"; "0x4575B4"|];
            [|"0xA50026"; "0xD73027"; "0xF46D43"; "0xFDAE61"; "0xFEE090"; "0xE0F3F8"; "0xABD9E9"; "0x74ADD1"; "0x4575B4"; "0x313695"|];
            [|"0xA50026"; "0xD73027"; "0xF46D43"; "0xFDAE61"; "0xFEE090"; "0xFFFFBF"; "0xE0F3F8"; "0xABD9E9"; "0x74ADD1"; "0x4575B4"; "0x313695"|];
        |]
    }
            
    let RedYellowGreen = {
        paletteType = Diverging
        paletteDescription = "Red-Yellow-Green"
        colorBlindSave = false
        hexColors = [|
            [|"0xFC8D59"|];
            [|"0xFC8D59"; "0x91CF60"|];
            [|"0xFC8D59"; "0xFFFFBF"; "0x91CF60"|];
            [|"0xD7191C"; "0xFDAE61"; "0xA6D96A"; "0x1A9641"|];
            [|"0xD7191C"; "0xFDAE61"; "0xFFFFBF"; "0xA6D96A"; "0x1A9641"|];
            [|"0xD73027"; "0xFC8D59"; "0xFEE08B"; "0xD9EF8B"; "0x91CF60"; "0x1A9850"|];
            [|"0xD73027"; "0xFC8D59"; "0xFEE08B"; "0xFFFFBF"; "0xD9EF8B"; "0x91CF60"; "0x1A9850"|];
            [|"0xD73027"; "0xF46D43"; "0xFDAE61"; "0xFEE08B"; "0xD9EF8B"; "0xA6D96A"; "0x66BD63"; "0x1A9850"|];
            [|"0xD73027"; "0xF46D43"; "0xFDAE61"; "0xFEE08B"; "0xFFFFBF"; "0xD9EF8B"; "0xA6D96A"; "0x66BD63"; "0x1A9850"|];
            [|"0xA50026"; "0xD73027"; "0xF46D43"; "0xFDAE61"; "0xFEE08B"; "0xD9EF8B"; "0xA6D96A"; "0x66BD63"; "0x1A9850"; "0x006837"|];
            [|"0xA50026"; "0xD73027"; "0xF46D43"; "0xFDAE61"; "0xFEE08B"; "0xFFFFBF"; "0xD9EF8B"; "0xA6D96A"; "0x66BD63"; "0x1A9850"; "0x006837"|];
        |]
    }
            
    let Spectral = {
        paletteType = Diverging
        paletteDescription = "Spectral colors"
        colorBlindSave = false
        hexColors = [|
            [|"0xFC8D59"|];
            [|"0xFC8D59"; "0x99D594"|];
            [|"0xFC8D59"; "0xFFFFBF"; "0x99D594"|];
            [|"0xD7191C"; "0xFDAE61"; "0xABDDA4"; "0x2B83BA"|];
            [|"0xD7191C"; "0xFDAE61"; "0xFFFFBF"; "0xABDDA4"; "0x2B83BA"|];
            [|"0xD53E4F"; "0xFC8D59"; "0xFEE08B"; "0xE6F598"; "0x99D594"; "0x3288BD"|];
            [|"0xD53E4F"; "0xFC8D59"; "0xFEE08B"; "0xFFFFBF"; "0xE6F598"; "0x99D594"; "0x3288BD"|];
            [|"0xD53E4F"; "0xF46D43"; "0xFDAE61"; "0xFEE08B"; "0xE6F598"; "0xABDDA4"; "0x66C2A5"; "0x3288BD"|];
            [|"0xD53E4F"; "0xF46D43"; "0xFDAE61"; "0xFEE08B"; "0xFFFFBF"; "0xE6F598"; "0xABDDA4"; "0x66C2A5"; "0x3288BD"|];
            [|"0x9E0142"; "0xD53E4F"; "0xF46D43"; "0xFDAE61"; "0xFEE08B"; "0xE6F598"; "0xABDDA4"; "0x66C2A5"; "0x3288BD"; "0x5E4FA2"|];
            [|"0x9E0142"; "0xD53E4F"; "0xF46D43"; "0xFDAE61"; "0xFEE08B"; "0xFFFFBF"; "0xE6F598"; "0xABDDA4"; "0x66C2A5"; "0x3288BD"; "0x5E4FA2"|];
        |]
    }
            
    //* qualitative colors */
    let Accent = {
        paletteType = Qualitative
        paletteDescription = "Accents"
        colorBlindSave = false
        hexColors = [|
            [|"0x7FC97F"|];
            [|"0x7FC97F"; "0xFDC086"|];
            [|"0x7FC97F"; "0xBEAED4"; "0xFDC086"|];
            [|"0x7FC97F"; "0xBEAED4"; "0xFDC086"; "0xFFFF99"|];
            [|"0x7FC97F"; "0xBEAED4"; "0xFDC086"; "0xFFFF99"; "0x386CB0"|];
            [|"0x7FC97F"; "0xBEAED4"; "0xFDC086"; "0xFFFF99"; "0x386CB0"; "0xF0027F"|];
            [|"0x7FC97F"; "0xBEAED4"; "0xFDC086"; "0xFFFF99"; "0x386CB0"; "0xF0027F"; "0xBF5B17"|];
            [|"0x7FC97F"; "0xBEAED4"; "0xFDC086"; "0xFFFF99"; "0x386CB0"; "0xF0027F"; "0xBF5B17"; "0x666666"|];
        |]
    }
            
    let Dark = {
        paletteType = Qualitative
        paletteDescription = "Dark colors"
        colorBlindSave = false
        hexColors = [|
            [|"0x1B9E77"|];
            [|"0x1B9E77"; "0x7570B3"|];
            [|"0x1B9E77"; "0xD95F02"; "0x7570B3"|];
            [|"0x1B9E77"; "0xD95F02"; "0x7570B3"; "0xE7298A"|];
            [|"0x1B9E77"; "0xD95F02"; "0x7570B3"; "0xE7298A"; "0x66A61E"|];
            [|"0x1B9E77"; "0xD95F02"; "0x7570B3"; "0xE7298A"; "0x66A61E"; "0xE6AB02"|];
            [|"0x1B9E77"; "0xD95F02"; "0x7570B3"; "0xE7298A"; "0x66A61E"; "0xE6AB02"; "0xA6761D"|];
            [|"0x1B9E77"; "0xD95F02"; "0x7570B3"; "0xE7298A"; "0x66A61E"; "0xE6AB02"; "0xA6761D"; "0x666666"|];
        |]
    }
            
    let Paired = {
        paletteType = Qualitative
        paletteDescription = "Paired colors"
        colorBlindSave = true
        hexColors = [|
            [|"0xA6CEE3"|];
            [|"0xA6CEE3"; "0xB2DF8A"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"; "0x33A02C"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"; "0x33A02C"; "0xFB9A99"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"; "0x33A02C"; "0xFB9A99"; "0xE31A1C"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"; "0x33A02C"; "0xFB9A99"; "0xE31A1C"; "0xFDBF6F"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"; "0x33A02C"; "0xFB9A99"; "0xE31A1C"; "0xFDBF6F"; "0xFF7F00"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"; "0x33A02C"; "0xFB9A99"; "0xE31A1C"; "0xFDBF6F"; "0xFF7F00"; "0xCAB2D6"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"; "0x33A02C"; "0xFB9A99"; "0xE31A1C"; "0xFDBF6F"; "0xFF7F00"; "0xCAB2D6"; "0x6A3D9A"|];
            [|"0xA6CEE3"; "0x1F78B4"; "0xB2DF8A"; "0x33A02C"; "0xFB9A99"; "0xE31A1C"; "0xFDBF6F"; "0xFF7F00"; "0xCAB2D6"; "0x6A3D9A"; "0xFFFF99"|]; 
        |]
    }
            
    let Pastel1 = {
        paletteType = Qualitative
        paletteDescription = "Pastel1 colors"
        colorBlindSave = false
        hexColors = [|
            [|"0xFBB4AE"|];
            [|"0xFBB4AE"; "0xCCEBC5"|];
            [|"0xFBB4AE"; "0xB3CDE3"; "0xCCEBC5"|];
            [|"0xFBB4AE"; "0xB3CDE3"; "0xCCEBC5"; "0xDECBE4"|];
            [|"0xFBB4AE"; "0xB3CDE3"; "0xCCEBC5"; "0xDECBE4"; "0xFED9A6"|];
            [|"0xFBB4AE"; "0xB3CDE3"; "0xCCEBC5"; "0xDECBE4"; "0xFED9A6"; "0xFFFFCC"|];
            [|"0xFBB4AE"; "0xB3CDE3"; "0xCCEBC5"; "0xDECBE4"; "0xFED9A6"; "0xFFFFCC"; "0xE5D8BD"|];
            [|"0xFBB4AE"; "0xB3CDE3"; "0xCCEBC5"; "0xDECBE4"; "0xFED9A6"; "0xFFFFCC"; "0xE5D8BD"; "0xFDDAEC"|];
            [|"0xFBB4AE"; "0xB3CDE3"; "0xCCEBC5"; "0xDECBE4"; "0xFED9A6"; "0xFFFFCC"; "0xE5D8BD"; "0xFDDAEC"; "0xF2F2F2"|];
        |]
    }
            
    let Pastel2 = {
        paletteType = Qualitative
        paletteDescription = "Pastel2 colors"
        colorBlindSave = false
        hexColors = [|
            [|"0xB3E2CD"|];
            [|"0xB3E2CD"; "0xCBD5E8"|];
            [|"0xB3E2CD"; "0xFDCDAC"; "0xCBD5E8"|];
            [|"0xB3E2CD"; "0xFDCDAC"; "0xCBD5E8"; "0xF4CAE4"|];
            [|"0xB3E2CD"; "0xFDCDAC"; "0xCBD5E8"; "0xF4CAE4"; "0xE6F5C9"|];
            [|"0xB3E2CD"; "0xFDCDAC"; "0xCBD5E8"; "0xF4CAE4"; "0xE6F5C9"; "0xFFF2AE"|];
            [|"0xB3E2CD"; "0xFDCDAC"; "0xCBD5E8"; "0xF4CAE4"; "0xE6F5C9"; "0xFFF2AE"; "0xF1E2CC"|];
            [|"0xB3E2CD"; "0xFDCDAC"; "0xCBD5E8"; "0xF4CAE4"; "0xE6F5C9"; "0xFFF2AE"; "0xF1E2CC"; "0xCCCCCC"|];
        |]
    }
            
    let Set1 = {
        paletteType = Qualitative
        paletteDescription = "Set1 colors"
        colorBlindSave = false
        hexColors = [|
            [|"0xE41A1C"|];
            [|"0xE41A1C"; "0x4DAF4A"|];
            [|"0xE41A1C"; "0x377EB8"; "0x4DAF4A"|];
            [|"0xE41A1C"; "0x377EB8"; "0x4DAF4A"; "0x984EA3"|];
            [|"0xE41A1C"; "0x377EB8"; "0x4DAF4A"; "0x984EA3"; "0xFF7F00"|];
            [|"0xE41A1C"; "0x377EB8"; "0x4DAF4A"; "0x984EA3"; "0xFF7F00"; "0xFFFF33"|];
            [|"0xE41A1C"; "0x377EB8"; "0x4DAF4A"; "0x984EA3"; "0xFF7F00"; "0xFFFF33"; "0xA65628"|];
            [|"0xE41A1C"; "0x377EB8"; "0x4DAF4A"; "0x984EA3"; "0xFF7F00"; "0xFFFF33"; "0xA65628"; "0xF781BF"|];
            [|"0xE41A1C"; "0x377EB8"; "0x4DAF4A"; "0x984EA3"; "0xFF7F00"; "0xFFFF33"; "0xA65628"; "0xF781BF"; "0x999999"|];
        |]
    }
            
    let Set2 = {
        paletteType = Qualitative
        paletteDescription = "Set2 colors"
        colorBlindSave = false
        hexColors = [|
            [|"0x66C2A5"|];
            [|"0x66C2A5"; "0x8DA0CB"|];
            [|"0x66C2A5"; "0xFC8D62"; "0x8DA0CB"|];
            [|"0x66C2A5"; "0xFC8D62"; "0x8DA0CB"; "0xE78AC3"|];
            [|"0x66C2A5"; "0xFC8D62"; "0x8DA0CB"; "0xE78AC3"; "0xA6D854"|];
            [|"0x66C2A5"; "0xFC8D62"; "0x8DA0CB"; "0xE78AC3"; "0xA6D854"; "0xFFD92F"|];
            [|"0x66C2A5"; "0xFC8D62"; "0x8DA0CB"; "0xE78AC3"; "0xA6D854"; "0xFFD92F"; "0xE5C494"|];
            [|"0x66C2A5"; "0xFC8D62"; "0x8DA0CB"; "0xE78AC3"; "0xA6D854"; "0xFFD92F"; "0xE5C494"; "0xB3B3B"|];
        |]
    }
        
    let Set3 = {
        paletteType = Qualitative
        paletteDescription = "Set3 colors"
        colorBlindSave = false
        hexColors = [|
            [|"0x8DD3C7"|];
            [|"0x8DD3C7"; "0xBEBADA"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"; "0xFB8072"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"; "0xFB8072"; "0x80B1D3"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"; "0xFB8072"; "0x80B1D3"; "0xFDB462"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"; "0xFB8072"; "0x80B1D3"; "0xFDB462"; "0xB3DE69"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"; "0xFB8072"; "0x80B1D3"; "0xFDB462"; "0xB3DE69"; "0xFCCDE5"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"; "0xFB8072"; "0x80B1D3"; "0xFDB462"; "0xB3DE69"; "0xFCCDE5"; "0xD9D9D9"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"; "0xFB8072"; "0x80B1D3"; "0xFDB462"; "0xB3DE69"; "0xFCCDE5"; "0xD9D9D9"; "0xBC80BD"|];
            [|"0x8DD3C7"; "0xFFFFB3"; "0xBEBADA"; "0xFB8072"; "0x80B1D3"; "0xFDB462"; "0xB3DE69"; "0xFCCDE5"; "0xD9D9D9"; "0xBC80BD"; "0xCCEBC5"|];
        |]
    }
            
    //* sequential colors */
    let Blues = {
        paletteType = SequentialSingle
        paletteDescription = "Blue shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xDEEBF7"|];
            [|"0xDEEBF7"; "0x3182BD"|];
            [|"0xDEEBF7"; "0x9ECAE1"; "0x3182BD"|];
            [|"0xEFF3FF"; "0xBDD7E7"; "0x6BAED6"; "0x2171B5"|];
            [|"0xEFF3FF"; "0xBDD7E7"; "0x6BAED6"; "0x3182BD"; "0x08519C"|];
            [|"0xEFF3FF"; "0xC6DBEF"; "0x9ECAE1"; "0x6BAED6"; "0x3182BD"; "0x08519C"|];
            [|"0xEFF3FF"; "0xC6DBEF"; "0x9ECAE1"; "0x6BAED6"; "0x4292C6"; "0x2171B5"; "0x084594"|];
            [|"0xF7FBFF"; "0xDEEBF7"; "0xC6DBEF"; "0x9ECAE1"; "0x6BAED6"; "0x4292C6"; "0x2171B5"; "0x084594"|];
            [|"0xF7FBFF"; "0xDEEBF7"; "0xC6DBEF"; "0x9ECAE1"; "0x6BAED6"; "0x4292C6"; "0x2171B5"; "0x08519C"; "0x08306B"|];
        |]
    }
        
    let BlueGreenShades = {
        paletteType = SequentialMulti
        paletteDescription = "Blue-Green shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xE5F5F9"|];
            [|"0xE5F5F9"; "0x2CA25F"|];
            [|"0xE5F5F9"; "0x99D8C9"; "0x2CA25F"|];
            [|"0xEDF8FB"; "0xB2E2E2"; "0x66C2A4"; "0x238B45"|];
            [|"0xEDF8FB"; "0xB2E2E2"; "0x66C2A4"; "0x2CA25F"; "0x006D2C"|];
            [|"0xEDF8FB"; "0xCCECE6"; "0x99D8C9"; "0x66C2A4"; "0x2CA25F"; "0x006D2C"|];
            [|"0xEDF8FB"; "0xCCECE6"; "0x99D8C9"; "0x66C2A4"; "0x41AE76"; "0x238B45"; "0x005824"|];
            [|"0xF7FCFD"; "0xE5F5F9"; "0xCCECE6"; "0x99D8C9"; "0x66C2A4"; "0x41AE76"; "0x238B45"; "0x005824"|];
            [|"0xF7FCFD"; "0xE5F5F9"; "0xCCECE6"; "0x99D8C9"; "0x66C2A4"; "0x41AE76"; "0x238B45"; "0x006D2C"; "0x00441B"|];
        |]
    }
    
    let BluePurpleShades = {
        paletteType = SequentialMulti
        paletteDescription = "Blue-Purple shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xE0ECF4"|];
            [|"0xE0ECF4"; "0x8856A7"|];
            [|"0xE0ECF4"; "0x9EBCDA"; "0x8856A7"|];
            [|"0xEDF8FB"; "0xB3CDE3"; "0x8C96C6"; "0x88419D"|];
            [|"0xEDF8FB"; "0xB3CDE3"; "0x8C96C6"; "0x8856A7"; "0x810F7C"|];
            [|"0xEDF8FB"; "0xBFD3E6"; "0x9EBCDA"; "0x8C96C6"; "0x8856A7"; "0x810F7C"|];
            [|"0xEDF8FB"; "0xBFD3E6"; "0x9EBCDA"; "0x8C96C6"; "0x8C6BB1"; "0x88419D"; "0x6E016B"|];
            [|"0xF7FCFD"; "0xE0ECF4"; "0xBFD3E6"; "0x9EBCDA"; "0x8C96C6"; "0x8C6BB1"; "0x88419D"; "0x6E016B"|];
            [|"0xF7FCFD"; "0xE0ECF4"; "0xBFD3E6"; "0x9EBCDA"; "0x8C96C6"; "0x8C6BB1"; "0x88419D"; "0x810F7C"; "0x4D004B"|];
        |]
    }
            
    let GreenBlueShades = {
        paletteType = SequentialMulti
        paletteDescription = "Green-Blue shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xE0F3DB"|];
            [|"0xE0F3DB"; "0x43A2CA"|];
            [|"0xE0F3DB"; "0xA8DDB5"; "0x43A2CA"|];
            [|"0xF0F9E8"; "0xBAE4BC"; "0x7BCCC4"; "0x2B8CBE"|];
            [|"0xF0F9E8"; "0xBAE4BC"; "0x7BCCC4"; "0x43A2CA"; "0x0868AC"|];
            [|"0xF0F9E8"; "0xCCEBC5"; "0xA8DDB5"; "0x7BCCC4"; "0x43A2CA"; "0x0868AC"|];
            [|"0xF0F9E8"; "0xCCEBC5"; "0xA8DDB5"; "0x7BCCC4"; "0x4EB3D3"; "0x2B8CBE"; "0x08589E"|];
            [|"0xF7FCF0"; "0xE0F3DB"; "0xCCEBC5"; "0xA8DDB5"; "0x7BCCC4"; "0x4EB3D3"; "0x2B8CBE"; "0x08589E"|];
            [|"0xF7FCF0"; "0xE0F3DB"; "0xCCEBC5"; "0xA8DDB5"; "0x7BCCC4"; "0x4EB3D3"; "0x2B8CBE"; "0x0868AC"; "0x084081"|];
        |]
    }
            
    let Greens = {
        paletteType = SequentialSingle
        paletteDescription = "Green shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xE5F5E0"|];
            [|"0xE5F5E0"; "0x31A354"|];
            [|"0xE5F5E0"; "0xA1D99B"; "0x31A354"|];
            [|"0xEDF8E9"; "0xBAE4B3"; "0x74C476"; "0x238B45"|];
            [|"0xEDF8E9"; "0xBAE4B3"; "0x74C476"; "0x31A354"; "0x006D2C"|];
            [|"0xEDF8E9"; "0xC7E9C0"; "0xA1D99B"; "0x74C476"; "0x31A354"; "0x006D2C"|];
            [|"0xEDF8E9"; "0xC7E9C0"; "0xA1D99B"; "0x74C476"; "0x41AB5D"; "0x238B45"; "0x005A32"|];
            [|"0xF7FCF5"; "0xE5F5E0"; "0xC7E9C0"; "0xA1D99B"; "0x74C476"; "0x41AB5D"; "0x238B45"; "0x005A32"|];
            [|"0xF7FCF5"; "0xE5F5E0"; "0xC7E9C0"; "0xA1D99B"; "0x74C476"; "0x41AB5D"; "0x238B45"; "0x006D2C"; "0x00441B"|];
        |]
    }
        
    let Greys = {
        paletteType = SequentialSingle
        paletteDescription = "Grey shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xF0F0F0"|];
            [|"0xF0F0F0"; "0x636363"|];
            [|"0xF0F0F0"; "0xBDBDBD"; "0x636363"|];
            [|"0xF7F7F7"; "0xCCCCCC"; "0x969696"; "0x525252"|];
            [|"0xF7F7F7"; "0xCCCCCC"; "0x969696"; "0x636363"; "0x252525"|];
            [|"0xF7F7F7"; "0xD9D9D9"; "0xBDBDBD"; "0x969696"; "0x636363"; "0x252525"|];
            [|"0xF7F7F7"; "0xD9D9D9"; "0xBDBDBD"; "0x969696"; "0x737373"; "0x525252"; "0x252525"|];
            [|"0xFFFFFF"; "0xF0F0F0"; "0xD9D9D9"; "0xBDBDBD"; "0x969696"; "0x737373"; "0x525252"; "0x252525"|];
            [|"0xFFFFFF"; "0xF0F0F0"; "0xD9D9D9"; "0xBDBDBD"; "0x969696"; "0x737373"; "0x525252"; "0x252525"; "0x000000"|];
        |]
    }
            
    let Oranges = {
        paletteType = SequentialSingle
        paletteDescription = "Orange shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xFEE6CE"|];
            [|"0xFEE6CE"; "0xE6550D"|];
            [|"0xFEE6CE"; "0xFDAE6B"; "0xE6550D"|];
            [|"0xFEEDDE"; "0xFDBE85"; "0xFD8D3C"; "0xD94701"|];
            [|"0xFEEDDE"; "0xFDBE85"; "0xFD8D3C"; "0xE6550D"; "0xA63603"|];
            [|"0xFEEDDE"; "0xFDD0A2"; "0xFDAE6B"; "0xFD8D3C"; "0xE6550D"; "0xA63603"|];
            [|"0xFEEDDE"; "0xFDD0A2"; "0xFDAE6B"; "0xFD8D3C"; "0xF16913"; "0xD94801"; "0x8C2D04"|];
            [|"0xFFF5EB"; "0xFEE6CE"; "0xFDD0A2"; "0xFDAE6B"; "0xFD8D3C"; "0xF16913"; "0xD94801"; "0x8C2D04"|];
            [|"0xFFF5EB"; "0xFEE6CE"; "0xFDD0A2"; "0xFDAE6B"; "0xFD8D3C"; "0xF16913"; "0xD94801"; "0xA63603"; "0x7F2704"|];
        |]
    }
            
    let OrangeRedShades = {
        paletteType = SequentialMulti
        paletteDescription = "Orange-Red shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xFEE8C8"|];
            [|"0xFEE8C8"; "0xE34A33"|];
            [|"0xFEE8C8"; "0xFDBB84"; "0xE34A33"|];
            [|"0xFEF0D9"; "0xFDCC8A"; "0xFC8D59"; "0xD7301F"|];
            [|"0xFEF0D9"; "0xFDCC8A"; "0xFC8D59"; "0xE34A33"; "0xB30000"|];
            [|"0xFEF0D9"; "0xFDD49E"; "0xFDBB84"; "0xFC8D59"; "0xE34A33"; "0xB30000"|];
            [|"0xFEF0D9"; "0xFDD49E"; "0xFDBB84"; "0xFC8D59"; "0xEF6548"; "0xD7301F"; "0x990000"|];
            [|"0xFFF7EC"; "0xFEE8C8"; "0xFDD49E"; "0xFDBB84"; "0xFC8D59"; "0xEF6548"; "0xD7301F"; "0x990000"|];
            [|"0xFFF7EC"; "0xFEE8C8"; "0xFDD49E"; "0xFDBB84"; "0xFC8D59"; "0xEF6548"; "0xD7301F"; "0xB30000"; "0x7F0000"|];
        |]
    }
            
    let PurpleBlueShades = {
        paletteType = SequentialMulti
        paletteDescription = "Purple-Blue shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xECE7F2"|];
            [|"0xECE7F2"; "0x2B8CBE"|];
            [|"0xECE7F2"; "0xA6BDDB"; "0x2B8CBE"|];
            [|"0xF1EEF6"; "0xBDC9E1"; "0x74A9CF"; "0x0570B0"|];
            [|"0xF1EEF6"; "0xBDC9E1"; "0x74A9CF"; "0x2B8CBE"; "0x045A8D"|];
            [|"0xF1EEF6"; "0xD0D1E6"; "0xA6BDDB"; "0x74A9CF"; "0x2B8CBE"; "0x045A8D"|];
            [|"0xF1EEF6"; "0xD0D1E6"; "0xA6BDDB"; "0x74A9CF"; "0x3690C0"; "0x0570B0"; "0x034E7B"|];
            [|"0xFFF7FB"; "0xECE7F2"; "0xD0D1E6"; "0xA6BDDB"; "0x74A9CF"; "0x3690C0"; "0x0570B0"; "0x034E7B"|];
            [|"0xFFF7FB"; "0xECE7F2"; "0xD0D1E6"; "0xA6BDDB"; "0x74A9CF"; "0x3690C0"; "0x0570B0"; "0x045A8D"; "0x023858"|];
        |]
    }
    
    let PurpleBlueGreenShades = {
        paletteType = SequentialMulti
        paletteDescription = "Purple-Blue-Green shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xECE2F0"|];
            [|"0xECE2F0"; "0x1C9099"|];
            [|"0xECE2F0"; "0xA6BDDB"; "0x1C9099"|];
            [|"0xF6EFF7"; "0xBDC9E1"; "0x67A9CF"; "0x02818A"|];
            [|"0xF6EFF7"; "0xBDC9E1"; "0x67A9CF"; "0x1C9099"; "0x016C59"|];
            [|"0xF6EFF7"; "0xD0D1E6"; "0xA6BDDB"; "0x67A9CF"; "0x1C9099"; "0x016C59"|];
            [|"0xF6EFF7"; "0xD0D1E6"; "0xA6BDDB"; "0x67A9CF"; "0x3690C0"; "0x02818A"; "0x016450"|];
            [|"0xFFF7FB"; "0xECE2F0"; "0xD0D1E6"; "0xA6BDDB"; "0x67A9CF"; "0x3690C0"; "0x02818A"; "0x016450"|];
            [|"0xFFF7FB"; "0xECE2F0"; "0xD0D1E6"; "0xA6BDDB"; "0x67A9CF"; "0x3690C0"; "0x02818A"; "0x016C59"; "0x014636"|];
        |]
    }
            
    let PurpleRedShades = {
        paletteType = SequentialMulti
        paletteDescription = "Purple-Red shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xE7E1EF"|];
            [|"0xE7E1EF"; "0xDD1C77"|];
            [|"0xE7E1EF"; "0xC994C7"; "0xDD1C77"|];
            [|"0xF1EEF6"; "0xD7B5D8"; "0xDF65B0"; "0xCE1256"|];
            [|"0xF1EEF6"; "0xD7B5D8"; "0xDF65B0"; "0xDD1C77"; "0x980043"|];
            [|"0xF1EEF6"; "0xD4B9DA"; "0xC994C7"; "0xDF65B0"; "0xDD1C77"; "0x980043"|];
            [|"0xF1EEF6"; "0xD4B9DA"; "0xC994C7"; "0xDF65B0"; "0xE7298A"; "0xCE1256"; "0x91003F"|];
            [|"0xF7F4F9"; "0xE7E1EF"; "0xD4B9DA"; "0xC994C7"; "0xDF65B0"; "0xE7298A"; "0xCE1256"; "0x91003F"|];
            [|"0xF7F4F9"; "0xE7E1EF"; "0xD4B9DA"; "0xC994C7"; "0xDF65B0"; "0xE7298A"; "0xCE1256"; "0x980043"; "0x67001F"|];
        |]
    }
    
    let Purples = {
        paletteType = SequentialSingle
        paletteDescription = "Purple shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xEFEDF5"|];
            [|"0xEFEDF5"; "0x756BB1"|];
            [|"0xEFEDF5"; "0xBCBDDC"; "0x756BB1"|];
            [|"0xF2F0F7"; "0xCBC9E2"; "0x9E9AC8"; "0x6A51A3"|];
            [|"0xF2F0F7"; "0xCBC9E2"; "0x9E9AC8"; "0x756BB1"; "0x54278F"|];
            [|"0xF2F0F7"; "0xDADAEB"; "0xBCBDDC"; "0x9E9AC8"; "0x756BB1"; "0x54278F"|];
            [|"0xF2F0F7"; "0xDADAEB"; "0xBCBDDC"; "0x9E9AC8"; "0x807DBA"; "0x6A51A3"; "0x4A1486"|];
            [|"0xFCFBFD"; "0xEFEDF5"; "0xDADAEB"; "0xBCBDDC"; "0x9E9AC8"; "0x807DBA"; "0x6A51A3"; "0x4A1486"|];
            [|"0xFCFBFD"; "0xEFEDF5"; "0xDADAEB"; "0xBCBDDC"; "0x9E9AC8"; "0x807DBA"; "0x6A51A3"; "0x54278F"; "0x3F007D"|];
        |]
    }
            
    let RedPurpleShades = {
        paletteType = SequentialMulti
        paletteDescription = "Red-Purple shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xFDE0DD"|];
            [|"0xFDE0DD"; "0xC51B8A"|];
            [|"0xFDE0DD"; "0xFA9FB5"; "0xC51B8A"|];
            [|"0xFEEBE2"; "0xFBB4B9"; "0xF768A1"; "0xAE017E"|];
            [|"0xFEEBE2"; "0xFBB4B9"; "0xF768A1"; "0xC51B8A"; "0x7A0177"|];
            [|"0xFEEBE2"; "0xFCC5C0"; "0xFA9FB5"; "0xF768A1"; "0xC51B8A"; "0x7A0177"|];
            [|"0xFEEBE2"; "0xFCC5C0"; "0xFA9FB5"; "0xF768A1"; "0xDD3497"; "0xAE017E"; "0x7A0177"|];
            [|"0xFFF7F3"; "0xFDE0DD"; "0xFCC5C0"; "0xFA9FB5"; "0xF768A1"; "0xDD3497"; "0xAE017E"; "0x7A0177"|];
            [|"0xFFF7F3"; "0xFDE0DD"; "0xFCC5C0"; "0xFA9FB5"; "0xF768A1"; "0xDD3497"; "0xAE017E"; "0x7A0177"; "0x49006A"|];
        |]
    }
            
    let Reds = {
        paletteType = SequentialSingle
        paletteDescription = "Red shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xFEE0D2"|];
            [|"0xFEE0D2"; "0xDE2D26"|];
            [|"0xFEE0D2"; "0xFC9272"; "0xDE2D26"|];
            [|"0xFEE5D9"; "0xFCAE91"; "0xFB6A4A"; "0xCB181D"|];
            [|"0xFEE5D9"; "0xFCAE91"; "0xFB6A4A"; "0xDE2D26"; "0xA50F15"|];
            [|"0xFEE5D9"; "0xFCBBA1"; "0xFC9272"; "0xFB6A4A"; "0xDE2D26"; "0xA50F15"|];
            [|"0xFEE5D9"; "0xFCBBA1"; "0xFC9272"; "0xFB6A4A"; "0xEF3B2C"; "0xCB181D"; "0x99000D"|];
            [|"0xFFF5F0"; "0xFEE0D2"; "0xFCBBA1"; "0xFC9272"; "0xFB6A4A"; "0xEF3B2C"; "0xCB181D"; "0x99000D"|];
            [|"0xFFF5F0"; "0xFEE0D2"; "0xFCBBA1"; "0xFC9272"; "0xFB6A4A"; "0xEF3B2C"; "0xCB181D"; "0xA50F15"; "0x67000D"|]; 
        |]
    }
    
    let YellowGreenShades = {
        paletteType = SequentialMulti
        paletteDescription = "Yellow-Green shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xF7FCB9"|];
            [|"0xF7FCB9"; "0x31A354"|];
            [|"0xF7FCB9"; "0xADDD8E"; "0x31A354"|];
            [|"0xFFFFCC"; "0xC2E699"; "0x78C679"; "0x238443"|];
            [|"0xFFFFCC"; "0xC2E699"; "0x78C679"; "0x31A354"; "0x006837"|];
            [|"0xFFFFCC"; "0xD9F0A3"; "0xADDD8E"; "0x78C679"; "0x31A354"; "0x006837"|];
            [|"0xFFFFCC"; "0xD9F0A3"; "0xADDD8E"; "0x78C679"; "0x41AB5D"; "0x238443"; "0x005A32"|];
            [|"0xFFFFE5"; "0xF7FCB9"; "0xD9F0A3"; "0xADDD8E"; "0x78C679"; "0x41AB5D"; "0x238443"; "0x005A32"|];
            [|"0xFFFFE5"; "0xF7FCB9"; "0xD9F0A3"; "0xADDD8E"; "0x78C679"; "0x41AB5D"; "0x238443"; "0x006837"; "0x004529"|];
        |]
    }
    
    let YellowGreenBlueShades = {
        paletteType = SequentialMulti
        paletteDescription = "Yellow-Green-Blue shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xEDF8B1"|];
            [|"0xEDF8B1"; "0x2C7FB8"|];
            [|"0xEDF8B1"; "0x7FCDBB"; "0x2C7FB8"|];
            [|"0xFFFFCC"; "0xA1DAB4"; "0x41B6C4"; "0x225EA8"|];
            [|"0xFFFFCC"; "0xA1DAB4"; "0x41B6C4"; "0x2C7FB8"; "0x253494"|];
            [|"0xFFFFCC"; "0xC7E9B4"; "0x7FCDBB"; "0x41B6C4"; "0x2C7FB8"; "0x253494"|];
            [|"0xFFFFCC"; "0xC7E9B4"; "0x7FCDBB"; "0x41B6C4"; "0x1D91C0"; "0x225EA8"; "0x0C2C84"|];
            [|"0xFFFFD9"; "0xEDF8B1"; "0xC7E9B4"; "0x7FCDBB"; "0x41B6C4"; "0x1D91C0"; "0x225EA8"; "0x0C2C84"|];
            [|"0xFFFFD9"; "0xEDF8B1"; "0xC7E9B4"; "0x7FCDBB"; "0x41B6C4"; "0x1D91C0"; "0x225EA8"; "0x253494"; "0x081D58"|];
        |]
    }
    
    let YellowOrangeBrownShades = {
        paletteType = SequentialMulti
        paletteDescription = "Yellow-Orange-Brown shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xFFF7BC"|];
            [|"0xFFF7BC"; "0xD95F0E"|];
            [|"0xFFF7BC"; "0xFEC44F"; "0xD95F0E"|];
            [|"0xFFFFD4"; "0xFED98E"; "0xFE9929"; "0xCC4C02"|];
            [|"0xFFFFD4"; "0xFED98E"; "0xFE9929"; "0xD95F0E"; "0x993404"|];
            [|"0xFFFFD4"; "0xFEE391"; "0xFEC44F"; "0xFE9929"; "0xD95F0E"; "0x993404"|];
            [|"0xFFFFD4"; "0xFEE391"; "0xFEC44F"; "0xFE9929"; "0xEC7014"; "0xCC4C02"; "0x8C2D04"|];
            [|"0xFFFFE5"; "0xFFF7BC"; "0xFEE391"; "0xFEC44F"; "0xFE9929"; "0xEC7014"; "0xCC4C02"; "0x8C2D04"|];
            [|"0xFFFFE5"; "0xFFF7BC"; "0xFEE391"; "0xFEC44F"; "0xFE9929"; "0xEC7014"; "0xCC4C02"; "0x993404"; "0x662506"|];
        |]
    }
    
    let YellowOrangeRedShades = {
        paletteType = SequentialMulti
        paletteDescription = "Yellow-Orange-Red shades"
        colorBlindSave = true
        hexColors = [|
            [|"0xFFEDA0"|];
            [|"0xFFEDA0"; "0xF03B20"|];
            [|"0xFFEDA0"; "0xFEB24C"; "0xF03B20"|];
            [|"0xFFFFB2"; "0xFECC5C"; "0xFD8D3C"; "0xE31A1C"|];
            [|"0xFFFFB2"; "0xFECC5C"; "0xFD8D3C"; "0xF03B20"; "0xBD0026"|];
            [|"0xFFFFB2"; "0xFED976"; "0xFEB24C"; "0xFD8D3C"; "0xF03B20"; "0xBD0026"|];
            [|"0xFFFFB2"; "0xFED976"; "0xFEB24C"; "0xFD8D3C"; "0xFC4E2A"; "0xE31A1C"; "0xB10026"|];
            [|"0xFFFFCC"; "0xFFEDA0"; "0xFED976"; "0xFEB24C"; "0xFD8D3C"; "0xFC4E2A"; "0xE31A1C"; "0xB10026"|];
            [|"0xFFFFCC"; "0xFFEDA0"; "0xFED976"; "0xFEB24C"; "0xFD8D3C"; "0xFC4E2A"; "0xE31A1C"; "0xBD0026"; "0x800026"|];
        |]
    }
    
    let HSVRedBlue = {
        paletteType = Diverging
        paletteDescription = "HSV Red-Blue"
        colorBlindSave = true
        hexColors = [|
            [|"0xFF0000"|];
            [|"0xFF0000"; "0x0000FF"|];
            [|"0xFF0000"; "0xFFFFFF"; "0x0000FF"|];
            [|"0xFF0000"; "0xFFAAAA"; "0xAAAAFF"; "0x0000FF"|];
            [|"0xFF0000"; "0xFF8080"; "0xFFFFFF"; "0x8080FF"; "0x0000FF"|];
            [|"0xFF0000"; "0xFF6666"; "0xFFCCCC"; "0xCCCCFF"; "0x6666FF"; "0x0000FF"|];
            [|"0xFF0000"; "0xFF5555"; "0xFFAAAA"; "0xFFFFFF"; "0xAAAAFF"; "0x5555FF"; "0x0000FF"|];
            [|"0xFF0000"; "0xFF4949"; "0xFF9292"; "0xFFDBDB"; "0xDBDBFF"; "0x9292FF"; "0x4949FF"; "0x0000FF"|];
            [|"0xFF0000"; "0xFF4040"; "0xFF8080"; "0xFFBFBF"; "0xFFFFFF"; "0xBFBFFF"; "0x8080FF"; "0x4040FF"; "0x0000FF"|];
            [|"0xFF0000"; "0xFF3939"; "0xFF7171"; "0xFFAAAA"; "0xFFE3E3"; "0xE3E3FF"; "0xAAAAFF"; "0x7171FF"; "0x3939FF"; "0x0000FF"|];
            [|"0xFF0000"; "0xFF3333"; "0xFF6666"; "0xFF9999"; "0xFFCCCC"; "0xFFFFFF"; "0xCCCCFF"; "0x9999FF"; "0x6666FF"; "0x3333FF"; "0x0000FF"|];
        |]
    }
    
    let HSVCyanMagenta = {
        paletteType = Diverging
        paletteDescription = "HSV Cyan-Magenta"
        colorBlindSave = true
        hexColors = [|
            [|"0x00FFFF"|];
            [|"0x00FFFF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0xFFFFFF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0xAAFFFF"; "0xFFAAFF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0x80FFFF"; "0xFFFFFF"; "0xFF80FF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0x66FFFF"; "0xCCFFFF"; "0xFFCCFF"; "0xFF66FF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0x55FFFF"; "0xAAFFFF"; "0xFFFFFF"; "0xFFAAFF"; "0xFF55FF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0x49FFFF"; "0x92FFFF"; "0xDBFFFF"; "0xFFDBFF"; "0xFF92FF"; "0xFF49FF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0x40FFFF"; "0x80FFFF"; "0xBFFFFF"; "0xFFFFFF"; "0xFFBFFF"; "0xFF80FF"; "0xFF40FF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0x39FFFF"; "0x71FFFF"; "0xAAFFFF"; "0xE3FFFF"; "0xFFE3FF"; "0xFFAAFF"; "0xFF71FF"; "0xFF39FF"; "0xFF00FF"|];
            [|"0x00FFFF"; "0x33FFFF"; "0x66FFFF"; "0x99FFFF"; "0xCCFFFF"; "0xFFFFFF"; "0xFFCCFF"; "0xFF99FF"; "0xFF66FF"; "0xFF33FF"; "0xFF00FF"|];
        |]
    }

type ColorBrewer = {
    palettes : Set<BrewerPalette>
} with 
    member x.PalettesOfType (paletteType: PaletteType) = 
        x.palettes |> Set.filter (fun x -> x.paletteType = paletteType)

module ColorBrewer = 
    
    let all = { palettes =
        [
            ColorBrewerPalettes.BrownBlueGreen
            ColorBrewerPalettes.MagentaYellowGreen
            ColorBrewerPalettes.PurpleRedGreen
            ColorBrewerPalettes.PurpleOrange
            ColorBrewerPalettes.RedBlue
            ColorBrewerPalettes.RedGrey
            ColorBrewerPalettes.RedYellowBlue
            ColorBrewerPalettes.RedYellowGreen
            ColorBrewerPalettes.Spectral
            ColorBrewerPalettes.Accent
            ColorBrewerPalettes.Dark
            ColorBrewerPalettes.Paired
            ColorBrewerPalettes.Pastel1
            ColorBrewerPalettes.Pastel2
            ColorBrewerPalettes.Set1
            ColorBrewerPalettes.Set2
            ColorBrewerPalettes.Set3
            ColorBrewerPalettes.Blues
            ColorBrewerPalettes.BlueGreenShades
            ColorBrewerPalettes.BluePurpleShades
            ColorBrewerPalettes.GreenBlueShades
            ColorBrewerPalettes.Greens
            ColorBrewerPalettes.Greys
            ColorBrewerPalettes.Oranges
            ColorBrewerPalettes.OrangeRedShades
            ColorBrewerPalettes.PurpleBlueShades
            ColorBrewerPalettes.PurpleBlueGreenShades
            ColorBrewerPalettes.PurpleRedShades
            ColorBrewerPalettes.Purples
            ColorBrewerPalettes.RedPurpleShades
            ColorBrewerPalettes.Reds
            ColorBrewerPalettes.YellowGreenShades
            ColorBrewerPalettes.YellowGreenBlueShades
            ColorBrewerPalettes.YellowOrangeBrownShades
            ColorBrewerPalettes.YellowOrangeRedShades
            ColorBrewerPalettes.HSVRedBlue
            ColorBrewerPalettes.HSVCyanMagenta
        ] 
        |> Set.ofList
    }

    let palettesOfType (paletteType : PaletteType) =
        all.PalettesOfType paletteType