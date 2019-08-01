namespace ES.Update.Backend

open System

module Entities =
    type File = {
        Path: String
        Sha1: String
    }

    type Application = {
        Version: Version
        Files: File array
    }
