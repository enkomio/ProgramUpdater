namespace ES.Update

open System
open System.IO
open System.Net
open System.IO.Compression
open System.Security.Cryptography

[<AutoOpen>]
module Utility =

    let iterZip(filename: String) = seq {
        use fileHandle = File.OpenRead(filename)
        for zipEntry in (new System.IO.Compression.ZipArchive(fileHandle, ZipArchiveMode.Read)).Entries do
            use zipStream = zipEntry.Open()
            use memStream = new MemoryStream()
            zipStream.CopyTo(memStream)            
            yield (zipEntry.FullName, memStream.ToArray())
    }

    let sha1(content: Byte array) =
        use sha = new SHA1CryptoServiceProvider()
        let result = sha.ComputeHash(content)
        BitConverter.ToString(result).Replace("-",String.Empty).ToUpperInvariant()