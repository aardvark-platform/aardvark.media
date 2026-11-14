namespace Aardvark.UI.Tests

open System
open Aardvark.UI.Primitives
open Expecto

module ``Integrator Tests`` =

    let private bits = BitConverter.DoubleToInt64Bits
    let private invalid = [
        for cap in [0.0; -0.0; -1.0; -Double.Epsilon; Double.NegativeInfinity; Double.NaN] do
            for dt in [-2.0; 0.0; 1.0] do
                yield cap, dt, "maxDt"
        for cap in [0.25; Double.PositiveInfinity] do
            for dt in [Double.NaN; Double.NegativeInfinity; Double.PositiveInfinity] do
                yield cap, dt, "dt"
        for cap, dt in [
            Double.Epsilon, 1.0
            Double.Epsilon, Double.MaxValue
            1e-300, 1.0
            1.0, Double.MaxValue
            1e-10, 1e20
            Math.Pow(2.0, -54.0), 1.0
        ] do
            yield cap, dt, "maxDt"
    ]

    let private partitions = [
        0.25, 0.75, [0.25; 0.25; 0.25]
        0.25, 0.875, [0.25; 0.25; 0.25; 0.125]
        0.25, 1.0, [0.25; 0.25; 0.25; 0.25]
        0.1, 0.3, [0.1; 0.1; 0.09999999999999998]
        0.2, 0.6, [0.2; 0.2; 0.19999999999999996]
        1.0, Math.BitIncrement 1.0, [1.0; 2.220446049250313e-16]
        Double.Epsilon, 5.0 * Double.Epsilon, List.replicate 5 Double.Epsilon
        Double.MaxValue / 2.0, Double.MaxValue, List.replicate 2 (Double.MaxValue / 2.0)
        0.03125, 0.15625, List.replicate 5 0.03125
    ]

    [<Tests>]
    let tests = testList "Primitives.Camera.Integrator" [
        test "Invalid arguments fail before any application step" {
            let failures = ResizeArray<string>()
            for cap, dt, parameter in invalid do
                let sentinel = InvalidOperationException "Application step must not run for invalid arguments"
                let mutable calls = 0
                let actual =
                    try
                        Integrator.integrate cap (fun _ _ -> calls <- calls + 1; raise sentinel) 7 dt |> ignore
                        None
                    with error -> Some error
                let validError = match actual with Some (:? ArgumentException as error) -> error.ParamName = parameter | _ -> false
                if calls <> 0 || not validError then
                    let actual = match actual with Some error -> error.GetType().Name | None -> "success"
                    failures.Add($"maxDt={cap:R}, dt={dt:R}: expected ArgumentException({parameter}) before callback; got {actual}, calls={calls}")
            if failures.Count > 0 then failtest (String.concat "\n" failures)
        }

        test "Finite single steps retain signed intervals references and infinite caps" {
            let controls = [
                for cap in [Double.Epsilon; 0.25; 1.0; Double.PositiveInfinity] do
                    for dt in [-Double.MaxValue; -1.0; -Double.Epsilon; -0.0; 0.0; Double.Epsilon] do
                        yield cap, dt
                for dt in [0.25; 1.0; Double.MaxValue] do
                    yield dt, dt
                    yield Double.PositiveInfinity, dt
            ]
            for cap, dt in controls do
                let original, returned = obj(), obj()
                let mutable seen = []
                let result = Integrator.integrate cap (fun model step -> seen <- (model, bits step) :: seen; returned) original dt
                Expect.isTrue (obj.ReferenceEquals(result, returned)) "Return the callback's exact model reference"
                match seen with
                | [model, step] ->
                    Expect.isTrue (obj.ReferenceEquals(model, original)) "Callback receives the original model"
                    Expect.equal step (bits dt) "One exact interval, including negative/signed zero and finite extremes"
                | _ -> failtestf "Expected exactly one callback, got %d" seen.Length
        }

        test "Exact remainder subnormal and boundary partitions thread models in order" {
            for cap, dt, expected in partitions do
                let seen = ResizeArray<int * int64>()
                let result = Integrator.integrate cap (fun model step -> seen.Add(model, bits step); 3 * model + 1) 7 dt
                let models = expected |> List.scan (fun model _ -> 3 * model + 1) 7
                let expectedTrace = List.zip (models |> List.take expected.Length) (expected |> List.map bits)
                Expect.sequenceEqual seen expectedTrace $"Exact original subtraction and callback order for {cap:R}/{dt:R}"
                Expect.equal result (List.last models) "Every returned model feeds the next step"
        }

        test "Callback failures retain identity prefix and stop further steps" {
            let expected = [0.25; 0.25; 0.25; 0.125] |> List.map bits
            for stop in 1 .. expected.Length do
                let error = InvalidOperationException $"step {stop} failed"
                let seen = ResizeArray<int64>()
                let actual =
                    try
                        Integrator.integrate 0.25 (fun model step ->
                            seen.Add(bits step)
                            if seen.Count = stop then raise error
                            model + 1) 0 0.875 |> ignore
                        None
                    with caught -> Some caught
                Expect.isTrue (actual.IsSome && obj.ReferenceEquals(actual.Value, error)) "Preserve the exact application exception"
                Expect.sequenceEqual seen (List.take stop expected) "No callbacks after the throwing step"
            for dt in [-1.0; -0.0; 0.0; 0.125] do
                let error = InvalidOperationException "single step failed"
                let mutable calls = 0
                let actual = try Integrator.integrate 0.25 (fun _ _ -> calls <- calls + 1; raise error) () dt |> ignore; None with caught -> Some caught
                Expect.isTrue (actual.IsSome && obj.ReferenceEquals(actual.Value, error)) "Single-step exception identity"
                Expect.equal calls 1 "A throwing single step is invoked once"
        }
    ]
