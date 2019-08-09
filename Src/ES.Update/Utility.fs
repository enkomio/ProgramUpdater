namespace ES.Update

open System
open System.IO.Compression
open System.IO

module internal Utility =
    let readEntry(zipArchive: ZipArchive, name: String) =
        let entry =
            zipArchive.Entries
            |> Seq.find(fun entry -> entry.FullName.Equals(name, StringComparison.OrdinalIgnoreCase))
        
        use zipStream = entry.Open()
        use memStream = new MemoryStream()
        zipStream.CopyTo(memStream)
        memStream.ToArray()

