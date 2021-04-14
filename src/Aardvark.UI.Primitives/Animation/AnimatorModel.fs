namespace Aardvark.UI.Anewmation

open Aardvark.Base
open System.Collections.Generic

type Animation<'Model> =
    {
        Name : Symbol
        mutable Animation : IAnimation<'Model>
    }

type Animator<'Model> =
    {
        Animations : List<Animation<'Model>>
        TickRate : int
        mutable TickCount : int
    }

[<RequireQualifiedAccess>]
type AnimatorMessage<'Model> =
    | Tick
    | Set         of name: Symbol * animation: IAnimation<'Model>
    | Remove      of name: Symbol
    | Stop        of name: Symbol
    | Start       of name: Symbol * startFrom: float * restart: bool
    | Pause       of name: Symbol
    | Resume      of name: Symbol