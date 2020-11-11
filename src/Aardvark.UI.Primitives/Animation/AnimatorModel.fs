namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open Adaptify

[<ModelType>]
type Animator<'Model> =
    {
        Animations : HashMap<Symbol, IAnimation<'Model>>
        TickRate : int
    }

[<RequireQualifiedAccess>]
type AnimatorMessage<'Model> =
    | Tick
    | Set of name: Symbol * animation: IAnimation<'Model> * start: bool
    | Remove of name: Symbol
    | Stop of name: Symbol * remove: bool
    | Start of name: Symbol
    | Pause of name: Symbol
    | Resume of name: Symbol
    | SetTickRate of rate: int