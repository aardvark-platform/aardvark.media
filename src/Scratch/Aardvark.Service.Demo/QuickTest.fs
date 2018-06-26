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
        | Select p ->  { m with selected =  p }
        //| ChangeValue s -> { m with newValue = s }
        //| AddValue s -> { m with values = m.values |> PList.append s }
            
            
let personToText (p:IMod<option<Person>>) : IMod<string> =
    p |> Mod.map(
        fun x ->
            match x with
                | Some s -> sprintf "person selected: %s %s" s.firstName s.secondName
                | None -> "no person selected")    

let view (m : MQuickTestModel) =
    body[][
        div [] [
         //   Html.SemUi.textBox m.newValue. ChangeValue
          //  button [onClick(fun _ -> AddValue (m.newValue |> Mod.force))] [text "add"]
          //  br[]
            Html.SemUi.dropDown' m.values m.selected (fun a -> Select a) (fun a -> a.secondName)
            br[]
            Incremental.text (m.selected |> Mod.map(fun x -> sprintf "person selected: %s %s" x.firstName x.secondName ))
        ]
    ]

let persons = [

    { firstName = "Harry"; secondName = "Stonelicker" }
    { firstName = "Mitschgi"; secondName = "Blackler" }
    { firstName = "Atti"; secondName = "Szaborider" }

]

let app =
    {
        unpersist = Unpersist.instance
        threads = fun _ -> ThreadPool.Empty
        initial = 
            { 
                newValue = None
                values = persons|> PList.ofList
                selected= { firstName = "Atti"; secondName = "Szaborider" }
                //dropdown = dropDownINit                    
            }
        update = update
        view = view
    }

let start() = App.start app
    


