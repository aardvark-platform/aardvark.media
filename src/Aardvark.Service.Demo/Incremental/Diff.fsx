#r @"..\..\..\bin\Debug\Aardvark.Base.dll"
#r @"..\..\..\bin\Debug\Aardvark.Base.Essentials.dll"
#r @"..\..\..\bin\Debug\Aardvark.Base.TypeProviders.dll"
#r @"..\..\..\bin\Debug\Aardvark.Base.FSharp.dll"
#r @"..\..\..\bin\Debug\Aardvark.Base.Incremental.dll"
#r @"..\..\..\bin\Debug\FSharp.Compiler.Service.dll"
#r @"..\..\..\bin\Debug\FSharp.Compiler.Service.MSBuild.v12.dll"
#r @"..\..\..\bin\Debug\FSharp.Compiler.Service.ProjectCracker.dll"

open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental
open Microsoft.FSharp.Compiler.SourceCodeServices

do Environment.CurrentDirectory <- Path.Combine(__SOURCE_DIRECTORY__, "..")

let checker = FSharpChecker.Create()


let rec findDomainTypes (e : list<FSharpEntity>) : list<FSharpEntity> =
    

    e |> List.collect (fun e ->
        let rest = e.NestedEntities |> Seq.toList |> findDomainTypes


        if e.IsFSharpRecord then
            e :: rest
        else
            rest
    )

let checkFile (projFile : string) (file : string) =
    async {
        do! Async.SwitchToThreadPool()
        let source = File.ReadAllText file
        let options = ProjectCracker.GetProjectOptionsFromProjectFile(projFile)

        let! (res, answer) = checker.ParseAndCheckFileInProject(file, 0, source, options)
        match answer with
            | FSharpCheckFileAnswer.Succeeded res -> 
                let signature = res.PartialAssemblySignature

                return findDomainTypes (Seq.toList signature.Entities)
            | _ -> return failwith "could not check file in project"
    }

let a = checkFile @"Aardvark.Service.Demo.fsproj" @"Ui.fs" |> Async.RunSynchronously
printfn "%A" a