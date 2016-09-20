module xunit.jet.Assert

open System
open System.Reflection
open System.Collections.Concurrent
open Microsoft.FSharp.Reflection
open TypeShape
open Xunit
open Xunit.Sdk

type EqualDeepException (userMessage:string) = inherit XunitException(userMessage)

module private EqualityComparerImpl =

    let formatError (message:string) (breadcrumbs : string list) : string =
        match breadcrumbs with
        | [] -> message
        |  _ -> sprintf "%s at %s" message (breadcrumbs |> Seq.rev |> String.concat ".")

    let errorf breadCrumbs fmt = 
        Printf.ksprintf (fun msg -> raise <| new EqualDeepException(formatError msg breadCrumbs)) fmt

    let areMatchingNulls breadCrumbs (v1 : 'T) (v2 : 'T) =
        match v1, v2 with
        | null, null -> ()
        | null, _ 
        | _, null -> errorf breadCrumbs "Expected was %A but actual was %A." v1 v2
        | _ -> ()

    let areEqualCollectionCounts breadCrumbs (collectionName : string) (expectedLength : int) (actualLength : int) =
        if expectedLength <> actualLength then
            errorf breadCrumbs "%s collections were found of differing lengths. The expected length was %d but the actual was %d" 
                collectionName expectedLength actualLength

    /// Asserts for equality of expected and actual input
    type EqualityComparer<'T> = string list -> 'T -> 'T -> unit

    /// Generates an Equality assertion checker for given type
    let rec generateEqualityComparer<'T> () : EqualityComparer<'T> =
        let wrap (ecomp : EqualityComparer<'a>) : EqualityComparer<'T> = unbox ecomp
        let genUntyped (t : Type) =
            TypeShape.Create(t).Accept {
                new ITypeShapeVisitor<EqualityComparer<obj>> with
                    member __.Visit<'t> () =
                        let tEq = generateEqualityComparerCached<'t>()
                        fun bc t1 t2 -> tEq bc (t1 :?> 't) (t2 :?> 't)
            }

        match TypeShape.Create<'T> () with
        | Shape.Array s ->
            s.Accept {
                new IArrayVisitor<EqualityComparer<'T>> with
                    member __.Visit<'t>() =
                        let elemComparer = generateEqualityComparerCached<'t>()
                        wrap(fun breadCrumbs (expected : 't[]) (actual : 't[]) ->
                            areMatchingNulls breadCrumbs expected actual
                            areEqualCollectionCounts breadCrumbs "Array" expected.Length actual.Length
                            (expected,actual) ||> Array.iteri2 (fun i t1 t2 -> elemComparer (sprintf "[%d]" i :: breadCrumbs) t1 t2))
            }

        | Shape.FSharpList s ->
            s.Accept {
                new IFSharpListVisitor<EqualityComparer<'T>> with
                    member __.Visit<'t>() =
                        let elemComparer = generateEqualityComparerCached<'t>()
                        wrap(fun breadCrumbs (expected : 't list) (actual : 't list) ->
                            areEqualCollectionCounts breadCrumbs "List" expected.Length actual.Length
                            (expected,actual) ||> List.iteri2 (fun i t1 t2 -> elemComparer (sprintf "[%d]" i :: breadCrumbs) t1 t2))
            }

        | Shape.FSharpSet s ->
            s.Accept {
                new IFSharpSetVisitor<EqualityComparer<'T>> with
                    member __.Visit<'t when 't : comparison> () =
                        let elemComparer = generateEqualityComparerCached<'t>()
                        wrap(fun breadCrumbs (expected : Set<'t>) (actual : Set<'t>) ->
                            // |A| = |B| && A \subset B => A = B
                            areEqualCollectionCounts breadCrumbs "Set" expected.Count actual.Count
                            for e in expected do
                                if not <| actual.Contains e then
                                    errorf breadCrumbs "Set expected element %A which was not found in actual." e)
            }

        | Shape.FSharpMap s ->
            s.Accept {
                new IFSharpMapVisitor<EqualityComparer<'T>> with
                    member __.Visit<'k, 'v when 'k : comparison> () =
                        let valueComparer = generateEqualityComparerCached<'v> ()
                        wrap(fun breadCrumbs (expected : Map<'k,'v>) (actual : Map<'k,'v>) ->
                            // |A| = |B| && A \subset B => A = B
                            areEqualCollectionCounts breadCrumbs "Map" expected.Count actual.Count
                            for KeyValue(k,v) in expected do
                                match actual.TryFind k with
                                | None -> errorf breadCrumbs "Map expected key %A which was not found in actual." k
                                | Some v' -> valueComparer (sprintf "[%A]" k :: breadCrumbs) v v')
            }

        | Shape.Enumerable s ->
            s.Accept {
                new IEnumerableVisitor<EqualityComparer<'T>> with
                    member __.Visit<'Enum, 't when 'Enum :> seq<'t>> () =
                        let elemComparer = generateEqualityComparerCached<'t>()
                        wrap(fun breadCrumbs (expected : 'Enum) (actual : 'Enum) ->
                            let expected,actual = Seq.toArray expected, Seq.toArray actual
                            areEqualCollectionCounts breadCrumbs "Seq" expected.Length actual.Length
                            (expected,actual) ||> Array.iteri2 (fun i t1 t2 -> elemComparer (sprintf "[%d]" i :: breadCrumbs) t1 t2))
            }

        | Shape.Tuple ->
            let fieldComparers = FSharpType.GetTupleElements typeof<'T> |> Array.map genUntyped
            let fieldReader = FSharpValue.PreComputeTupleReader typeof<'T>

            fun bc expected actual ->
                let values1, values2 = fieldReader expected, fieldReader actual
                for i = 0 to fieldComparers.Length - 1 do
                    fieldComparers.[i] (sprintf "[%d]" i :: bc) values1.[i] values2.[i]

        | Shape.FSharpRecord s ->
            let fields = s.Properties |> List.toArray
            let fieldComparers = fields |> Array.map (fun p -> genUntyped p.PropertyType)
            fun bc expected actual ->
                for i = 0 to fields.Length - 1 do
                    let field = fields.[i]
                    let expectedField,actualField = field.GetValue(expected), field.GetValue(actual)
                    fieldComparers.[i] (field.Name :: bc) expectedField actualField


        | Shape.FSharpUnion s ->
            let ucis = s.UnionCaseInfo |> List.toArray
            let fieldss = ucis |> Array.map (fun uci -> uci.GetFields())
            let comparerss = fieldss |> Array.map (fun fs -> fs |> Array.map (fun f -> genUntyped f.PropertyType))
            fun breadCrumbs expected actual ->
                let tag1,tag2 = s.GetTagUntyped expected, s.GetTagUntyped actual
                if tag1 <> tag2 then
                    errorf breadCrumbs "Expected union case %A but the actual was %A" ucis.[tag1].Name ucis.[tag2].Name

                let uci = ucis.[tag1]
                let fields = fieldss.[tag1]
                let comparers = comparerss.[tag1]
                for i = 0 to fields.Length - 1 do
                    let field = fields.[i]
                    let expectedField,actualField = field.GetValue(expected), field.GetValue(actual)
                    comparers.[i] (uci.Name :: breadCrumbs) expectedField actualField

        | Shape.Equality s ->
            s.Accept { new IEqualityVisitor<EqualityComparer<'T>> with
                member __.Visit<'t when 't : equality> () =
                    wrap(fun breadCrumbs (expected:'t) (actual:'t) ->
                        if expected <> actual then
                            errorf breadCrumbs "Value was expected to be '%A' but was '%A'" expected actual) }

        | _ -> failwithf "Type %A does not support equality" typeof<'T>


    and private cache = new ConcurrentDictionary<Type, obj>()
    and generateEqualityComparerCached<'T> () : EqualityComparer<'T> =
        cache.GetOrAdd(typeof<'T>, fun _ -> generateEqualityComparer<'T>() :> obj) :?> _

let equalDeep<'a> (expected:'a) (actual:'a) =
    let cmp = EqualityComparerImpl.generateEqualityComparerCached<'a>()
    cmp [] expected actual