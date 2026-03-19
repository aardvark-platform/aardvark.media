namespace Aardvark.Service.Suave

open Aardvark.Base
open System.Threading

// https://github.com/aardvark-platform/aardvark.media/issues/19
module ThreadPoolAdjustment =
    let mutable shouldAdjust = true

    let adjust () =
        if shouldAdjust then
            let mutable maxThreads,maxIOThreads = 0,0
            ThreadPool.GetMaxThreads(&maxThreads, &maxIOThreads)

            let mutable minThreads, minIOThreads = 0,0
            ThreadPool.GetMinThreads(&minThreads, &minIOThreads)

            if minThreads < 12 || minIOThreads < 12 then
                Log.warn "[aardvark.media] currently ThreadPool.MinThreads is (%d,%d)" minThreads minIOThreads
                let minThreads   = max 12 minThreads
                let minIOThreads = max 12 minIOThreads
                Log.warn "[aardvark.media] unfortunately, currently we need to adjust this to at least (12,12) due to an open issue https://github.com/aardvark-platform/aardvark.media/issues/19"
                if not <| ThreadPool.SetMinThreads(minThreads, minIOThreads) then Log.warn "could not set min threads"
                if maxThreads < 12 || maxIOThreads < 12 then
                    Log.warn "[aardvark.media] detected less than 12 threadpool threads: (%d,%d). Be aware that this will result in severe stutters... Consider switching back to the default (65537,1000)." maxThreads maxIOThreads