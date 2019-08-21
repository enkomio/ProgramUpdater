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

    let private addEntry(zipFile: String, name: String, content: Byte array) =
        use zipStream = File.Open(zipFile, FileMode.Open)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Update)
        let zipEntry = zipArchive.CreateEntry(name)
        use zipEntryStream = zipEntry.Open()
        zipEntryStream.Write(content, 0, content.Length)
        
    let addSignature(zipFile: String, privateKey: Byte array) =        
        // compute signature and add it to the new file
        let integrityInfo = readIntegrityInfo(zipFile)
        let signature = CryptoUtility.sign(Encoding.UTF8.GetBytes(integrityInfo), privateKey)
        addEntry(zipFile, "signature", signature)

    let private getRelativePath(fileName: String, basePath: String) =
        fileName.Replace(basePath, String.Empty).TrimStart(Path.DirectorySeparatorChar)

    let addInstaller(zipFile: String, installerPath: String, privateKey: Byte array) =
        // compute the integrtiy info for the installer
        let integrityInfo = new StringBuilder()
        Directory.GetFiles(installerPath, "*.*", SearchOption.AllDirectories)
        |> Array.iter(fun fileName ->
            let relativePath = getRelativePath(fileName, installerPath)
            let hashValue = sha256(File.ReadAllBytes(fileName))
            integrityInfo.AppendFormat("{0},{1}", hashValue, relativePath).AppendLine() |> ignore
        )

        // sign the integrity info and add the catalog
        let catalog = Encoding.UTF8.GetBytes(integrityInfo.ToString())
        let signature = CryptoUtility.sign(catalog, privateKey)
        addEntry(zipFile, "installer-signature", signature)
        addEntry(zipFile, "installer-catalog", catalog)

        // add all files from installerPath to the zip file
        Directory.GetFiles(installerPath, "*.*", SearchOption.AllDirectories)
        |> Array.iter(fun fileName ->
            let relativePath = getRelativePath(fileName, installerPath)
            addEntry(zipFile, relativePath, File.ReadAllBytes(fileName))
        )

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