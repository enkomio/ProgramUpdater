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
open System.Net.NetworkInformation
open System.Text

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

    let hexSign(data: Byte array, privateKey: Byte array) =
        BitConverter.ToString(sign(data, privateKey)).Replace("-","")

    let verifyData(data: Byte array, signature: Byte array, publicKey: Byte array) =
        let bpubKey = PublicKeyFactory.CreateKey(publicKey) :?> ECPublicKeyParameters
        let signer  = SignerUtilities.GetSigner("SHA256withECDSA")
        signer.Init (false, bpubKey)        
        signer.BlockUpdate (data, 0, data.Length)
        signer.VerifySignature(signature)

    let verifyString(data: String, signature: String, publicKey: Byte array) =
        let byteSignature = [|
            for i in 0..2..(signature.Length-1) do
                let s = String.Format("{0}{1}", signature.[i], signature.[i+1])
                yield Convert.ToByte(s, 16)
        |]

        let byteData = Encoding.UTF8.GetBytes(data)
        verifyData(byteData, byteSignature, publicKey)

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

    let getMacAddresses() =
        NetworkInterface.GetAllNetworkInterfaces()
        |> Array.filter(fun ni -> 
            (ni.NetworkInterfaceType = NetworkInterfaceType.Wireless80211
            || ni.NetworkInterfaceType = NetworkInterfaceType.Ethernet)
            && ni.OperationalStatus = OperationalStatus.Up
        )
        |> Array.collect(fun ni -> ni.GetPhysicalAddress().GetAddressBytes())

    let getHardDiskSerials() =
        let bytes =
            DriveInfo.GetDrives()
            |> Array.filter(fun drive -> drive.IsReady)
            |> Array.collect(fun drive -> drive.RootDirectory.CreationTime.ToBinary() |> BitConverter.GetBytes)        
            |> sha256Raw
        Array.sub bytes 0 16 
        
    let encryptKey(data: Byte array) =
        let key = getMacAddresses() |> sha256Raw
        let iv = getHardDiskSerials()
        encrypt(data, key, iv)

    let private decryptKey(data: Byte array) =
        let key = getMacAddresses() |> sha256Raw
        let iv = getHardDiskSerials()
        decrypt(data, key, iv)

    let readPrivateKey(filename: String) =
        let encodedKey = Convert.FromBase64String(File.ReadAllText(filename))
        let effectiveKey = decryptKey(encodedKey)
        Convert.ToBase64String(effectiveKey)

    let encryptExportedKey(password: String, base64KeyData: String) =
        let keyDataBytes = Convert.FromBase64String(base64KeyData)
        let passwordBytes = Encoding.UTF8.GetBytes(password) |> sha256Raw
        let iv = Array.zeroCreate<Byte>(16)
        use provider = new RNGCryptoServiceProvider()        
        provider.GetBytes(iv)
        let encryptedKey = encrypt(keyDataBytes, passwordBytes, iv)
        let encryptedData = Array.concat [iv; encryptedKey]
        Convert.ToBase64String(encryptedData)

    let decryptImportedKey(password: String, base64KeyData: String) =
        let keyDataBytes = Convert.FromBase64String(base64KeyData)
        let passwordBytes = Encoding.UTF8.GetBytes(password) |> sha256Raw
        let iv = Array.sub keyDataBytes 0 16 
        let encryptedKey = keyDataBytes.[16..]
        let decryptedKey = decrypt(encryptedKey, passwordBytes, iv)
        Convert.ToBase64String(decryptedKey)