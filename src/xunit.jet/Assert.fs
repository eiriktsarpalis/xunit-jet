namespace xunit.jet

open System
open System.Reflection
open Microsoft.FSharp.Reflection
open TypeShape
open Xunit
open Xunit.Sdk

module private EqualityComparerImpl =

    type EqualDeepException (userMessage:string) = inherit XunitException(userMessage)

    type EqualityComparer<'T> = string list -> 'T -> 'T -> unit

    let formatError (message:string) (breadcrumbs : string list) : string =
        match breadcrumbs with
        | [] -> message
        |  _ -> sprintf "%s at %s" message (breadcrumbs |> Seq.rev |> String.concat ".")

    let errorf breadCrumbs fmt = Printf.ksprintf (fun msg -> raise <| new EqualDeepException(formatError msg breadCrumbs)) fmt

    let areMatchingNulls breadCrumbs (v1 : 'T) (v2 : 'T) =
        match v1, v2 with
        | null, null -> ()
        | _ -> errorf breadCrumbs "Expected was %A but actual was %A." v1 v2

    let areEqualCollectionCounts breadCrumbs (collectionName : string) (expectedLength : int) (actualLength : int) =
        if expectedLength <> actualLength then
            errorf breadCrumbs "%s collections were found of differing lengths. The expected length was %d but the actual was %d" 
                collectionName expectedLength actualLength

    let rec generateEqualityComparer<'T> () : EqualityComparer<'T> =
        let wrap (ecomp : EqualityComparer<'a>) = unbox ecomp
        let genUntyped (t : Type) =
            TypeShape.Create(t).Accept {
                new ITypeShapeVisitor<EqualityComparer<obj>> with
                    member __.Visit<'t> () =
                        let tEq = generateEqualityComparer<'t>()
                        fun bc t1 t2 -> tEq bc (t1 :?> 't) (t2 :?> 't)
            }

        match TypeShape.Create<'T> () with
        | Shape.Array s ->
            s.Accept {
                new IArrayVisitor<EqualityComparer<'T>> with
                    member __.Visit<'t>() =
                        let elemComparer = generateEqualityComparer<'t>()
                        wrap(fun breadCrumbs (expected : 't[]) (actual : 't[]) ->
                            areMatchingNulls breadCrumbs expected actual
                            areEqualCollectionCounts breadCrumbs "Array" expected.Length actual.Length
                            (expected,actual) ||> Array.iteri2 (fun i t1 t2 -> elemComparer (sprintf "[%d]" i :: breadCrumbs) t1 t2))
            }

        | Shape.FSharpList s ->
            s.Accept {
                new IFSharpListVisitor<EqualityComparer<'T>> with
                    member __.Visit<'t>() =
                        let elemComparer = generateEqualityComparer<'t>()
                        wrap(fun breadCrumbs (expected : 't list) (actual : 't list) ->
                            areEqualCollectionCounts breadCrumbs "List" expected.Length actual.Length
                            (expected,actual) ||> List.iteri2 (fun i t1 t2 -> elemComparer (sprintf "[%d]" i :: breadCrumbs) t1 t2))
            }

        | Shape.FSharpSet s ->
            s.Accept {
                new IFSharpSetVisitor<EqualityComparer<'T>> with
                    member __.Visit<'t when 't : comparison> () =
                        let elemComparer = generateEqualityComparer<'t>()
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
                        let valueComparer = generateEqualityComparer<'v> ()
                        wrap(fun breadCrumbs (expected : Map<'k,'v>) (actual : Map<'k,'v>) ->
                            // |A| = |B| && A \subset B => A = B
                            areEqualCollectionCounts breadCrumbs "Map" expected.Count actual.Count
                            for KeyValue(k,v) in expected do
                                match actual.TryFind k with
                                | None -> errorf breadCrumbs "Map expected key %A which was not found in actual." k
                                | Some v' -> valueComparer (sprintf "[%A]" k :: breadCrumbs) v v')
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

        | 

    //TODO: handle maps and sets
    // Note: Although this function has a recursive flow, recursion has to be done through reflection because the generic type can change with each iteration.
    let equalDeepHelper<'a> (selfReference:MethodInfo) (expected:'a) (actual:'a) (breadcrumb:string list) =

        // these ridiculous functions just tell the compiler that objects are of specific types
        let (|ListToList|_|) (candidate:obj) = candidate :?> System.Collections.IEnumerable |> Seq.cast<obj> |> Seq.toList |> Some
        let (|SeqToSeq|_|) (candidate:obj) = candidate :?> System.Collections.IEnumerable |> Seq.cast<obj> |> Some
        let (|ArrayToArray|_|) (candidate:obj) = candidate :?> System.Collections.IEnumerable |> Seq.cast<obj> |> Seq.toArray |> Some

        let formatError (message:string) (breadcrumbValue:string option) : string =
            let breadcrumbFolder (state:string) (breadcrumbItem:string) : string =
                match state.Length = 0 with
                | true -> breadcrumbItem
                | false -> sprintf "%s.%s" breadcrumbItem state
            let errorMessage = sprintf "%s at %s" message (List.fold breadcrumbFolder "" breadcrumb)
            match breadcrumbValue with
            | Some x -> sprintf "%s.%s" errorMessage x
            | None -> errorMessage

        let compare (expected:'b) (actual:'b) (message:string) (breadcrumbValue:string option) =
            match expected <> actual with
            | true -> new EqualDeepException(formatError message breadcrumbValue) |> raise
            | false -> ()

        let fail (message:string) (breadcrumbValue:string option) =
            new EqualDeepException(formatError message breadcrumbValue) |> raise

        let recurse (expected:'b) (actual:'b) (t:Type) (breadcrumbValue:string) =
            // differing strings fail the reflective binding, so assert on them locally
            //TODO: figure out how to get strings to bind recursively, as other types might be affected too
            match t.Name = "String" with
            | true -> compare expected actual (sprintf "String values for %A do not match. Expected '%A' but actual was '%A'" t.Name expected actual) (Some breadcrumbValue)
            | false ->
                let concreteMethod = selfReference.MakeGenericMethod([| t |])
                try
                    concreteMethod.Invoke(null, [| selfReference ; expected ; actual ; breadcrumbValue::breadcrumb |]) |> ignore
                with
                | :? TargetInvocationException as ex -> raise ex.InnerException // do this to trim off the excess stack trace

        let assertRecord (t:Type) expected actual =
            FSharpType.GetRecordFields t
            |> Seq.iter (fun f ->
                try
                    (f.GetValue(expected), f.GetValue(actual)) |> ignore
                with _ -> fail (sprintf "Actual field type for %A does not exist. Expected %A but actual was %A" f.Name expected actual) (Some f.Name)

                match (f.GetValue(expected), f.GetValue(actual)) with
                | (null, null) -> ()
                | (null, a) -> fail (sprintf "Field %A is expected to be None or null, but it is %A" f.Name a) (Some f.Name)
                | (e, null) -> fail (sprintf "Field %A is expected to be %A, but it is None or null" f.Name e) (Some f.Name)
                | (e, a) ->
                    compare (e.GetType().Name) (a.GetType().Name) (sprintf "Expected value for %A is of type %A while the actual is of type %A" f.Name (e.GetType().Name) (a.GetType().Name)) (Some f.Name)
                    recurse e a f.PropertyType f.Name)

        let assertCollection (expected:'b list) (actual:'b list) (collectionType:string) =
            compare
                (expected |> List.length)
                (actual |> List.length)
                (sprintf "%A collections were found of differing lengths. The expected length was %A but the actual was %A. The expected collection was %A and the actual was %A"
                    collectionType
                    (expected |> List.length)
                    (actual |> List.length)
                    expected
                    actual)
                None
            let assertListItem (index:int) (e:'b) (a:'b) = recurse e a (e.GetType()) (sprintf "[%d]" index)
            List.iteri2 assertListItem expected actual

        let assertList (expected:'b list) (actual:'b list) =
            assertCollection expected actual "list"

        let assertSeq (expected:'b seq) (actual:'b seq) =
            assertCollection (expected |> Seq.toList) (actual |> Seq.toList) "sequence"

        let assertArray (expected:'b array) (actual:'b array) =
            assertCollection (expected |> Array.toList) (actual |> Array.toList) "array"

        let assertTuple expected actual =
            assertCollection (expected |> FSharpValue.GetTupleFields |> Array.toList) (actual |> FSharpValue.GetTupleFields |> Array.toList) "tuple"

        let assertDiscriminatedUnion expected actual =
            let getDiscriminatedUnionUnderlyingObject obj = (obj, obj.GetType(), true) |> FSharpValue.GetUnionFields

            let (eUnionCaseInfo, eUnionFields) = getDiscriminatedUnionUnderlyingObject expected
            let (aUnionCaseInfo, aUnionFields) = getDiscriminatedUnionUnderlyingObject actual

            match (eUnionFields.Length, aUnionFields.Length) with
            | (0, _)
            | (_, 0) -> compare eUnionCaseInfo.Name aUnionCaseInfo.Name (sprintf "At least one discriminated union field only has a label. Expected type was %A but the actual was %A" eUnionCaseInfo.Name aUnionCaseInfo.Name) (Some eUnionCaseInfo.Name)
            | _ ->
                match (eUnionFields.[0], aUnionFields.[0]) with
                | (null, null) -> ()
                | (null, a) -> fail (sprintf "Discriminated union field %A is expected to be None or null, but it is %A" eUnionCaseInfo.Name a) (Some eUnionCaseInfo.Name)
                | (e, null) -> fail (sprintf "Discriminated union field %A is expected to be %A, but it is None or null" eUnionCaseInfo.Name e) (Some eUnionCaseInfo.Name)
                | (e, a) ->
                    compare (e.GetType().Name) (a.GetType().Name) (sprintf "Discriminated union field type is expected to be '%A' but is '%A'" (e.GetType().Name) (a.GetType().Name)) (Some eUnionCaseInfo.Name)
                    match e.GetType() |> FSharpType.IsRecord with
                    | true -> assertRecord (e.GetType()) e a
                    | false -> recurse e a (e.GetType()) eUnionCaseInfo.Name

        let t = typeof<'a>
        if t |> FSharpType.IsRecord then
            assertRecord t expected actual
        elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<list<_>> then
            match (expected, actual) with
            | (ListToList e, ListToList a) -> assertList e a
            | _ -> fail "Failed pattern matching on list" None
        elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<seq<_>> then
            match (expected, actual) with
            | (SeqToSeq e, SeqToSeq a) -> assertSeq e a
            | _ -> fail "Failed pattern matching on sequence" None
        elif t.IsArray then
            match (expected, actual) with
            | (ArrayToArray e, ArrayToArray a) -> assertArray e a
            | _ -> fail "Failed pattern matching on array" None
        elif t |> FSharpType.IsTuple then
            assertTuple expected actual
        elif t |> FSharpType.IsUnion then
            assertDiscriminatedUnion expected actual
        else compare (sprintf "%A" expected) (sprintf "%A" actual) (sprintf "Value is expected to be '%A' but is '%A'" expected actual) None

    let equalDeep<'a> (expected:'a) (actual:'a) =
        try Assert.Equal(expected, actual)
        with _ ->
            let t = typeof<'a>
            match FSharpType.IsRecord t || FSharpType.IsUnion t || (t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<list<_>>)
                    || (t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<seq<_>>) || t.IsArray || FSharpType.IsTuple t with
            | true ->
                let moduleInfo =
                    Assembly.GetExecutingAssembly().GetTypes()
                    |> Seq.find (fun t -> t.Name = EqualDeepModule)
                let helperMethod = moduleInfo.GetMethod(EqualDeepHelper)
                equalDeepHelper helperMethod expected actual [ "expected" ]
                // if we get here, it means that Assert.Equal found a problem, but our code did not; if Assert.Equal was reliable, we would let its exception bubble out at this point, but Assert.Equal fails unnecessarily for sequences that return identical results

            // the type isn't something special to F#, so it's either a string (which Assert will print relevant info for), or a more complex object that isn't handled here; let the failed assertion bubble out
            | false -> reraise()
