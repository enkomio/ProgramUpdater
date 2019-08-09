namespace ES.Update

open System
open System.IO
open System.IO.Compression

type internal Installer(destinationDirectory: String) =
    let verifyIntegrty(hashValue: String, content: Byte array) =
        let computedHashValue = CryptoUtility.sha256(content)
        hashValue.Equals(computedHashValue, StringComparison.OrdinalIgnoreCase)

    let copyFile(filePath: String, content: Byte array) =
        let destinationFile = Path.Combine(destinationDirectory, filePath)
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)) |> ignore
        File.WriteAllBytes(destinationFile, content)

    member this.InstallUpdate(zipArchive: ZipArchive, fileList: String) =
        let mutable integrityFailedOnFile = String.Empty
        
        fileList.Split()
        |> Array.filter(String.IsNullOrWhiteSpace >> not)
        |> Array.map(fun line -> 
            let items = line.Split([|','|])
            (items.[0], String.Join(",", items.[1..]))
        )
        |> Array.forall(fun (hashValue, filePath) ->
            let content = Utility.readEntry(zipArchive, hashValue)
            if verifyIntegrty(hashValue, content) then
                copyFile(filePath, content)
                true
            else
                integrityFailedOnFile <- filePath
                false
        )
        |> fun result ->
            if result then Ok ()
            else Error("Integrity check failed on file: " + integrityFailedOnFile)