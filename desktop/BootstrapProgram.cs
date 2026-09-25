using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace LauncherLeEaglercraft;

internal static class BootstrapProgram
{
    private static void Main()
    {
        try
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "Launcher-Le-Eaglercraft",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(root);

            ExtractPayload(root);

            string exe =
                Path.Combine(
                    root,
                    "Launcher-Le-Eaglercraft.exe");

            if (!File.Exists(exe))
            {
                throw new FileNotFoundException(
                    "The embedded launcher executable was not found.",
                    exe);
            }

            Process process =
                new Process();

            process.StartInfo =
                new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = root,
                    UseShellExecute = false
                };

            process.Start();

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                ex.ToString(),
                "Launcher Le Eaglercraft",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    private static void ExtractPayload(string root)
    {
        Assembly assembly =
            typeof(BootstrapProgram).Assembly;

        string? resource =
            assembly
                .GetManifestResourceNames()
                .FirstOrDefault(
                    x => x.EndsWith(
                        "payload.zip",
                        StringComparison.OrdinalIgnoreCase));

        if (resource == null)
        {
            throw new FileNotFoundException(
                "Embedded launcher payload was not found.");
        }

        using Stream? input =
            assembly.GetManifestResourceStream(
                resource);

        if (input == null)
        {
            throw new FileNotFoundException(
                "Could not open embedded launcher payload.");
        }

        string zip =
            Path.Combine(
                root,
                "payload.zip");

        using (FileStream output =
               File.Create(zip))
        {
            input.CopyTo(output);
        }

        System.IO.Compression.ZipFile
            .ExtractToDirectory(
                zip,
                root,
                true);

        File.Delete(zip);
    }
}
