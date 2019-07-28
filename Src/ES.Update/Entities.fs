namespace ES.Update

open System

module Entities =
    [<CLIMutable>]
    type File = {
        Path: String
        Sha1: String
    }

    [<CLIMutable>]
    type Application = {
        Version: Version
        Files: File array
    }
