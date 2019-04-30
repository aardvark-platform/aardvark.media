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
               (id : string) =
    if Directory.Exists id then failwith "[Service] output dir already exists"
    Directory.CreateDirectory id |> ignore
    let stdoutFile = File.CreateText(Path.Combine(id,"stdout.txt"))
    let stderrFile = File.CreateText(Path.Combine(id,"stderr.txt"))
    let parentProcessId = Process.GetCurrentProcess().Id
    let psi = ProcessStartInfo()
    psi.Arguments <- sprintf "\"%s\" %d %d" assembly port parentProcessId
    psi.CreateNoWindow <- true
    psi.FileName <- "C:\Program Files\dotnet\dotnet.exe"
    psi.WorkingDirectory <- workingDirectory
    psi.RedirectStandardError <- true
    psi.RedirectStandardOutput <- true
    psi.UseShellExecute <- false
    let p = new Process()
    p.StartInfo <- psi
    p.OutputDataReceived.Add(fun dre -> stdoutFile.WriteLine dre.Data; stdoutFile.Flush(); dre.Data |> stdout)
    p.ErrorDataReceived.Add(fun dre -> stderrFile.WriteLine dre.Data; stderrFile.Flush(); dre.Data |> stderr)
    p.Exited.Add(fun _ -> died ())
    p.EnableRaisingEvents <- true
    let ok = p.Start()
    p.BeginOutputReadLine()
    p.BeginErrorReadLine()
   
    if not ok then failwith "could not start process."
    p

let test () =
    runService @"C:\Users\hs\Desktop\platform\aardvark.media\bin\Debug\netcoreapp2.0\17 - Serviced.dll"
               @"C:\Users\hs\Desktop\platform\aardvark.media\bin\Debug\netcoreapp2.0"
               4322
               (fun s -> printfn "%A" s)
               (fun s -> printfn "%A" s)
               (fun s -> printfn "died")