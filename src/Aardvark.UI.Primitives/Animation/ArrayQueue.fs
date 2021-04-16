namespace Aardvark.UI.Anewmation

open System

// Queue based on an array that is grown on demand.
// Dequeuing does not move any data around, instead the head index is incremented.
// Useful in scenarios where the queue is emptied regularly (to reset the head).
type private ArrayQueue<'Value> =
    struct
        val mutable private data : 'Value[]
        val mutable private count : int
        val mutable private head : int

        private new (data, count, head) =
            { data = data; count = count; head = head}

        static member Empty =
            ArrayQueue<'Value>(Array.zeroCreate 1, 0, 0)

        member x.Count = x.count

        member x.Item(index : int) : 'Value inref =
            &x.data.[x.head + index]

        member x.Clear() =
            x.count <- 0
            x.head <- 0

        member x.Enqueue(value : 'Value) =
            if x.count >= x.data.Length then
                if x.head = x.count then
                    x.Clear()
                else
                    Array.Resize(&x.data, x.data.Length * 2)

            x.data.[x.count] <- value
            x.count <- x.count + 1

        member x.Dequeue(result : 'Value outref) =
            if x.head < x.count then
                result <- x.data.[x.head]
                x.head <- x.head + 1
                true
            else
                false
    end

