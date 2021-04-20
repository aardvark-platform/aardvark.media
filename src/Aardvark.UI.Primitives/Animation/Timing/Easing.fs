namespace Aardvark.UI.Anewmation

open Aardvark.Base

[<RequireQualifiedAccess>]
type EasingFunction =
    | Quadratic
    | Cubic
    | Sine
    | Bounce of amount: float
    | Overshoot of amount: float
    | Elastic of amplitude: float * period: float

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

            // Bounce
            let bounceImpl c a t =
                if t = 1.0 then c
                elif t < 4.0 / 11.0 then
                    c * 7.5625 * t * t;
                elif t < 8.0 / 11.0 then
                    let t = t - 6.0 / 11.0 in -a * (1.0 - (7.5625 * t * t + 0.75)) + c
                elif t < 10.0 / 11.0 then
                    let t = t - 9.0 / 11.0 in -a * (1.0 - (7.5625 * t * t + 0.9375)) + c;
                else
                    let t = t - 21.0 / 22.0 in -a * (1.0 - (7.5625 * t * t + 0.984375)) + c;

            let inline bounceIn a t = 1.0 - bounceImpl 1.0 a (1.0 - t)
            let inline bounceOut a t = bounceImpl 1.0 a t

            let inline bounceInOut a t =
                if (t < 0.5) then (bounceIn a (2.0 * t)) * 0.5;
                else if (t = 1.0) then 1.0 else (bounceOut a (2.0 * t - 1.0)) * 0.5 + 0.5;

            let inline bounceOutIn a t =
                if t < 0.5 then bounceImpl 0.5 a (2.0 * t)
                else 1.0 - bounceImpl 0.5 a (2.0 - 2.0 * t);

            // Overshoot
            let inline overshootIn s t = t * t * ((s + 1.0) * t - s)
            let inline overshootOut s t = let t = t - 1.0 in t * t * ((s + 1.0) * t + s) + 1.0
            let inline overshootInOut s t = let s = s * 1.525 in inOut (overshootIn s) (overshootOut s) t
            let inline overshootOutIn s t = outIn (overshootIn s) (overshootOut s) t

            // Elastic
            let inline elasticParams upper amplitude period =
                if amplitude < upper then
                    struct (upper, period / 4.0)
                else
                    struct (amplitude, (period / Constant.PiTimesTwo) * asin (upper / amplitude))

            let inline elasticPower sign period s t =
                (pow 2.0 (sign * 10.0 * t)) * sin ((t - s) * Constant.PiTimesTwo / period)

            let elasticInImpl lower upper amplitude period t =
                if t = 0.0 then lower
                elif t = 1.0 then lower + upper
                else
                    let struct (a, s) = elasticParams upper amplitude period
                    lower - a * elasticPower 1.0 period s (t - 1.0)

            let elasticOutImpl upper amplitude period t =
                if t = 0.0 then 0.0
                elif t = 1.0 then upper
                else
                    let struct (a, s) = elasticParams upper amplitude period
                    upper + a * elasticPower -1.0 period s t

            let inline elasticIn amplitude period t =
                t |> elasticInImpl 0.0 1.0 amplitude period

            let inline elasticOut amplitude period t =
                t |> elasticOutImpl 1.0 amplitude period

            let elasticInOut amplitude period t =
                if t = 0.0 then 0.0
                elif t = 1.0 then 1.0
                else
                    let t = t * 2.0
                    let struct (a, s) = elasticParams 1.0 amplitude period

                    if t < 1.0 then -0.5 * a * elasticPower 1.0 period s (t - 1.0)
                    else 1.0 + 0.5 * a * elasticPower -1.0 period s (t - 1.0)

            let inline elasticOutIn amplitude period t =
                if t < 0.5 then (2.0 * t) |> elasticOutImpl 0.5 amplitude period
                else (2.0 * t - 1.0) |> elasticInImpl 0.5 0.5 amplitude period


            let toInFunction = function
                | EasingFunction.Quadratic      -> quadIn
                | EasingFunction.Cubic          -> cubicIn
                | EasingFunction.Sine           -> sineIn
                | EasingFunction.Bounce a       -> bounceIn a
                | EasingFunction.Overshoot s    -> overshootIn s
                | EasingFunction.Elastic(a, p)  -> elasticIn a p

            let toOutFunction = function
                | EasingFunction.Quadratic      -> quadOut
                | EasingFunction.Cubic          -> cubicOut
                | EasingFunction.Sine           -> sineOut
                | EasingFunction.Bounce a       -> bounceOut a
                | EasingFunction.Overshoot s    -> overshootOut s
                | EasingFunction.Elastic(a, p)  -> elasticOut a p

            let toInOutFunction = function
                | EasingFunction.Quadratic      -> quadInOut
                | EasingFunction.Cubic          -> cubicInOut
                | EasingFunction.Sine           -> sineInOut
                | EasingFunction.Bounce a       -> bounceInOut a
                | EasingFunction.Overshoot s    -> overshootInOut s
                | EasingFunction.Elastic(a, p)  -> elasticInOut a p

            let toOutInFunction = function
                | EasingFunction.Quadratic      -> quadOutIn
                | EasingFunction.Cubic          -> cubicOutIn
                | EasingFunction.Sine           -> sineOutIn
                | EasingFunction.Bounce a       -> bounceOutIn a
                | EasingFunction.Overshoot s    -> overshootOutIn s
                | EasingFunction.Elastic(a, p)  -> elasticOutIn a p

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

