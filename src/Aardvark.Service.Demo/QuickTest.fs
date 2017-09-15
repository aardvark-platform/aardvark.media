module QuickTestApp

open QuickTest

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

type Action = 
    | Select      of Person
    //| AddValue    of Person
    //| ChangeValue of Person

let update (m : QuickTestModel) (a : Action) =
    match a with
        | Select p ->  { m with selected = p.secondName }
        //| ChangeValue s -> { m with newValue = s }
        //| AddValue s -> { m with values = m.values |> PList.append s }
            
            

let view (m : MQuickTestModel) =
    body[][
        div [] [
         //   Html.SemUi.textBox m.newValue. ChangeValue
          //  button [onClick(fun _ -> AddValue (m.newValue |> Mod.force))] [text "add"]
          //  br[]
            Html.SemUi.dropDown' m.values m.selected (fun a -> Select a) (fun a -> a.secondName)
        ]
    ]

let dropDownINit = 
    { 
        values = (["Horst";"Hinz"; "Kunz"] |> List.mapi(fun i v -> (i,v)) |> HMap.ofList)
        selected = 0 
    }

let app =
    {
        unpersist = Unpersist.instance
        threads = fun _ -> ThreadPool.Empty
        initial = 
            { 
                newValue = { firstName = ""; secondName = "" };
                values = [{ firstName = "horst"; secondName = "hinioadfs" }; { firstName = "adfadsf"; secondName = "adfdasdf" }]|> PList.ofList
                selected= "Horst"
                //dropdown = dropDownINit                    
            }
        update = update
        view = view
    }

let start() = App.start app
    


