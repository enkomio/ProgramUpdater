namespace UnitTests

module Program =

    [<EntryPoint>]
    let main argv = 
        CryptoUtilityTests.runAll()  
        BackendTests.runAll()
        0
