namespace UnitTests

open System
open System.Text
open ES.Update
open System.Net.NetworkInformation
open System.IO

module CryptoUtilityTests =

    let private getMacAddresses() =
        NetworkInterface.GetAllNetworkInterfaces()
        |> Array.filter(fun ni -> 
            (ni.NetworkInterfaceType = NetworkInterfaceType.Wireless80211
            || ni.NetworkInterfaceType = NetworkInterfaceType.Ethernet)
            && ni.OperationalStatus = OperationalStatus.Up
        )
        |> Array.collect(fun ni -> ni.GetPhysicalAddress().GetAddressBytes())

    let private getHardDiskSerials() =
        DriveInfo.GetDrives()
        |> Array.filter(fun drive -> drive.IsReady)
        |> Array.collect(fun drive -> drive.RootDirectory.CreationTime.ToBinary() |> BitConverter.GetBytes)
        

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
