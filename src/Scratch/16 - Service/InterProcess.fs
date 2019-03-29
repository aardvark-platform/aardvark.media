#if INTERACTIVE
#else
module InterProcess
#endif

open System
open System.IO
open System.Diagnostics
open System.Threading


let runService (assembly : string)
               (workingDirectory : string)
               (port : int)
               (stdout : string -> unit)
               (stderr : string -> unit)
               (died : unit -> unit)
               (id : int) =
    let psi = ProcessStartInfo()
    psi.Arguments <- sprintf "\"%s\" %d" assembly port
    psi.CreateNoWindow <- true
    psi.FileName <- "C:\Program Files\dotnet\dotnet.exe"
    psi.WorkingDirectory <- workingDirectory
    psi.RedirectStandardError <- true
    psi.RedirectStandardOutput <- true
    psi.UseShellExecute <- false
    let p = new Process()
    p.StartInfo <- psi
    //p.OutputDataReceived.Add(fun dre -> dre.Data |> stdout)
    p.ErrorDataReceived.Add(fun dre -> dre.Data |> stderr)
    p.Exited.Add(fun _ -> died ())
    p.EnableRaisingEvents <- true
   
    let ok = p.Start()
    let t = 
        new Thread(ThreadStart(fun _ -> 
            while true do 
                let l = p.StandardOutput.ReadLine()
                stdout l
            )
        )
    printfn "ok?: %A" ok
    t.Start()
    p

let test () =
    runService @"C:\Users\hs\Desktop\platform\aardvark.media\bin\Debug\netcoreapp2.0\17 - Serviced.dll"
               @"C:\Users\hs\Desktop\platform\aardvark.media\bin\Debug\netcoreapp2.0"
               4322
               (fun s -> printfn "%A" s)
               (fun s -> printfn "%A" s)
               (fun s -> printfn "died")
               0