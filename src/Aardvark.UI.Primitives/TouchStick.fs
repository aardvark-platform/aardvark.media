namespace Aardvark.UI.Primitives

open System
open Aardvark.Base
open Aardvark.UI

module TouchStick =
    
    type StickConfig =
        {
            name : string
            area : Box2d
            radius : float
        }

    let withTouchSticks ( configs : list<StickConfig> ) el =
        let rs = 
            [
                { name = "touchstick.js"; url = "touchstick.js"; kind = Script }
                { name = "touch.css"; url = "touch.css"; kind = Stylesheet }
                { name = "hammerjs"; url = "https://cdnjs.cloudflare.com/ajax/libs/hammer.js/2.0.8/hammer.js"; kind = Script }
            ]       

        let str = 
            configs |> List.map ( fun cfg -> 
                sprintf "initTouchStick('__ID__', '%s', %f, %f, %f, %f, %f)" cfg.name cfg.area.Min.X cfg.area.Max.X cfg.area.Min.Y cfg.area.Max.Y cfg.radius
            )|> String.concat ";"

        require rs (
            onBoot str (
                el
            )
        )

    type TouchStickState =
        {
            distance : float
            angle : float
        }

    let onTouchStickStart name f =
        onEvent ("touchstickstart_"+name) [] (( fun args -> 
            match args with
            | [d;a;x;y] -> 
                let stick = { distance = float d; angle = float a }
                let pos = V2d(float x,float y)
                f stick pos
            | _ -> failwith ""
        ))

    let onTouchStickMove name f =
        onEvent ("touchstickmove_"+name) [] (( fun args -> 
            match args with
            | [d;a] -> { distance = float d; angle = float a } |> f
            | _ -> failwith ""
        ))

    let onTouchStickStop name f =
        onEvent ("touchstickstop_"+name) [] ( fun _ -> f() )