namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive

type Animator<'Model> =
    {
        Animations : HashMap<Symbol, IAnimationInstance<'Model>>
        TickRate : int
        mutable TickCount : int
    }

[<RequireQualifiedAccess>]
type AnimatorMessage<'Model> =
    /// Performs an animation tick.
    | Tick

    /// Creates an instance of the given animation with the given name, and performs the given action.
    /// Replaces any existing instance with the given name.
    | Create  of name: Symbol * animation: IAnimation<'Model> * action: (GlobalTime -> IAnimationInstance<'Model> -> unit)

    /// Performs the action for the animation instance with given name if it exists.
    | Perform of name: Symbol * action: (GlobalTime -> IAnimationInstance<'Model> -> unit)

    /// Removes the animation instance with the given name if it exists.
    | Remove  of name: Symbol

    /// Performs the action for every animation instance.
    | Iterate of action: (GlobalTime -> IAnimationInstance<'Model> -> unit)

    /// Removes every animation instance for which the given predicate returns false.
    | Filter  of predicate: (IAnimationInstance<'Model> -> bool)