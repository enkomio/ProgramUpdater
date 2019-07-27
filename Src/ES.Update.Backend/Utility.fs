namespace ES.Update.Backend

open System
open System.IO
open System.Net
open System.IO.Compression
open System.Security.Cryptography

[<AutoOpen>]
module Utility =

    let getAllHashPerVersion(workingDirectory: String) =
        Directory.GetFiles(Path.Combine(workingDirectory, "Versions"))
        |> Array.map(fun filename ->
            (
                Path.GetFileNameWithoutExtension(filename),            
                (File.ReadAllLines(filename) |> Array.map(fun line -> line.Split([|','|]).[0]))
            )
        )