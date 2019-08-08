namespace ES.Update

open System
open System.Security.Cryptography
open System.IO

[<AutoOpen>]
module Utility =
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