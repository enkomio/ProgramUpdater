namespace ES.Update

open System
open System.Security.Cryptography
open System.IO

[<AutoOpen>]
module CryptoUtility =

    let generateKeys() =
        use dsa = new ECDsaCng(HashAlgorithm = CngAlgorithm.Sha256)
        let publicKey = dsa.Key.Export(CngKeyBlobFormat.EccPublicBlob)
        let privateKey = dsa.Key.Export(CngKeyBlobFormat.EccPrivateBlob)        
        (publicKey, privateKey)

    let sign(data: Byte array, privateKey: Byte array) =
        use cngKey = CngKey.Import(privateKey, CngKeyBlobFormat.EccPrivateBlob, CngProvider.MicrosoftSoftwareKeyStorageProvider)
        use dsa = new ECDsaCng(cngKey)
        dsa.SignData(data)

    let verifySignature(data: Byte array, signature: Byte array, publicKey: Byte array) =
        use cngKey = CngKey.Import(publicKey, CngKeyBlobFormat.EccPublicBlob, CngProvider.MicrosoftSoftwareKeyStorageProvider)
        use dsa = new ECDsaCng(cngKey)
        dsa.VerifyData(data, signature)

    let sha256Raw(content: Byte array) =        
        use sha = new SHA256Managed()
        sha.ComputeHash(content)

    let sha256(content: Byte array) =        
        BitConverter.ToString(sha256Raw(content)).Replace("-",String.Empty).ToUpperInvariant()

    let decrypt(data: Byte array, key: Byte array, iv: Byte array) =
        use aes = new AesManaged(Key = key, IV = iv, Padding = PaddingMode.ISO10126)
        use ms = new MemoryStream(data)
        use resultStream = new MemoryStream()
        use sr = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read)
        sr.CopyTo(resultStream)
        sr.Close()
        resultStream.ToArray()

    let encrypt(data: Byte array, key: Byte array, iv: Byte array) =
        use aes = new AesManaged(Key = key, IV = iv, Padding = PaddingMode.ISO10126)
        use ms = new MemoryStream()
        use sw = new StreamWriter(new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        sw.Write(data)
        sw.Close()
        ms.ToArray()