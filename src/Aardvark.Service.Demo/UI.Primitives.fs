namespace Viewer

open System
open Suave

open Aardvark.Base
open Aardvark.Base.Incremental
open UiPrimitives

//module Helpers = 
    //let onWheel (f : Aardvark.Base.V2d -> 'msg) =
    //    let clientClick = 
    //        """function(ev) { 
    //            return { X : ev.deltaX.toFixed(), Y : ev.deltaY.toFixed() };
    //        }"""
    //    let serverClick (str : string) : Aardvark.Base.V2d = 
    //        Pickler.json.UnPickleOfString str / Aardvark.Base.V2d(-100.0,-100.0) // up is down in mouse wheel events

    //    ClientEvent("onWheel", clientClick, serverClick >> f)

type NumericInputType = Slider | InputBox

module List =
    /// The intersperse function takes an element and a list and
    /// 'intersperses' that element between the elements of the list.
    let intersperse sep ls =
        List.foldBack (fun x -> function
            | [] -> [x]
            | xs -> x::sep::xs) ls []

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Numeric = 
    open Aardvark.Base
    open Aardvark.UI

    type Action = 
        | Set of string
        | NewTime    

    let update (model : NumericBox) (action : Action) =
        match action with
            | Set s     ->
                let parsed = 0.0
                match Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                    | (true,v) -> { model with value = Fun.Clamp(v, model.min, model.max)  }
                    | _ -> 
                        printfn "validation failed: %s" s
                        model  
             | _ -> model

    let numericField set (model:MNumericBox) inputType =         
       // let formatString = model.format.
        let num = model.value

        let t = 
            match inputType with
                | Slider -> "range" | InputBox -> "number"
       
        let format (format : string) (value : float) =
            String.Format(Globalization.CultureInfo.InvariantCulture, format, value) |> AttributeValue.String |> Some

        let attributes =
            AttributeMap.ofListCond [
                //style "textAlign:right"
                "style", Mod.constant (AttributeValue.String "text-align:right" |> Some)
                "type", Mod.constant (AttributeValue.String t |> Some)
                "step", Mod.map (fun step -> sprintf "%f" step |> AttributeValue.String |> Some) model.step
                "min", Mod.map (fun step -> sprintf "%f" step |> AttributeValue.String |> Some) model.min
                "max", Mod.map (fun step -> sprintf "%f" step |> AttributeValue.String |> Some) model.max
                always (onChange (unbox >> set))
                "value", Mod.map2 format model.format model.value
            ]

            
//        input [
//            style "textAlign:right"
//            attribute "value" (String.Format(Globalization.CultureInfo.InvariantCulture,
//                                   num.format,
//                                   num.value)) 
//        ] 
        Incremental.input attributes

    let view' (inputTypes : list<NumericInputType>) (model : MNumericBox) : DomNode<Action> =
        inputTypes 
            |> List.map (numericField Set model) 
            |> List.intersperse (text " ") 
            |> div []

    let view (model : MNumericBox) =
        view' [InputBox]

    let init = {
        value   = 3.0
        min     = 0.0
        max     = 15.0
        step    = 1.5
        format  = "{0:0.00}"
    }

    let rec timerThread() =
        proclist {
            do! Proc.Sleep 10
            yield NewTime
            yield! timerThread()
        }

    let app' t =
        {
            unpersist = Unpersist.instance
            threads = fun _ -> ThreadPool.create() |> ThreadPool.add "timer" (timerThread())
            initial = init
            update = update
            view = view' t
        }

    let app = app' [NumericInputType.InputBox; NumericInputType.InputBox; NumericInputType.Slider]

    let start () =
        App.start app