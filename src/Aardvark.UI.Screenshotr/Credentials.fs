namespace Aardvark.UI.Screenshotr

open Aardvark.Base
open System
open System.IO
open System.Text.Json

type CredentialsDto = {
    url : string
    key : string
}

type Credentials =
    | Missing
    | NotAuthorized of CredentialsDto
    | Valid of CredentialsDto

module Credentials = 

    let credentialFileName =
    
        let credentialDirPath = 
            Path.combine 
                [
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,Environment.SpecialFolderOption.Create)
                    "Screenshotr"
                ]

        if not (Directory.Exists(credentialDirPath)) then
                Directory.CreateDirectory(credentialDirPath) |> ignore

        Path.combine [ credentialDirPath; "cache.json" ] 

    let save (m : CredentialsDto) = 
        m |> JsonSerializer.Serialize |> File.writeAllText credentialFileName
        m

    let load () = 

        if File.Exists(credentialFileName) then

            printfn "[Credentials.load] file exist (%s)" credentialFileName
            try
                let result =
                    credentialFileName
                    |> File.ReadAllText
                    |> JsonSerializer.Deserialize<CredentialsDto>
                    |> Valid
                result

            with e1 ->
                printfn "[Credentials.load] failed to load credentials from %s\n%A" credentialFileName e1
                
                try
                    File.Delete credentialFileName
                    printfn "[Credentials.load] deleted %s\n%A" credentialFileName e1
                with e2 ->
                    ()

                Missing

        else

            printfn "[Credentials.load] file not found (%s)" credentialFileName
            Missing

