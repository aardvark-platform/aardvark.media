namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

open RenderingParametersModel

type NavigationMode =
    | FreeFly = 0
    | ArcBall = 1

[<DomainType>]
type NavigationParameters = {
    navigationMode : NavigationMode    
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NavigationParameters =

    let initial =
        {
            navigationMode = NavigationMode.FreeFly
        }

[<DomainType>]
type NavigationModeDemoModel = {
    camera     : CameraControllerState
    rendering  : RenderingParameters
    navigation : NavigationParameters
    navsensitivity : NumericInput
    zoomFactor : NumericInput
    panFactor  : NumericInput
}