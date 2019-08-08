namespace ES.Update.Backend

open System

module Entities =
    type File = {
        Path: String
        ContentHash: String
    }

    type Application = {
        Version: Version
        Files: File array
    }
