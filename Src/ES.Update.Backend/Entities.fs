namespace ES.Update.Backend

open System

module Entities =
    let DefaultVersion = "0.0"

    type File = {
        Path: String
        ContentHash: String
    }

    type Application = {
        Version: Version
        Files: File array
    }
