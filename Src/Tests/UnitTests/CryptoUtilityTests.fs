namespace UnitTests

open System
open System.Reflection
open System.Text
open ES.Update
open System.Net.NetworkInformation
open System.IO

module CryptoUtilityTests =

    let private getMacAddresses() =
        let utility = typeof<UpdateServer.Settings>.Assembly.ManifestModule.GetType("UpdateServer.Utility")
        let getMacAddressesMethod = utility.GetMethod("getMacAddresses", BindingFlags.Static ||| BindingFlags.NonPublic)
        getMacAddressesMethod.Invoke(null, Array.empty) :?> Byte array

    let private getHardDiskSerials() =
        let utility = typeof<UpdateServer.Settings>.Assembly.ManifestModule.GetType("UpdateServer.Utility")
        let getMacAddressesMethod = utility.GetMethod("getHardDiskSerials", BindingFlags.Static ||| BindingFlags.NonPublic)
        getMacAddressesMethod.Invoke(null, Array.empty) :?> Byte array

    let private ``Sign data and verify``() =
        // generate signature
        let data = Encoding.UTF8.GetBytes("This buffer contains data that must be signed")
        let (publicKey, privateKey) = CryptoUtility.generateKeys()
        let signature = CryptoUtility.sign(data, privateKey)
        
        // verify signature
        let testResult = CryptoUtility.verifySignature(data, signature, publicKey)
        assert testResult

    let private ``Sign data and verify a corrupted data``() =
        // generate signature
        let data = Encoding.UTF8.GetBytes("This buffer contains data that must be signed")
        let (publicKey, privateKey) = CryptoUtility.generateKeys()
        let signature = CryptoUtility.sign(data, privateKey)
        
        // verify signature
        data.[2] <- data.[2] ^^^ 0xA1uy
        let testResult = CryptoUtility.verifySignature(data, signature, publicKey)
        assert(not testResult)

    let private ``Sign data and verify a corrupted signature``() =
        // generate signature
        let data = Encoding.UTF8.GetBytes("This buffer contains data that must be signed")
        let (publicKey, privateKey) = CryptoUtility.generateKeys()
        let signature = CryptoUtility.sign(data, privateKey)
        
        // verify signature
        signature.[2] <- signature.[2] ^^^ 0xA1uy
        let testResult = CryptoUtility.verifySignature(data, signature, publicKey)
        assert(not testResult)

    let private ``Encrypt and decrypt data``() =
        let text = "This is the text that must be used in order to verify if the encryption and decryption work correctly"
        let key = getMacAddresses() |> CryptoUtility.sha256Raw
        let iv = getHardDiskSerials()
        
        // encrypt and decrypt
        let textBytes = Encoding.UTF8.GetBytes(text)
        let encryptedText = CryptoUtility.encrypt(textBytes, key, iv)
        let decryptedText = CryptoUtility.decrypt(encryptedText, key, iv) |> Encoding.UTF8.GetString
        
        // check
        let testResult = decryptedText.Equals(text, StringComparison.Ordinal)
        assert testResult
        
    let runAll() =
        ``Sign data and verify``()
        ``Sign data and verify a corrupted data``()
        ``Sign data and verify a corrupted signature``()
        ``Encrypt and decrypt data``()
