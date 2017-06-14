namespace Demo.TestApp

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Demo.TestApp

[<AutoOpen>]
module Mutable =

    
    
    type MUrdar(__initial : Demo.TestApp.Urdar) =
        inherit obj()
        let mutable __current = __initial
        let _urdar = ResetMod.Create(__initial.urdar)
        
        member x.urdar = _urdar :> IMod<_>
        
        member x.Update(v : Demo.TestApp.Urdar) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_urdar,v.urdar)
                
        
        static member Create(__initial : Demo.TestApp.Urdar) : MUrdar = MUrdar(__initial)
        static member Update(m : MUrdar, v : Demo.TestApp.Urdar) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<Demo.TestApp.Urdar> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Urdar =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let urdar =
                { new Lens<Demo.TestApp.Urdar, Microsoft.FSharp.Core.int>() with
                    override x.Get(r) = r.urdar
                    override x.Set(r,v) = { r with urdar = v }
                    override x.Update(r,f) = { r with urdar = f r.urdar }
                }
    [<AbstractClass; StructuredFormatDisplay("{AsString}")>]
    type MTreeNode<'va,'na>() = 
        abstract member content : 'va
        abstract member children : Aardvark.Base.Incremental.alist<MTreeNode<'va,'na>>
        abstract member AsString : string
    
    
    and private MTreeNodeD<'a,'ma,'va>(__initial : Demo.TestApp.TreeNode<'a>, __ainit : 'a -> 'ma, __aupdate : 'ma * 'a -> unit, __aview : 'ma -> 'va) =
        inherit MTreeNode<'va,'va>()
        let mutable __current = __initial
        let _content = __ainit(__initial.content)
        let _children = MList.Create(__initial.children, (fun v -> MTreeNode.Create(v, (fun v -> __ainit(v)), (fun (m,v) -> __aupdate(m, v)), (fun v -> __aview(v)))), (fun (m,v) -> MTreeNode.Update(m, v)), (fun v -> v))
        
        override x.content = __aview(_content)
        override x.children = _children :> alist<_>
        
        member x.Update(v : Demo.TestApp.TreeNode<'a>) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                __aupdate(_content, v.content)
                MList.Update(_children, v.children)
                
        
        static member Update(m : MTreeNodeD<'a,'ma,'va>, v : Demo.TestApp.TreeNode<'a>) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        interface IUpdatable<Demo.TestApp.TreeNode<'a>> with
            member x.Update v = x.Update v
    
    and private MTreeNodeV<'a>(__initial : Demo.TestApp.TreeNode<'a>) =
        inherit MTreeNode<IMod<'a>,'a>()
        let mutable __current = __initial
        let _content = ResetMod.Create(__initial.content)
        let _children = MList.Create(__initial.children, (fun v -> MTreeNode.Create(v)), (fun (m,v) -> MTreeNode.Update(m, v)), (fun v -> v))
        
        override x.content = _content :> IMod<_>
        override x.children = _children :> alist<_>
        
        member x.Update(v : Demo.TestApp.TreeNode<'a>) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_content,v.content)
                MList.Update(_children, v.children)
                
        
        static member Update(m : MTreeNodeV<'a>, v : Demo.TestApp.TreeNode<'a>) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        interface IUpdatable<Demo.TestApp.TreeNode<'a>> with
            member x.Update v = x.Update v
    
    and [<AbstractClass; Sealed>] MTreeNode private() =
        static member Create<'a,'ma,'va>(__initial : Demo.TestApp.TreeNode<'a>, __ainit : 'a -> 'ma, __aupdate : 'ma * 'a -> unit, __aview : 'ma -> 'va) : MTreeNode<'va,'va> = MTreeNodeD<'a,'ma,'va>(__initial, __ainit, __aupdate, __aview) :> MTreeNode<'va,'va>
        static member Create<'a>(__initial : Demo.TestApp.TreeNode<'a>) : MTreeNode<IMod<'a>,'a> = MTreeNodeV<'a>(__initial) :> MTreeNode<IMod<'a>,'a>
        static member Update<'a,'xva,'xna>(m : MTreeNode<'xva,'xna>, v : Demo.TestApp.TreeNode<'a>) : unit = 
            match m :> obj with
            | :? IUpdatable<Demo.TestApp.TreeNode<'a>> as m -> m.Update(v)
            | _ -> failwith "cannot update"
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TreeNode =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let content<'a> =
                { new Lens<Demo.TestApp.TreeNode<'a>, 'a>() with
                    override x.Get(r) = r.content
                    override x.Set(r,v) = { r with content = v }
                    override x.Update(r,f) = { r with content = f r.content }
                }
            let children<'a> =
                { new Lens<Demo.TestApp.TreeNode<'a>, Aardvark.Base.plist<Demo.TestApp.TreeNode<'a>>>() with
                    override x.Get(r) = r.children
                    override x.Set(r,v) = { r with children = v }
                    override x.Update(r,f) = { r with children = f r.children }
                }
    [<AbstractClass; StructuredFormatDisplay("{AsString}")>]
    type MTree<'va,'na>() = 
        abstract member nodes : Aardvark.Base.Incremental.alist<MTreeNode<'va,'na>>
        abstract member AsString : string
    
    
    and private MTreeD<'a,'ma,'va>(__initial : Demo.TestApp.Tree<'a>, __ainit : 'a -> 'ma, __aupdate : 'ma * 'a -> unit, __aview : 'ma -> 'va) =
        inherit MTree<'va,'va>()
        let mutable __current = __initial
        let _nodes = MList.Create(__initial.nodes, (fun v -> MTreeNode.Create(v, (fun v -> __ainit(v)), (fun (m,v) -> __aupdate(m, v)), (fun v -> __aview(v)))), (fun (m,v) -> MTreeNode.Update(m, v)), (fun v -> v))
        
        override x.nodes = _nodes :> alist<_>
        
        member x.Update(v : Demo.TestApp.Tree<'a>) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MList.Update(_nodes, v.nodes)
                
        
        static member Update(m : MTreeD<'a,'ma,'va>, v : Demo.TestApp.Tree<'a>) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        interface IUpdatable<Demo.TestApp.Tree<'a>> with
            member x.Update v = x.Update v
    
    and private MTreeV<'a>(__initial : Demo.TestApp.Tree<'a>) =
        inherit MTree<IMod<'a>,'a>()
        let mutable __current = __initial
        let _nodes = MList.Create(__initial.nodes, (fun v -> MTreeNode.Create(v)), (fun (m,v) -> MTreeNode.Update(m, v)), (fun v -> v))
        
        override x.nodes = _nodes :> alist<_>
        
        member x.Update(v : Demo.TestApp.Tree<'a>) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MList.Update(_nodes, v.nodes)
                
        
        static member Update(m : MTreeV<'a>, v : Demo.TestApp.Tree<'a>) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        interface IUpdatable<Demo.TestApp.Tree<'a>> with
            member x.Update v = x.Update v
    
    and [<AbstractClass; Sealed>] MTree private() =
        static member Create<'a,'ma,'va>(__initial : Demo.TestApp.Tree<'a>, __ainit : 'a -> 'ma, __aupdate : 'ma * 'a -> unit, __aview : 'ma -> 'va) : MTree<'va,'va> = MTreeD<'a,'ma,'va>(__initial, __ainit, __aupdate, __aview) :> MTree<'va,'va>
        static member Create<'a>(__initial : Demo.TestApp.Tree<'a>) : MTree<IMod<'a>,'a> = MTreeV<'a>(__initial) :> MTree<IMod<'a>,'a>
        static member Update<'a,'xva,'xna>(m : MTree<'xva,'xna>, v : Demo.TestApp.Tree<'a>) : unit = 
            match m :> obj with
            | :? IUpdatable<Demo.TestApp.Tree<'a>> as m -> m.Update(v)
            | _ -> failwith "cannot update"
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Tree =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let nodes<'a> =
                { new Lens<Demo.TestApp.Tree<'a>, Aardvark.Base.plist<Demo.TestApp.TreeNode<'a>>>() with
                    override x.Get(r) = r.nodes
                    override x.Set(r,v) = { r with nodes = v }
                    override x.Update(r,f) = { r with nodes = f r.nodes }
                }
    
    
    type MModel(__initial : Demo.TestApp.Model) =
        inherit obj()
        let mutable __current = __initial
        let _boxHovered = ResetMod.Create(__initial.boxHovered)
        let _dragging = ResetMod.Create(__initial.dragging)
        let _lastName = MOption.Create(__initial.lastName)
        let _elements = MList.Create(__initial.elements)
        let _hasD3Hate = ResetMod.Create(__initial.hasD3Hate)
        let _boxScale = ResetMod.Create(__initial.boxScale)
        let _objects = MMap.Create(__initial.objects, (fun v -> MUrdar.Create(v)), (fun (m,v) -> MUrdar.Update(m, v)), (fun v -> v))
        let _lastTime = ResetMod.Create(__initial.lastTime)
        let _tree = MTree.Create(__initial.tree)
        
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.dragging = _dragging :> IMod<_>
        member x.lastName = _lastName :> IMod<_>
        member x.elements = _elements :> alist<_>
        member x.hasD3Hate = _hasD3Hate :> IMod<_>
        member x.boxScale = _boxScale :> IMod<_>
        member x.objects = _objects :> amap<_,_>
        member x.lastTime = _lastTime :> IMod<_>
        member x.tree = _tree
        
        member x.Update(v : Demo.TestApp.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_boxHovered,v.boxHovered)
                ResetMod.Update(_dragging,v.dragging)
                MOption.Update(_lastName, v.lastName)
                MList.Update(_elements, v.elements)
                ResetMod.Update(_hasD3Hate,v.hasD3Hate)
                ResetMod.Update(_boxScale,v.boxScale)
                MMap.Update(_objects, v.objects)
                ResetMod.Update(_lastTime,v.lastTime)
                MTree.Update(_tree, v.tree)
                
        
        static member Create(__initial : Demo.TestApp.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Demo.TestApp.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<Demo.TestApp.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let boxHovered =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.boxHovered
                    override x.Set(r,v) = { r with boxHovered = v }
                    override x.Update(r,f) = { r with boxHovered = f r.boxHovered }
                }
            let dragging =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.dragging
                    override x.Set(r,v) = { r with dragging = v }
                    override x.Update(r,f) = { r with dragging = f r.dragging }
                }
            let lastName =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.Option<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.lastName
                    override x.Set(r,v) = { r with lastName = v }
                    override x.Update(r,f) = { r with lastName = f r.lastName }
                }
            let elements =
                { new Lens<Demo.TestApp.Model, Aardvark.Base.plist<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.elements
                    override x.Set(r,v) = { r with elements = v }
                    override x.Update(r,f) = { r with elements = f r.elements }
                }
            let hasD3Hate =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.hasD3Hate
                    override x.Set(r,v) = { r with hasD3Hate = v }
                    override x.Update(r,f) = { r with hasD3Hate = f r.hasD3Hate }
                }
            let boxScale =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.boxScale
                    override x.Set(r,v) = { r with boxScale = v }
                    override x.Update(r,f) = { r with boxScale = f r.boxScale }
                }
            let objects =
                { new Lens<Demo.TestApp.Model, Aardvark.Base.hmap<Microsoft.FSharp.Core.string,Demo.TestApp.Urdar>>() with
                    override x.Get(r) = r.objects
                    override x.Set(r,v) = { r with objects = v }
                    override x.Update(r,f) = { r with objects = f r.objects }
                }
            let lastTime =
                { new Lens<Demo.TestApp.Model, Aardvark.Base.MicroTime>() with
                    override x.Get(r) = r.lastTime
                    override x.Set(r,v) = { r with lastTime = v }
                    override x.Update(r,f) = { r with lastTime = f r.lastTime }
                }
            let tree =
                { new Lens<Demo.TestApp.Model, Demo.TestApp.Tree<Microsoft.FSharp.Core.int>>() with
                    override x.Get(r) = r.tree
                    override x.Set(r,v) = { r with tree = v }
                    override x.Update(r,f) = { r with tree = f r.tree }
                }
