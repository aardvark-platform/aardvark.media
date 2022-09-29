namespace Test

open Adaptify
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Test.Dashboard

type ServerAction =
    | DashboardMessage of (string * DashboardAction)
    //| IncreaseDashboardDebug of string
    //| TestAction
    //| NewClientSession of string
    //| CreateNewClient of string
    //| RemoveThread of string

[<ModelType>]
type ServerApp = {
    testCount   : int

}
