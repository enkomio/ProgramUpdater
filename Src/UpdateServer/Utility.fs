namespace UpdateServer

open System
open System.Net.NetworkInformation
open System.IO
open ES.Update
open System.Text
open System.Security.Cryptography

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

    let encryptExportedKey(password: String, base64KeyData: String) =
        let keyDataBytes = Convert.FromBase64String(base64KeyData)
        let passwordBytes = Encoding.UTF8.GetBytes(password) |> sha256Raw
        let iv = Array.zeroCreate<Byte>(16)
        use provider = new RNGCryptoServiceProvider()        
        provider.GetBytes(iv)
        let encryptedKey = CryptoUtility.encrypt(keyDataBytes, passwordBytes, iv)
        let encryptedData = Array.concat [iv; encryptedKey]
        Convert.ToBase64String(encryptedData)

    let decryptImportedKey(password: String, base64KeyData: String) =
        let keyDataBytes = Convert.FromBase64String(base64KeyData)
        let passwordBytes = Encoding.UTF8.GetBytes(password) |> sha256Raw
        let iv = Array.sub keyDataBytes 0 16 
        let encryptedKey = keyDataBytes.[16..]
        let decryptedKey = CryptoUtility.decrypt(encryptedKey, passwordBytes, iv)
        Convert.ToBase64String(decryptedKey)

    let readPassword() =
        let password = new StringBuilder()
        let mutable key = Console.ReadKey(true)
        while key.Key <> ConsoleKey.Enter do
            Console.Write("*")
            password.Append(key.KeyChar) |> ignore
            key <- Console.ReadKey(true)
        Console.WriteLine()
        password.ToString()        