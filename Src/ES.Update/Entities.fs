namespace ES.Update

open System

// create this class to easier the C# interoperability
type Result(result: Boolean) =
    member val Success = result with get, set
    member val Error = String.Empty with get, set

