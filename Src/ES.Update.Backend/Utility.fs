namespace ES.Update.Backend

open System
open System.IO
open System.IO.Compression
open System.Text
open Suave
open Entities
open System.Security.Cryptography
open ES.Update

[<AutoOpen>]
module Utility =
    let private readIntegrityInfo(zipFile: String) = 
        use zipStream = File.OpenRead(zipFile)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read)
        let catalogEntry =
            zipArchive.Entries
            |> Seq.find(fun entry -> entry.FullName.Equals("catalog", StringComparison.OrdinalIgnoreCase))
        
        use zipStream = catalogEntry.Open()
        use memStream = new MemoryStream()
        zipStream.CopyTo(memStream)
        Encoding.UTF8.GetString(memStream.ToArray())

    let private addSignatureEntry(zipFile: String, signature: Byte array) =
        use zipStream = File.Open(zipFile, FileMode.Open)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Update)
        let zipEntry = zipArchive.CreateEntry("signature")
        use zipEntryStream = zipEntry.Open()
        zipEntryStream.Write(signature, 0, signature.Length)
        
    let addSignature(zipFile: String, privateKey: Byte array) =        
        // compute signature and add it to the new file
        let integrityInfo = readIntegrityInfo(zipFile)
        let signature = CryptoUtility.sign(Encoding.UTF8.GetBytes(integrityInfo), privateKey)
        addSignatureEntry(zipFile, signature)

    let createZipFile(zipFile: String, files: (File * Byte array) list, integrityInfo: String) =
        use zipStream = File.OpenWrite(zipFile)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create)

        // add integrity info and files
        let files = files |> List.map(fun (f, c) -> (f.ContentHash, c))
        ("catalog", integrityInfo |> Encoding.UTF8.GetBytes)::files
        |> List.iter(fun (name, content) ->
            let zipEntry = zipArchive.CreateEntry(name)
            use zipEntryStream = zipEntry.Open()
            zipEntryStream.Write(content, 0, content.Length)
        )

    let private getPostParameters(ctx: HttpContext) =
        // this method is necessary since Suave seems to have some problems in parsing params
        let data = Encoding.UTF8.GetString(ctx.request.rawForm)
        data.Split([|'&'|])
        |> Array.map(fun v -> v.Split([|'='|]))
        |> Array.filter(fun items -> items.Length > 1)
        |> Array.map(fun items -> (items.[0], String.Join("=", items.[1..])))

    let tryGetPostParameters(parameters: String list, ctx: HttpContext) =
        getPostParameters(ctx)
        |> Array.filter(fun (name, _) -> parameters |> List.contains name)
        |> fun resultValues ->
            if resultValues.Length = parameters.Length
            then Some(dict resultValues)
            else None