namespace Aardvark.UI

open System
open Suave

open Aardvark.Base
open Aardvark.Base.Incremental

module UI = 
    open Aardvark.Base
    open Aardvark.UI

    let onWheel (f : Aardvark.Base.V2d -> 'msg) =
        let serverClick (args : list<string>) : Aardvark.Base.V2d = 
            let delta = List.head args |> Pickler.unpickleOfJson
            delta  / Aardvark.Base.V2d(-100.0,-100.0) // up is down in mouse wheel events

        onEvent "onwheel" ["{ X: event.deltaX.toFixed(), Y: event.deltaY.toFixed()  }"] (serverClick >> f)

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
        | Step of float    

    let update (model : NumericBox) (action : Action) =
        match action with
            | Set s     ->
                let parsed = 0.0
                match Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                    | (true,v) -> { model with value = Fun.Clamp(v, model.min, model.max)  }
                    | _ -> 
                        printfn "validation failed: %s" s
                        model  
             | Step v -> { model with value = model.value + v }

    let formatNumber (format : string) (value : float) =
        String.Format(Globalization.CultureInfo.InvariantCulture, format, value)

    let numericField (set : string -> Action) (model : MNumericBox) inputType =         
      
        let attributes = 
            amap {
                yield style "text-align:right"
                match inputType with
                    | Slider ->   
                        yield attribute "type" "range"
                        yield onInput set   // continous updates for slider
                    | InputBox -> 
                        yield attribute "type" "number"
                        yield onChange set  // batch updates for input box (to let user type)

                let! step = model.step
                yield UI.onWheel (fun d ->  d.Y * step |> Step)

                let! min = model.min
                let! max = model.max
                yield attribute "step" (sprintf "%f" step)
                yield attribute "min"  (sprintf "%f" min)
                yield attribute "max"  (sprintf "%f" max)

                let! value = model.value

                let! format = model.format
                yield attribute "value" (formatNumber format value)
            } 

        Incremental.input (AttributeMap.ofAMap attributes)

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

    let app' inputTypes =
        {
            unpersist = Unpersist.instance
            threads = fun _ -> ThreadPool.create()
            initial = init
            update = update
            view = view' inputTypes
        }

    let app = app' [NumericInputType.InputBox; NumericInputType.InputBox; NumericInputType.Slider]

    let start () =
        App.start app