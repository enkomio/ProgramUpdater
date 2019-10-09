namespace ES.Update

open System
open System.Security.Cryptography
open System.IO
open Org.BouncyCastle.Crypto.Generators
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Asn1.X9
open Org.BouncyCastle.Security
open Org.BouncyCastle.Pkcs
open Org.BouncyCastle.X509

[<AutoOpen>]
module CryptoUtility =
    [<CompiledName("GenerateKeys")>]
    let generateKeys() =
        let ecSpec = ECNamedCurveTable.GetOid("prime256v1")
        let gen = new ECKeyPairGenerator("ECDSA")
        let keyGenParam = new ECKeyGenerationParameters(ecSpec, new SecureRandom())
        gen.Init(keyGenParam)
        let keyPair = gen.GenerateKeyPair()
        
        // public key
        let publicKey = keyPair.Public :?> ECPublicKeyParameters
        let publicKeyBytes = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey).GetDerEncoded()
        
        // private key
        let privatekey = keyPair.Private :?> ECPrivateKeyParameters
        let pkinfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privatekey)
        let privatekeyBytes = pkinfo.GetDerEncoded()

        (publicKeyBytes, privatekeyBytes)

    let sign(data: Byte array, privateKey: Byte array) =
        let privateKey = PrivateKeyFactory.CreateKey(privateKey)
        let signer  = SignerUtilities.GetSigner("SHA256withECDSA")
        signer.Init(true, privateKey)
        signer.BlockUpdate(data, 0, data.Length)
        signer.GenerateSignature()

    let verifySignature(data: Byte array, signature: Byte array, publicKey: Byte array) =
        let bpubKey = PublicKeyFactory.CreateKey(publicKey) :?> ECPublicKeyParameters
        let signer  = SignerUtilities.GetSigner("SHA256withECDSA")
        signer.Init (false, bpubKey)        
        signer.BlockUpdate (data, 0, data.Length)
        signer.VerifySignature(signature)

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
        use sw = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write)
        sw.Write(data, 0, data.Length)
        sw.Close()
        ms.ToArray()