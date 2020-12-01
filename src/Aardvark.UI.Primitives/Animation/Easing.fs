namespace Aardvark.UI.Anewmation

open Aardvark.Base

[<RequireQualifiedAccess>]
type EasingFunction =
    | Quadratic
    | Cubic
    | Sine

[<RequireQualifiedAccess>]
type Easing =
    | None
    | In of EasingFunction
    | Out of EasingFunction
    | InOut of EasingFunction
    | OutIn of EasingFunction

[<AutoOpen>]
module AnimationEasingExtensions =

    module Animation =

        [<AutoOpen>]
        module private EasingFunctions =

            let inline inOut fIn fOut t =
                if t < 0.5 then
                    0.5 * fIn (t * 2.0)
                else
                    0.5 + 0.5 * fOut (2.0 * t - 1.0)

            let inline outIn fIn fOut t =
                inOut fOut fIn t

            // Quadratic
            let inline quadIn t    = t * t
            let inline quadOut t   = -t * (t - 2.0)
            let inline quadInOut t = inOut quadIn quadOut t
            let inline quadOutIn t = outIn quadIn quadOut t

            // Cubic
            let inline cubicIn t    = t * t * t
            let inline cubicOut t   = let t = t - 1.0 in t * t * t + 1.0
            let inline cubicInOut t = inOut cubicIn cubicOut t
            let inline cubicOutIn t = outIn cubicIn cubicOut t

            // Sine
            let inline sineIn t    = if t = 1.0 then 1.0 else -cos(t * Constant.PiHalf) + 1.0;
            let inline sineOut t   = if t = 1.0 then 1.0 else sin(t * Constant.PiHalf)
            let inline sineInOut t = if t = 1.0 then 1.0 else -0.5 * (cos(t * Constant.Pi) - 1.0);
            let inline sineOutIn t = outIn sineIn sineOut t

            let toInFunction = function
                | EasingFunction.Quadratic   -> quadIn
                | EasingFunction.Cubic       -> cubicIn
                | EasingFunction.Sine        -> sineIn

            let toOutFunction = function
                | EasingFunction.Quadratic   -> quadOut
                | EasingFunction.Cubic       -> cubicOut
                | EasingFunction.Sine        -> sineOut

            let toInOutFunction = function
                | EasingFunction.Quadratic   -> quadInOut
                | EasingFunction.Cubic       -> cubicInOut
                | EasingFunction.Sine        -> sineInOut

            let toOutInFunction = function
                | EasingFunction.Quadratic   -> quadOutIn
                | EasingFunction.Cubic       -> cubicOutIn
                | EasingFunction.Sine        -> sineOutIn

            let toFunction = function
                | Easing.None -> id
                | Easing.In f -> toInFunction f
                | Easing.Out f -> toOutFunction f
                | Easing.InOut f -> toInOutFunction f
                | Easing.OutIn f -> toOutInFunction f

        /// Applies the given easing function to the animation.
        let easeCustom (compose : bool) (easing : float -> float) (animation : IAnimation<'Model, 'Value>) =
            animation.Ease(easing, compose)

        /// Applies the given easing function to the animation.
        let ease' (compose : bool) (easing : Easing) (animation : IAnimation<'Model, 'Value>) =
            animation |> easeCustom compose (toFunction easing)

        /// Applies the given easing function to the animation.
        let ease (easing : Easing) (animation : IAnimation<'Model, 'Value>) =
            animation |> ease' true easing

