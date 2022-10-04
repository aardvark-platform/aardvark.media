namespace Test.Dashboard

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type UserMode =
| NotSelected = 0
| Developer  = 1
| Expert = 2
| Simple = 3

type ViewId = string

type DashboardAction = 
    | SetUserMode      of UserMode
    | SetClientSession of string
    | IncMessage       of (ViewId * Tmp.Inc.Message)
    | BindComponent    of string
    | UnbindComponent  of string

[<ModelType>]
type Dashboard = 
    {
        incApps       : HashMap<ViewId, Tmp.Inc.Model>
        inServerMode  : bool /// if true use server mode without file dialogs etc
        userMode      : UserMode
        clientId      : string
        background    : C4b
        threads       : ThreadPool<DashboardAction>
        debugCount    : int
    } 
    static member Threads_ =
        (
            (fun (self : Dashboard) -> self.threads), 
            (fun (value : ThreadPool<DashboardAction>) (self : Dashboard) -> { self with threads = value })
        )
