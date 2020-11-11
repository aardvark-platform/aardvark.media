namespace Aardvark.UI.Anewmation

open System

type private SpaceFunction<'Value>(f : Func<float,'Value>) =

    static let defaultFunction = SpaceFunction(fun _ -> Unchecked.defaultof<'Value>)

    /// Evaluates the space function for the given position parameter within [0, 1].
    member x.Invoke(position : float) = f.Invoke position

    /// Default space function that returns a default value.
    static member Default = defaultFunction

    interface ISpaceFunction<'Value> with
        member x.Invoke(position) = x.Invoke(position)

[<AutoOpen>]
module AnimationSpaceExtensions =

    module Animation =

        /// Sets the space function of the given animation.
        let spaceFunction (f : float -> 'Value) (animation : IAnimation<'Model, 'Value>) =
            animation.UpdateSpaceFunction(fun _ ->
                SpaceFunction(Func<_,_> f) :> ISpaceFunction<'Value>
            )