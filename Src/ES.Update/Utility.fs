namespace ES.Update

open System
open System.Security.Cryptography

[<AutoOpen>]
module Utility =

    let sha1(content: Byte array) =
        use sha = new SHA1CryptoServiceProvider()
        let result = sha.ComputeHash(content)
        BitConverter.ToString(result).Replace("-",String.Empty).ToUpperInvariant()