namespace Installer

open System
open System.Security.Cryptography
open System.IO.Compression
open System.IO

[<AutoOpen>]
module Utility =
    let sha256Raw(content: Byte array) =        
        use sha = new SHA256Managed()
        sha.ComputeHash(content)

    let sha256(content: Byte array) =        
        BitConverter.ToString(sha256Raw(content)).Replace("-",String.Empty).ToUpperInvariant()

    let tryReadEntry(zipArchive: ZipArchive, name: String) =
        let entry =
            zipArchive.Entries
            |> Seq.tryFind(fun entry -> entry.FullName.Equals(name, StringComparison.OrdinalIgnoreCase))
        
        match entry with
        | Some entry ->
            use zipStream = entry.Open()
            use memStream = new MemoryStream()
            zipStream.CopyTo(memStream)
            Some <| memStream.ToArray()
        | None ->
            None

    let readEntry(zipArchive: ZipArchive, name: String) =
        tryReadEntry(zipArchive, name).Value