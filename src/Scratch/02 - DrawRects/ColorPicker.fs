namespace Aardvark.UI

open System
open Aardvark.Base

module Spectrum =

    let bootCode ="""
                var el =  $('#__ID__');
                el.spectrum(
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
                    showInput: true,
                    disabled: true,
                    change: function (c) { 
                        aardvark.processEvent('__ID__', 'changeColor', c.toHexString());
                    },
                    color: '__COLOR__'
                });

                var draggging = false;
                el.click(function (e) { if(!dragging) { el.spectrum("reflow"); el.spectrum("show"); }});
                el.mousedown(function (e) { dragging = false; });
                el.mousemove(function(e) { dragging = true; });
                """

     
    let colorFromHex (hex:string) =
        Log.warn "%s" (hex.Replace("#", ""))
        let arr =
            hex.Replace("#", "")
                |> Seq.windowed 2
                |> Seq.mapi   (fun i j -> (i,j))
                |> Seq.filter (fun (i,j) -> i % 2=0)
                |> Seq.map    (fun (_,j) -> Byte.Parse(new System.String(j),System.Globalization.NumberStyles.AllowHexSpecifier))
                |> Array.ofSeq

        C4b(arr.[0], arr.[1], arr.[2], 255uy).ToC4f()