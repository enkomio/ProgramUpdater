namespace KeysGenerator

open System
open System.Security.Cryptography

module Program =
    [<EntryPoint>]
    let main argv =
        use key =
            (new ECDiffieHellmanCng(
                KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash,
                HashAlgorithm = CngAlgorithm.Sha256
            )).Key

        let privateBytes = key.Export(CngKeyBlobFormat.EccPrivateBlob)
        let publicBytes = key.Export(CngKeyBlobFormat.EccPublicBlob)
        
        // data to print
        Console.WriteLine("-=[ Secret keys ]=-")
        Console.WriteLine()
        Console.WriteLine("Private key: {0}", Convert.ToBase64String(privateBytes))
        Console.WriteLine()
        Console.WriteLine("Public key: {0}", Convert.ToBase64String(publicBytes))
        
        0
