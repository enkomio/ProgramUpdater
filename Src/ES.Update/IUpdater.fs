namespace ES.Update

open System
open ES.Update.Entities

type IUpdater =
    interface
        /// This function returns true if there is a more updated version
        abstract CheckForUpdates: unit -> Boolean

        /// This function returns an update bundle for the current version 
        abstract GetUpdate: unit -> UpdateBundle
    end

