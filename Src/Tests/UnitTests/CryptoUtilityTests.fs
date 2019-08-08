namespace UnitTests

open System
open System.Text
open ES.Update

module CryptoUtilityTests =
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
        
    let runAll() =
        ``Sign data and verify``()
        ``Sign data and verify a corrupted data``()
