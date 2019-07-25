namespace ES.Update.Backend

open System
open ES.Update.Entities

type UpdateManager(workingDirectory: String) =
    
    member this.GetLatestVersion() =
        "0"

    member this.GetUpdates() =
        Array.empty<UpdateBundle>