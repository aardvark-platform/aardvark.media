namespace Aardvark.UI.Primitives

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
        let dependencies =
            [
                { name = "touchstick.js"; url = "resources/touchstick.js"; kind = Script }
            ]

        let boot =
            let configs =
                configs |> List.map (fun cfg ->
                    sprintf "{name:'%s',minx:%f,maxx:%f,miny:%f,maxy:%f,maxr:%f}"
                        cfg.name
                        cfg.area.Min.X
                        cfg.area.Max.X
                        cfg.area.Min.Y
                        cfg.area.Max.Y
                        cfg.radius
                    )
                |> String.concat ","

            String.concat "" [
                "if (typeof initTouchSticks === 'function') {"
                $"    initTouchSticks('__ID__',[{configs}]);"
                "} else {"
                "    console.warn('Cannot initialize TouchSticks (initTouchSticks undefined).');"
                "}"
            ]

        require dependencies (
            onBoot boot el
        )

    type TouchStickState =
        {
            distance : float
            angle : float
        }

    let scaleStick (exp : bool) (scale : float) (s : TouchStickState) =
        if exp then
            { s with 
                distance = s.distance ** 1.75 * scale
            }
        else
            { s with 
                distance = s.distance * (scale * 1.25)
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