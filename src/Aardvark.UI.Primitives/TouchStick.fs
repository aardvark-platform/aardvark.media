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
            ]       

        let str =
            sprintf
                "initTouchSticks('__ID__',[%s]);"
                (configs 
                    |> List.map ( fun cfg -> 
                        sprintf "{name:'%s',minx:%f,maxx:%f,miny:%f,maxy:%f,maxr:%f}" 
                            cfg.name 
                            cfg.area.Min.X 
                            cfg.area.Max.X  
                            cfg.area.Min.Y 
                            cfg.area.Max.Y 
                            cfg.radius
                        )
                    |> String.concat ","
                )

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
            | [x;y] -> 
                let pos = V2d(float x,float y)
                f pos
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