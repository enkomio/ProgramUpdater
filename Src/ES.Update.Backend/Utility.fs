namespace ES.Update.Backend

open System
open System.IO
open System.IO.Compression

[<AutoOpen>]
module Utility =

    let createZipFile(zipFile: String, files: (string * Byte array) array) =
        use zipStream = File.OpenWrite(zipFile)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create)
        files |> Array.iter(fun (name, content) ->
            let zipEntry = zipArchive.CreateEntry(name)
            use zipEntryStream = zipEntry.Open()
            zipEntryStream.Write(content, 0, content.Length)
        )