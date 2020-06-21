namespace ES.Update

open System
open System.IO.Compression
open System.IO

module Utility =
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