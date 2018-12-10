namespace Utils

open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Operators

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Application

module Nvg =
    let floatString (v : float) =
        System.String.Format(System.Globalization.CultureInfo.InvariantCulture,"{0}", v)

    let rect x y width height attributes = 
        Svg.rect (
            [
                "x" => floatString x; "y"  => floatString y;
                "width" => floatString width; "height" =>  floatString height;
            ] @ attributes
        )

    let line (p0 : V2d) (p1 : V2d) attributes =
        Svg.line (
            [
                "x1" => floatString p0.X; "y1" => floatString p0.Y;
                "x2" => floatString p1.X; "y2" => floatString p1.Y;
            ] @ attributes
        )

module Geometry = 

    open System

    let indices =
        [|
            1;2;6; 1;6;5
            2;3;7; 2;7;6
            4;5;6; 4;6;7
            3;0;4; 3;4;7
            0;1;5; 0;5;4
            0;3;2; 0;2;1
        |]

    let unitBox =
        let box = Box3d.Unit

        let positions = 
            [|
                V3f(box.Min.X, box.Min.Y, box.Min.Z)
                V3f(box.Max.X, box.Min.Y, box.Min.Z)
                V3f(box.Max.X, box.Max.Y, box.Min.Z)
                V3f(box.Min.X, box.Max.Y, box.Min.Z)
                V3f(box.Min.X, box.Min.Y, box.Max.Z)
                V3f(box.Max.X, box.Min.Y, box.Max.Z)
                V3f(box.Max.X, box.Max.Y, box.Max.Z)
                V3f(box.Min.X, box.Max.Y, box.Max.Z)
            |]

        let normals = 
            [| 
                V3f.IOO;
                V3f.OIO;
                V3f.OOI;

                -V3f.IOO;
                -V3f.OIO;
                -V3f.OOI;
            |]

        let texcoords =
            [|
                V2f.OO; V2f.IO; V2f.II;  V2f.OO; V2f.II; V2f.OI
            |]

        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,

            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                    DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                    DefaultSemantic.DiffuseColorCoordinates, indices |> Array.mapi (fun ti _ -> texcoords.[ti % 6]) :> Array
                ]

        )

    let createQuad (vertices : IMod<array<V2f>>) (colors : IMod<array<C4f>>) =
        let drawCall = 
            DrawCallInfo(
                FaceVertexCount = 4,
                InstanceCount = 1
            )

        let positions = 
            // strip: [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
            vertices |> Mod.map (fun arr -> 
                [| V3f(arr.[0],0.0f); V3f(arr.[1],0.0f); V3f(arr.[3],0.0f); V3f(arr.[2],0.0f) |]
            )
    
        let colors = 
            colors |> Mod.map (fun arr -> 
                [| arr.[0]; arr.[1]; arr.[3]; arr.[2] |]
            )
        
        let texcoords =     
            [| V2f(0,0); V2f(1,0); V2f(0,1); V2f(1,1) |]
            
        drawCall
            |> Sg.render IndexedGeometryMode.TriangleStrip 
            |> Sg.vertexAttribute DefaultSemantic.Positions positions
            |> Sg.vertexAttribute DefaultSemantic.Colors colors
            |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant texcoords)


    let box (colors : IMod<C4b[]>) (bounds : IMod<Box3d>) =
        let trafo = bounds |> Mod.map (fun box -> Trafo3d.Scale(box.Size) * Trafo3d.Translation(box.Min))
        SgPrimitives.Primitives.unitBox
            |> Sg.ofIndexedGeometry
            |> Sg.vertexAttribute DefaultSemantic.Colors colors
            |> Sg.trafo trafo

module Spectrum =
    open System


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
                        debugger;
                        aardvark.processEvent('__ID__', 'changeColor', c.toHexString(), __INDEX__);
                    },
                    color: '__COLOR__'
                });

                var draggging = false;
                el.click(function (e) { if(!dragging) { var el =  $('#__ID__'); el.spectrum("reflow"); el.spectrum("show"); debugger; }});
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