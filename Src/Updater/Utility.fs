namespace Updater

open System
open System.Reflection
open System.IO

module Utility =
    let readCurrentVersion() =
        let versionFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "current-version.txt")
        let currentVersion = ref(new Version())
        if File.Exists(versionFile) then
            Version.TryParse(File.ReadAllText(versionFile), currentVersion) |> ignore
        !currentVersion

