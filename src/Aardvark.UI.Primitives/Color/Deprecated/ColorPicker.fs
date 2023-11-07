namespace Aardvark.UI

#nowarn "44"

open System
open Aardvark.Base
open Aardvark.UI.Operators
open FSharp.Data.Adaptive

[<Obsolete("Use ColorPicker from Aardvark.UI.Primitives.ColorPicker2 instead.")>]
module ColorPicker =
    type Action =
        | SetColor of ColorInput

    let spectrum =
        [
            { kind = Stylesheet; name = "spectrumStyle"; url = "resources/spectrum.css" }
            { kind = Script; name = "spectrum"; url = "resources/spectrum.js" }
        ]

    let update (model : ColorInput) (action : Action) =
        match action with
            | SetColor c -> c

    let init = { c = C4b.VRVisGreen }

    let colorFromHex (hex:string) =
        Log.warn "%s" (hex.Replace("#", ""))
        let arr =
            hex.Replace("#", "")
                |> Seq.windowed 2
                |> Seq.mapi   (fun i j -> (i,j))
                |> Seq.filter (fun (i,j) -> i % 2=0)
                |> Seq.map    (fun (_,j) -> Byte.Parse(new System.String(j),System.Globalization.NumberStyles.AllowHexSpecifier))
                |> Array.ofSeq

        C4b(arr.[0], arr.[1], arr.[2], 255uy)

    let colorToHex (color : C4b) : string =
        let bytes = [| color.R; color.G; color.B |]
        bytes
            |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
            |> String.concat System.String.Empty

    let view (model : AdaptiveColorInput) =
        require spectrum (
            onBoot "$('#__ID__').spectrum(
                        {
                            showPalette: true,
                            palette: [
                                ['#000','#444','#666','#999','#ccc','#eee','#f3f3f3','#fff'],
                                ['#f00','#f90','#ff0','#0f0','#0ff','#00f','#90f','#f0f'],
                                ['#f4cccc','#fce5cd','#fff2cc','#d9ead3','#d0e0e3','#cfe2f3','#d9d2e9','#ead1dc'],
                                ['#ea9999','#f9cb9c','#ffe599','#b6d7a8','#a2c4c9','#9fc5e8','#b4a7d6','#d5a6bd'],
                                ['#e06666','#f6b26b','#ffd966','#93c47d','#76a5af','#6fa8dc','#8e7cc3','#c27ba0'],
                                ['#c00','#e69138','#f1c232','#6aa84f','#45818e','#3d85c6','#674ea7','#a64d79'],
                                ['#900','#b45f06','#bf9000','#38761d','#134f5c','#0b5394','#351c75','#741b47'],
                                ['#600','#783f04','#7f6000','#274e13','#0c343d','#073763','#20124d','#4c1130']
                            ],
                            showSelectionPalette: true,
                            localStorageKey: 'spectrum.homepage',
                            preferredFormat: 'hex',
                            showInput: true
                            });" (
                let attributes =
                    amap {
                        yield "type" => "text"
                        yield onChange (fun d -> { c = colorFromHex d }|> SetColor)

                        let! color = model.c
                        yield "value" => colorToHex color
                    }

                Incremental.input (AttributeMap.ofAMap attributes)
        ))


    let defaultPalette =
        """[
                ['#000','#444','#666','#999','#ccc','#eee','#f3f3f3','#fff'],
                ['#f00','#f90','#ff0','#0f0','#0ff','#00f','#90f','#f0f'],
                ['#f4cccc','#fce5cd','#fff2cc','#d9ead3','#d0e0e3','#cfe2f3','#d9d2e9','#ead1dc'],
                ['#ea9999','#f9cb9c','#ffe599','#b6d7a8','#a2c4c9','#9fc5e8','#b4a7d6','#d5a6bd'],
                ['#e06666','#f6b26b','#ffd966','#93c47d','#76a5af','#6fa8dc','#8e7cc3','#c27ba0'],
                ['#c00','#e69138','#f1c232','#6aa84f','#45818e','#3d85c6','#674ea7','#a64d79'],
                ['#900','#b45f06','#bf9000','#38761d','#134f5c','#0b5394','#351c75','#741b47'],
                ['#600','#783f04','#7f6000','#274e13','#0c343d','#073763','#20124d','#4c1130'],
                FAVORITES
           ]
       """

    type Palette = array<string>

    open System.IO

    let parsePalette (s : string) : Palette =
        Pickler.unpickleOfJson(s)

    let readPalette (paletteFile : string) =
        try
            if File.Exists paletteFile then
                try File.ReadAllText(paletteFile) |> parsePalette |> Some
                with e ->
                    Log.warn "[colorPicker] could not parse: %A" e.Message
                    File.WriteAllText(paletteFile, "[]")
                    defaultPalette |> parsePalette  |> Some
            else
                File.WriteAllText(paletteFile, "[]")
                "[]" |> parsePalette |> Some
        with e ->
            Log.warn "[colorPicker] %A" e.Message
            None

    let viewColorBrewer (rowElementCount: int) (paletteType: PaletteType) (model : AdaptiveColorInput) =

        let rows =
            paletteType
            |> ColorBrewer.palettesOfType
            |> Set.map (BrewerPalette.spectrumRow rowElementCount)
            |> String.concat ","

        let bootCode =
            sprintf """$('#__ID__').spectrum(
                {
                    showPalette: true,
                    showPaletteOnly: true,
                    palette: [ %s ],
                    preferredFormat: 'hex',
                    showInput: true
                    });
            """ rows

        require spectrum (
            onBoot bootCode (
                let attributes =
                    amap {
                        yield "type" => "text"
                        yield onChange (fun d ->  { c = colorFromHex d } |> SetColor)

                        let! color = model.c
                        yield "value" => colorToHex color
                    }

                Incremental.input (AttributeMap.ofAMap attributes)
        ))

    let viewColorBrewerPalette (rowElementCount: int) (palette: BrewerPalette) (model : AdaptiveColorInput) =

        let bootCode =
            sprintf """$('#__ID__').spectrum(
                {
                    showPalette: true,
                    showPaletteOnly: true,
                    palette: [ %s ],
                    preferredFormat: 'hex',
                    showInput: true
                    });
            """ (palette.SpectrumRow 20)

        require spectrum (
            onBoot bootCode (
                let attributes =
                    amap {
                        yield "type" => "text"
                        yield onChange (fun d ->  { c = colorFromHex d } |> SetColor)

                        let! color = model.c
                        yield "value" => colorToHex color
                    }

                Incremental.input (AttributeMap.ofAMap attributes)
        ))




    let viewAdvanced (defaultPalette : string) (paletteFile : string) (localStorageKey : string) (model : AdaptiveColorInput) =

        let favorites = readPalette paletteFile
        let favoritesJson =
            match favorites with
            | Some f -> Pickler.json.PickleToString(f)
            | None -> "[]"

        let addHex (hex : string) =
            try
                match readPalette paletteFile with
                | None -> ()
                | Some h ->
                    let hs = Array.toList h @ [hex] |> List.distinct
                    let favorites = hs |> Seq.atMost 15 |> Seq.toArray
                    let str = Pickler.json.PickleToString(favorites).Replace("\"","'")
                    File.WriteAllText(paletteFile, str)
                    ()
            with e ->
                Log.warn "[colorpicker] addtoHex - %s" e.Message



        let bootCode =
            sprintf """$('#__ID__').spectrum(
            {
                showPalette: true,
                palette: %s,
                showSelectionPalette: true,
                localStorageKey: '%s',
                preferredFormat: 'hex',
                showInput: true
            });
            """ (defaultPalette.Replace("FAVORITES", favoritesJson).Replace("\"","'")) localStorageKey

        require spectrum (
            onBoot bootCode (
                let attributes =
                    amap {
                        yield "type" => "text"
                        yield onChange (fun d ->  { c = colorFromHex d } |> SetColor)

                        let! color = model.c
                        let hex = colorToHex color
                        addHex hex // store possibly extern changed colors as favorites
                        yield "value" => colorToHex color
                    }

                Incremental.input (AttributeMap.ofAMap attributes)
        ))


    let viewSimple (color : aval<C4b>) (change : C4b -> 'msg) =
        require spectrum (
            onBoot "$('#__ID__').spectrum(
                        {
                            showPalette: true,
                            palette: [
                                ['#000','#444','#666','#999','#ccc','#eee','#f3f3f3','#fff'],
                                ['#f00','#f90','#ff0','#0f0','#0ff','#00f','#90f','#f0f'],
                                ['#f4cccc','#fce5cd','#fff2cc','#d9ead3','#d0e0e3','#cfe2f3','#d9d2e9','#ead1dc'],
                                ['#ea9999','#f9cb9c','#ffe599','#b6d7a8','#a2c4c9','#9fc5e8','#b4a7d6','#d5a6bd'],
                                ['#e06666','#f6b26b','#ffd966','#93c47d','#76a5af','#6fa8dc','#8e7cc3','#c27ba0'],
                                ['#c00','#e69138','#f1c232','#6aa84f','#45818e','#3d85c6','#674ea7','#a64d79'],
                                ['#900','#b45f06','#bf9000','#38761d','#134f5c','#0b5394','#351c75','#741b47'],
                                ['#600','#783f04','#7f6000','#274e13','#0c343d','#073763','#20124d','#4c1130']
                            ],
                            showSelectionPalette: true,
                            localStorageKey: 'spectrum.homepage',
                            preferredFormat: 'hex',
                            showInput: true
                            });" (
                let attributes =
                    amap {
                        yield "type" => "text"
                        yield onChange (change << colorFromHex)

                        let! color = color
                        yield "value" => colorToHex color
                    }

                Incremental.input (AttributeMap.ofAMap attributes)
        ))

    let app : App<ColorInput, AdaptiveColorInput, Action> =
        {
            unpersist = Unpersist.instance
            threads = fun _ -> ThreadPool.empty
            initial = init
            update = update
            view = view
        }

    let start () =
        app |> App.start