namespace SortedHashMap

open FSharp.Data.Adaptive

type SortedHashMap<'k, 'v when 'k : equality>(comparer : 'v -> 'v -> int, map : HashMap<'k, 'v>, sortedValues : IndexList<'v>, sortedKeys : IndexList<'k>) =

    static member Empty(comparer : 'v -> 'v -> int) = // -1 = smaller | 0 = equal | 1 = greater
       SortedHashMap(comparer, HashMap.empty, IndexList.empty, IndexList.empty)

    member x.SortedKeys : IndexList<'k> = sortedKeys
    member x.SortedValues : IndexList<'v> = sortedValues
    member x.Map : HashMap<'k, 'v> = map

    member x.Add(key : 'k, value : 'v) : SortedHashMap<'k, 'v> =
        
        let newMap = HashMap.add key value map // overwrite
        let newSortedValues, newSortedKeys = 
            
            if sortedValues.IsEmpty && sortedKeys.IsEmpty then IndexList.single value, IndexList.single key  // first item
            else 
                let compareList = sortedValues |> IndexList.map (fun v -> comparer v value) 
                match map |> HashMap.containsKey key, compareList |> IndexList.tryFindIndex 0 with
                | true, Some p -> // update ITEM / order does not change!
                    sortedValues |> IndexList.update p (fun _ -> value), sortedKeys   
                | false, Some p -> // add ITEM / different item with equal sorting entree exisits -> insert directly afterwards
                    sortedValues |> IndexList.insertAfter p value, sortedKeys |> IndexList.insertAfter p key 
                | false, None -> // add ITEM
                    match compareList |> IndexList.tryFindIndex 1 with
                    | Some next -> sortedValues |> IndexList.insertBefore next value, sortedKeys |> IndexList.insertBefore next key // insert before next larger item
                    | None -> sortedValues |> IndexList.add value, sortedKeys |> IndexList.add key  // largest item -> add at end
                | true, None -> // update ITEM
                    // remove old entries in sorting
                    let oldPosition = sortedKeys |> IndexList.findIndex key
                    let sortedKeys = sortedKeys |> IndexList.remove oldPosition
                    let sortedValues = sortedValues |> IndexList.remove oldPosition

                    match compareList |> IndexList.tryFindIndex 1 with
                    | Some next -> sortedValues |> IndexList.insertBefore next value, sortedKeys |> IndexList.insertBefore next key // insert before next larger item
                    | None -> sortedValues |> IndexList.add value, sortedKeys |> IndexList.add key  // largest item -> add at end
        
        SortedHashMap(comparer, newMap, newSortedValues, newSortedKeys)

    member x.Remove(key : 'k) : SortedHashMap<'k, 'v> =
        //let value = map |> HashMap.find key
        //let pos = sortedValues |> IndexList.map (fun v -> comparer v value) |> IndexList.findIndex 0
        let newMap = map |> HashMap.remove key
        let pos = sortedKeys |> IndexList.findIndex key
        let newValues = sortedValues |> IndexList.remove pos
        let newKeys = sortedKeys |> IndexList.remove pos
        SortedHashMap(comparer, newMap, newValues, newKeys)