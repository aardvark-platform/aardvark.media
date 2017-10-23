namespace LayoutingModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open LayoutingModel

[<AutoOpen>]
module Mutable =

    
    
    type MTab(__initial : LayoutingModel.Tab) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<LayoutingModel.Tab> = Aardvark.Base.Incremental.EqModRef<LayoutingModel.Tab>(__initial) :> Aardvark.Base.Incremental.IModRef<LayoutingModel.Tab>
        let _name = ResetMod.Create(__initial.name)
        let _url = ResetMod.Create(__initial.url)
        
        member x.name = _name :> IMod<_>
        member x.url = _url :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : LayoutingModel.Tab) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_name,v.name)
                ResetMod.Update(_url,v.url)
                
        
        static member Create(__initial : LayoutingModel.Tab) : MTab = MTab(__initial)
        static member Update(m : MTab, v : LayoutingModel.Tab) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<LayoutingModel.Tab> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Tab =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let name =
                { new Lens<LayoutingModel.Tab, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.name
                    override x.Set(r,v) = { r with name = v }
                    override x.Update(r,f) = { r with name = f r.name }
                }
            let url =
                { new Lens<LayoutingModel.Tab, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.url
                    override x.Set(r,v) = { r with url = v }
                    override x.Update(r,f) = { r with url = f r.url }
                }
    [<AbstractClass; System.Runtime.CompilerServices.Extension; StructuredFormatDisplay("{AsString}")>]
    type MTree() =
        abstract member TryUpdate : LayoutingModel.Tree -> bool
        abstract member AsString : string
        
        static member private CreateValue(__model : LayoutingModel.Tree) = 
            match __model with
                | Vertical(item1, item2) -> MVertical(__model, item1, item2) :> MTree
                | Horizontal(item1, item2) -> MHorizontal(__model, item1, item2) :> MTree
                | Leaf(item) -> MLeaf(__model, item) :> MTree
        
        static member Create(v : LayoutingModel.Tree) =
            ResetMod.Create(MTree.CreateValue v) :> IMod<_>
        
        [<System.Runtime.CompilerServices.Extension>]
        static member Update(m : IMod<MTree>, v : LayoutingModel.Tree) =
            let m = unbox<ResetMod<MTree>> m
            if not (m.GetValue().TryUpdate v) then
                m.Update(MTree.CreateValue v)
    
    and private MVertical(__initial : LayoutingModel.Tree, item1 : LayoutingModel.Tree, item2 : LayoutingModel.Tree) =
        inherit MTree()
        
        let mutable __current = __initial
        let _item1 = MTree.Create(item1)
        let _item2 = MTree.Create(item2)
        member x.item1 = _item1
        member x.item2 = _item2
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : LayoutingModel.Tree) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | Vertical(item1,item2) -> 
                        __current <- __model
                        MTree.Update(_item1,item1)
                        MTree.Update(_item2,item2)
                        true
                    | _ -> false
    
    and private MHorizontal(__initial : LayoutingModel.Tree, item1 : LayoutingModel.Tree, item2 : LayoutingModel.Tree) =
        inherit MTree()
        
        let mutable __current = __initial
        let _item1 = MTree.Create(item1)
        let _item2 = MTree.Create(item2)
        member x.item1 = _item1
        member x.item2 = _item2
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : LayoutingModel.Tree) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | Horizontal(item1,item2) -> 
                        __current <- __model
                        MTree.Update(_item1,item1)
                        MTree.Update(_item2,item2)
                        true
                    | _ -> false
    
    and private MLeaf(__initial : LayoutingModel.Tree, item : LayoutingModel.Tab) =
        inherit MTree()
        
        let mutable __current = __initial
        let _item = MTab.Create(item)
        member x.item = _item
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : LayoutingModel.Tree) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | Leaf(item) -> 
                        __current <- __model
                        MTab.Update(_item,item)
                        true
                    | _ -> false
    
    
    [<AutoOpen>]
    module MTreePatterns =
        let (|MVertical|MHorizontal|MLeaf|) (m : MTree) =
            match m with
            | :? MVertical as v -> MVertical(v.item1,v.item2)
            | :? MHorizontal as v -> MHorizontal(v.item1,v.item2)
            | :? MLeaf as v -> MLeaf(v.item)
            | _ -> failwith "impossible"
    
    
    
    
    
    
    type MModel(__initial : LayoutingModel.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<LayoutingModel.Model> = Aardvark.Base.Incremental.EqModRef<LayoutingModel.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<LayoutingModel.Model>
        let _tabs = MList.Create(__initial.tabs, (fun v -> MTab.Create(v)), (fun (m,v) -> MTab.Update(m, v)), (fun v -> v))
        
        member x.tabs = _tabs :> alist<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : LayoutingModel.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MList.Update(_tabs, v.tabs)
                
        
        static member Create(__initial : LayoutingModel.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : LayoutingModel.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<LayoutingModel.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let tabs =
                { new Lens<LayoutingModel.Model, Aardvark.Base.plist<LayoutingModel.Tab>>() with
                    override x.Get(r) = r.tabs
                    override x.Set(r,v) = { r with tabs = v }
                    override x.Update(r,f) = { r with tabs = f r.tabs }
                }
