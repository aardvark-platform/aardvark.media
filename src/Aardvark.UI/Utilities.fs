namespace Aardvark.UI


open System
open System.Collections.Generic


module private FakeRx =
    type SubjectDisposeable<'t>(store : Dictionary<IObserver<'t>, int>, self : IObserver<'t>) =
        interface IDisposable with
            member x.Dispose() =
                if isNull self then ()
                else 
                    lock store (fun _ -> 
                        match store.TryGetValue(self) with
                        | (true,cnt) -> 
                            let newCnt = cnt - 1
                            if newCnt > 0 then store.[self] <- newCnt
                            else store.Remove(self) |> ignore
                        | _ -> ()
                    )

    type Subject<'t>() =
        let observers = Dictionary<IObserver<'t>, int>()

        member x.OnCompleted() = 
            let obs = lock observers (fun _ -> observers.Keys |> Seq.toArray)
            for o in obs do o.OnCompleted()
        member x.OnNext(v) =
            let obs = lock observers (fun _ -> observers.Keys |> Seq.toArray)
            for o in obs do o.OnNext(v)
        member x.OnError(v) =
            let obs = lock observers (fun _ -> observers.Keys |> Seq.toArray)
            for o in obs do o.OnError(v)

        member x.Subscribe(o : IObserver<'t>) =
            lock observers (fun _ -> 
                match observers.TryGetValue(o) with
                | (false,_) -> observers.[o] <- 1
                | (true,cnt) -> observers.[o] <- cnt + 1
                new SubjectDisposeable<'t>(observers, o) :> IDisposable
            )

        interface IObservable<'t> with
            member x.Subscribe(o : IObserver<'t>) = x.Subscribe(o)
        interface IObserver<'t> with
            member x.OnCompleted() = x.OnCompleted()
            member x.OnError(v) = x.OnError(v)
            member x.OnNext(v) = x.OnNext(v)


        member x.Dispose() =
            let obs = lock observers (fun _ -> let r = observers.Keys |> Seq.toArray; in observers.Clear(); r)
            for o in obs do o.OnCompleted()

        interface IDisposable with
            member x.Dispose() = x.Dispose()
            
            
