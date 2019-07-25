namespace ES.Update

open System

module Entities =
    [<CLIMutable>]
    type UpdateFile = {
        Path: String
        Md5: String
    }

    [<CLIMutable>]
    type UpdateBundle = {
        Files: UpdateFile array
    }
