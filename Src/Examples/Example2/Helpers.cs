using ES.Update.Releaser;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Example2
{
    internal static class Helpers
    {
        private static void CreateFakeReleaseFile(String fileName, Int32 numberOfFiles)
        {
            using (var fileHandle = File.OpenWrite(fileName))
            using (var zipArchive = new ZipArchive(fileHandle, ZipArchiveMode.Create))
            {
                for (var i=0; i<numberOfFiles; i++)
                {
                    var entryName = String.Format("file{0}.txt", i);
                    var entryContent = Encoding.UTF8.GetBytes(String.Format("Content of file numner: {0}", i));
                    var entry = zipArchive.CreateEntry(entryName);
                    using (var entryStream = entry.Open())
                    {
                        entryStream.Write(entryContent, 0, entryContent.Length);
                    }
                }
            }
        }

        public static String CreateEnvironment()
        {
            var workspaceDirectory = Path.Combine(Path.GetTempPath(), "TEST_ProgramUpdater_Example2");
            if (Directory.Exists(workspaceDirectory))
            {
                Directory.Delete(workspaceDirectory, true);
            }
            Directory.CreateDirectory(workspaceDirectory);
            var metadataBuilder = new MetadataBuilder(workspaceDirectory);

            // create some fake zip File
            var currentDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            for (var i = 0; i < 5; i++)
            {
                var fileName = Path.Combine(currentDir, String.Format("MyApplication.v{0}.0.zip", i + 1));
                Helpers.CreateFakeReleaseFile(fileName, 5 + i);
                metadataBuilder.CreateReleaseMetadata(fileName);
                File.Delete(fileName);
            }

            return workspaceDirectory;
        }
    }
}
