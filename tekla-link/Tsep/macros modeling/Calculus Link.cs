#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Wpf.Runtime"
#pragma reference "Tekla.Macros.Runtime"
#pragma warning restore 1633 // Unrecognized #pragma directive

using System;
using System.IO;
using System.Diagnostics;
using Tekla.Structures;
using Tekla.Structures.Model.Operations;

namespace UserMacros
{
    public sealed class Macro
    {
        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime)
        {
            string dataDir = string.Empty;
            if (!TeklaStructuresSettings.GetAdvancedOption("XSDATADIR", ref dataDir))
            {
                Operation.DisplayPrompt("Failed to get the extension folder path.");
                return;
            }

            var folderPath = Path.Combine(dataDir, "Environments", "common", "extensions", "CalculusLink"); ;
            var fileName = "CalculusLink.exe";
            var programPath = Path.Combine(folderPath, fileName);

            if (!File.Exists(programPath))
            {
                Operation.DisplayPrompt(fileName + " not found.");
                return;
            }

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = programPath,
                    WorkingDirectory = folderPath,
                }
            };

            if (process.Start())
            {
                Operation.DisplayPrompt(fileName + " started successfully");
            }
            else
            {
                Operation.DisplayPrompt(fileName + " failed to start");
            }

            process.WaitForExit();
        }
    }
}