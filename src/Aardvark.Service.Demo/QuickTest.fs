module QuickTestApp

open QuickTest

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

type Action = 
    | SetValue of string
    | AddValue of string
    | ChangeValue of string

let update (m : DropDownModel) (a : Action) =
    match a with
        | SetValue s ->    { m with selected = s }
        | ChangeValue s -> { m with newValue = s }
        | AddValue s ->    { m with values = m.values |> PList.append s }

let view (m : MDropDownModel) =
    div [] [
        Html.SemUi.textBox m.newValue ChangeValue
        button [onClick(fun _ -> AddValue (m.newValue |> Mod.force))] [text "add"]
        br[]
        Html.SemUi.dropDown' m.values m.selected SetValue
    ]

let app =
    {
        unpersist = Unpersist.instance
        threads = fun _ -> ThreadPool.Empty
        initial = { values = ["Horst";"Hinz"; "Kunz"] |> PList.ofList; selected = "Kunz"; newValue = "" }
        update = update
        view = view
    }

let start() = App.start app
    


