namespace Scratch

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils

open System
open System.Net

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket


open System.Collections.Concurrent
open System.Threading.Tasks

open Aardvark.Base


module Communication =
   
    let ws (bc : BlockingCollection<string*TaskCompletionSource<string>>) (webSocket : WebSocket) (context: HttpContext) =
        socket {
            let mutable loop = true
            while loop do
                let (request,answer) = bc.Take()
                let bytes = System.Text.Encoding.UTF8.GetBytes request
                do! webSocket.send Opcode.Text bytes true 
                let! result = webSocket.read ()
                match result with
                    | (Opcode.Text,data,true) -> 
                        let str = System.Text.Encoding.UTF8.GetString data
                        answer.SetResult(str)
                        Log.line "Received msg from client %A" str
                    | (Close, _, _) -> loop <- false
                    | _ -> failwith "strange. what is this"
         }



    let app  (bc : BlockingCollection<string*TaskCompletionSource<string>>) : WebPart = 
      choose [
        path "/ws" >=> handShake (ws bc)
      //  GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
        NOT_FOUND "Found no handlers." ]

    open System.Threading
    open System.Threading.Tasks

    type Communication = { send : string -> Task<string> }
    

    let start () =

        let bc = new BlockingCollection<_>()

        let cts = new CancellationTokenSource()
        let config = { 
            defaultConfig with bindings = [ HttpBinding.mk HTTP IPAddress.Loopback (Port.Parse "31415")]; 
                               listenTimeout = TimeSpan.MaxValue
        }
        
        let listening,server = startWebServerAsync config (app bc)
        
        let t = Async.StartAsTask(server, cancellationToken = cts.Token)
        listening |> Async.RunSynchronously |> printfn "[webschmeb] start stats: %A"

        let send str =
            let tcs = TaskCompletionSource()
            bc.Add((str,tcs))
            tcs.Task

        { send = send }
