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
                    | (true,v) -> { model with value = { model.value with value = Fun.Clamp(v, model.value.min, model.value.max) } }
                    | _ -> 
                        printfn "validation failed: %s" s
                        model  
             | _ -> model

    let numericField set (model:MNumericBox) inputType =         
       // let formatString = model.format.
        let num = model.value |> Mod.force
            
        input [
            style "textAlign:right"
            attribute "value" (String.Format(Globalization.CultureInfo.InvariantCulture,
                                   num.format,
                                   num.value)) // custom number formatting
            attribute "type" (match inputType with | Slider -> "range"; | InputBox -> "number") 
            attribute "step" (sprintf "%f" num.step)
            attribute "min" (sprintf "%f" num.min)
            attribute "max" (sprintf "%f" num.max)
          //  onWheel (fun d -> model.value + (d.Y * model.step) |> string |> set)
            onChange (unbox >> set)
        ] 

    let view' (inputTypes : list<NumericInputType>) (model : MNumericBox) : DomNode<Action> =
        inputTypes 
            |> List.map (numericField Set model) 
            |> List.intersperse (text " ") 
            |> div []

    let view (model : MNumericBox) =
        view' [InputBox]

    let initial = {
        value   = 3.0
        min     = 0.0
        max     = 15.0
        step    = 1.5
        format  = "{0:0.00}"
    }

    let init = {
        value = initial
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