namespace UpdateServer

open System
open System.Net.NetworkInformation
open System.IO
open System.Security.Cryptography
open ES.Update

module internal Utility =

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
        
    let encryptKey(data: Byte array) =
        let key = getMacAddresses() |> sha256Raw
        let iv = getHardDiskSerials()

        // encrypt
        use aes = new AesManaged(Key = key, IV = iv, Padding = PaddingMode.ISO10126)
        use ms = new MemoryStream()
        use sw = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write)
        sw.Write(data, 0, data.Length) 
        sw.Close()
        ms.ToArray()

    let private decryptKey(data: Byte array) =
        let key = getMacAddresses() |> sha256Raw
        let iv = getHardDiskSerials()
        Utility.decrypt(data, key, iv)

    let createEncryptionKeys() =        
        use key =
            (new ECDiffieHellmanCng(
                KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash,
                HashAlgorithm = CngAlgorithm.Sha256
            )).Key

        (
            key.Export(CngKeyBlobFormat.EccPrivateBlob) |> encryptKey |> Convert.ToBase64String,
            key.Export(CngKeyBlobFormat.EccPublicBlob) |> Convert.ToBase64String
        )

    let readPrivateKey(filename: String) =
        let encodedKey = Convert.FromBase64String(File.ReadAllText(filename))
        let effectiveKey = decryptKey(encodedKey)
        Convert.ToBase64String(effectiveKey)