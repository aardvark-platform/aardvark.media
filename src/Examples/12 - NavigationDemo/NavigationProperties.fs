module NavigationProperties

open Aardvark.UI
open Aardvark.UI.Primitives

open Model
        
type Action =
    | SetNavigationMode of NavigationMode

let update (model : NavigationParameters) (act : Action) =
    match act with
        | SetNavigationMode mode ->
            { model with navigationMode = mode }

let view (model : MNavigationParameters) =        
    require Html.semui (
        Html.table [                            
            Html.row "Mode:" [Html.SemUi.dropDown model.navigationMode SetNavigationMode]
        ]
    )