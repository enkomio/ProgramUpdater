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
    let private getEncryptionKey(clientKey: String, privateKey: String) =
        let publicBytes = Convert.FromBase64String(clientKey)
        let privateBytes = Convert.FromBase64String(privateKey)
        use cng = new ECDiffieHellmanCng(CngKey.Import(privateBytes, CngKeyBlobFormat.EccPrivateBlob, CngProvider.MicrosoftSoftwareKeyStorageProvider))
        cng.DeriveKeyMaterial(CngKey.Import(publicBytes, CngKeyBlobFormat.EccPublicBlob))

    let private encrypt(data: String, clientKey: String, iv: String, privateKey: String) =
        use aes = new AesManaged(Key = getEncryptionKey(clientKey, privateKey), IV = Convert.FromBase64String(iv))
        use ms = new MemoryStream()
        use sw = new StreamWriter(new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        sw.Write(data)
        sw.Close()
        ms.ToArray()

    let private readIntegrityInfo(zipFile: String) = 
        use zipStream = File.OpenRead(zipFile)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read)
        let sha1Entry =
            zipArchive.Entries
            |> Seq.find(fun entry -> entry.FullName.Equals("sha1", StringComparison.OrdinalIgnoreCase))
        
        use zipStream = sha1Entry.Open()
        use memStream = new MemoryStream()
        zipStream.CopyTo(memStream)
        Encoding.UTF8.GetString(memStream.ToArray())

    let private addSignatureEntry(zipFile: String, signature: Byte array) =
        use zipStream = File.Open(zipFile, FileMode.Open)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Update)
        let zipEntry = zipArchive.CreateEntry("signature")
        use zipEntryStream = zipEntry.Open()
        let sha1Signature = Encoding.UTF8.GetBytes(sha1(signature))
        zipEntryStream.Write(sha1Signature, 0, sha1Signature.Length)

    let removeOldBinaryFiles(timeout: Int32) =
        let treshold = DateTime.Now.Subtract(TimeSpan.FromSeconds(float timeout))
        let tempPath = Path.Combine(Path.GetTempPath(), "UpdateBinaries")
        if Directory.Exists(tempPath) then
            Directory.GetFiles(tempPath)
            |> Array.iter(fun file ->
                let fileInfo = new FileInfo(file)
                if fileInfo.CreationTime < treshold then
                    File.Delete(file)
            )

    let addSignature(zipFile: String, clientkey: String, iv: String, privateKey: String) =
        // create signed zip file
        let tempPath = Path.Combine(Path.GetTempPath(), "UpdateBinaries")
        Directory.CreateDirectory(tempPath) |> ignore

        let signedZipFile = 
            Path.Combine(
                tempPath, 
                Path.GetFileNameWithoutExtension(zipFile) 
                + "-SIGID-"
                + Guid.NewGuid().ToString("N") 
                + ".zip"
            )        
        File.Copy(zipFile, signedZipFile)

        // compute signature and add it to the new file
        let integrityInfo = readIntegrityInfo(zipFile)
        let signature = encrypt(integrityInfo, clientkey, iv, privateKey)        
        addSignatureEntry(signedZipFile, signature)
        signedZipFile

    let createZipFile(zipFile: String, files: (File * Byte array) list, integrityInfo: String) =
        use zipStream = File.OpenWrite(zipFile)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create)

        // add integrity info and files
        let files = files |> List.map(fun (f, c) -> (f.Sha1, c))
        ("sha1", integrityInfo |> Encoding.UTF8.GetBytes)::files
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