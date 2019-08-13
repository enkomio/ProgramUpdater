namespace Updater

open System
open System.IO
open Newtonsoft.Json
open System.Reflection

type Settings() =
    member val ServerPublicKey = String.Empty with get, set
    member val UpdateBaseUri = String.Empty with get, set
    member val ProjectName = String.Empty with get, set
    member val DestinationDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) with get, set

    static member Read() =
        let configFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "configuration.json")
        if File.Exists(configFile) then
            (File.ReadAllText >> JsonConvert.DeserializeObject<Settings>)(configFile)
        else
            new Settings()