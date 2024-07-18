namespace Aardvark.UI.Animation

open System

// Queue based on an array that is grown on demand.
// Dequeuing does not move any data around, instead the head index is incremented.
// Useful in scenarios where the queue is emptied regularly (to reset the head).
type private ArrayQueue<'Value>() =
    let mutable data = Array.zeroCreate 1
    let mutable count = 0
    let mutable head = 0

    member x.Count = count

    member x.Clear() =
        count <- 0
        head <- 0

    member x.Enqueue(value : 'Value) =
        if count >= data.Length then
            if head = count then
                x.Clear()
            else
                Array.Resize(&data, data.Length * 2)

        data.[count] <- value
        count <- count + 1

    member x.Dequeue(result : 'Value outref) =
        if head < count then
            result <- data.[head]
            head <- head + 1
            true
        else
            false