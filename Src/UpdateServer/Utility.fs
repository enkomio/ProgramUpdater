namespace UpdateServer

open System
open System.Net.NetworkInformation
open System.IO
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
        let bytes =
            DriveInfo.GetDrives()
            |> Array.filter(fun drive -> drive.IsReady)
            |> Array.collect(fun drive -> drive.RootDirectory.CreationTime.ToBinary() |> BitConverter.GetBytes)        
            |> sha256Raw
        Array.sub bytes 0 16 
        
    let encryptKey(data: Byte array) =
        let key = getMacAddresses() |> sha256Raw
        let iv = getHardDiskSerials()
        CryptoUtility.encrypt(data, key, iv)

    let private decryptKey(data: Byte array) =
        let key = getMacAddresses() |> sha256Raw
        let iv = getHardDiskSerials()
        CryptoUtility.decrypt(data, key, iv)

    let readPrivateKey(filename: String) =
        let encodedKey = Convert.FromBase64String(File.ReadAllText(filename))
        let effectiveKey = decryptKey(encodedKey)
        Convert.ToBase64String(effectiveKey)