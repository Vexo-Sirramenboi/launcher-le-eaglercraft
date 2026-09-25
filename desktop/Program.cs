using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace LauncherLeEaglercraft;

internal static class Program
{
    private static string? runtimeRoot;
    private static HttpListener? server;

    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();

            runtimeRoot = PrepareRuntime();

            int port = FindFreePort();

            StartLocalServer(runtimeRoot, port);

            CefSettings settings = new CefSettings();

            settings.CachePath =
                Path.Combine(runtimeRoot, "cef-cache");

            settings.LogSeverity = LogSeverity.Disable;

            settings.UserAgent =
                "Launcher-Le-Eaglercraft/1.0 Chromium";

            settings.CefCommandLineArgs["autoplay-policy"] =
                "no-user-gesture-required";

            settings.CefCommandLineArgs["disable-features"] =
                "BlockInsecurePrivateNetworkRequests";

            settings.CefCommandLineArgs["enable-webgl"] = "1";

            settings.CefCommandLineArgs["enable-gpu-rasterization"] = "1";

            if (!Cef.Initialize(
                    settings,
                    performDependencyCheck: true,
                    browserProcessHandler: null))
            {
                throw new Exception(
                    "CEF failed to initialize.");
            }

            using MainForm form =
                new MainForm(port);

            Application.Run(form);

            Cef.Shutdown();

            StopServer();

            TryDeleteRuntime();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Launcher Le Eaglercraft",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string PrepareRuntime()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "Launcher-Le-Eaglercraft",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        ExtractEmbeddedPayload(root);

        return root;
    }

    private static void ExtractEmbeddedPayload(string root)
    {
        var assembly =
            typeof(Program).Assembly;

        string? resourceName =
            assembly
                .GetManifestResourceNames()
                .FirstOrDefault(
                    x => x.EndsWith(
                        "LauncherLeEaglercraft.payload.zip",
                        StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            throw new FileNotFoundException(
                "Embedded launcher payload was not found.");
        }

        using Stream? stream =
            assembly.GetManifestResourceStream(
                resourceName);

        if (stream == null)
        {
            throw new FileNotFoundException(
                "Could not open embedded launcher payload.");
        }

        string zipPath =
            Path.Combine(
                root,
                "payload.zip");

        using (FileStream output =
               File.Create(zipPath))
        {
            stream.CopyTo(output);
        }

        ZipFile.ExtractToDirectory(
            zipPath,
            root,
            overwriteFiles: true);

        File.Delete(zipPath);
    }

    private static int FindFreePort()
    {
        using TcpListener listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint)
            .Port;
    }

    private static void StartLocalServer(
        string root,
        int port)
    {
        server = new HttpListener();

        server.Prefixes.Add(
            $"http://127.0.0.1:{port}/");

        server.Start();

        _ = Task.Run(async () =>
        {
            while (server != null &&
                   server.IsListening)
            {
                try
                {
                    HttpListenerContext context =
                        await server.GetContextAsync();

                    _ = Task.Run(
                        () => HandleRequest(
                            context,
                            root));
                }
                catch
                {
                    break;
                }
            }
        });
    }

    private static void HandleRequest(
        HttpListenerContext context,
        string root)
    {
        try
        {
            string path =
                Uri.UnescapeDataString(
                    context.Request.Url?.AbsolutePath
                    ?? "/");

            if (path == "/")
            {
                path = "/index.html";
            }

            path = path.TrimStart('/');

            string fullPath =
                Path.GetFullPath(
                    Path.Combine(
                        root,
                        path.Replace(
                            '/',
                            Path.DirectorySeparatorChar)));

            string rootFull =
                Path.GetFullPath(root);

            if (!fullPath.StartsWith(
                    rootFull,
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            if (!File.Exists(fullPath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            byte[] data =
                File.ReadAllBytes(fullPath);

            context.Response.StatusCode = 200;

            context.Response.ContentType =
                GetContentType(fullPath);

            context.Response.ContentLength64 =
                data.Length;

            context.Response.Headers[
                "Access-Control-Allow-Origin"] = "*";

            context.Response.Headers[
                "Cross-Origin-Resource-Policy"] =
                "cross-origin";

            context.Response.Headers[
                "Cross-Origin-Opener-Policy"] =
                "same-origin";

            context.Response.Headers[
                "Cross-Origin-Embedder-Policy"] =
                "require-corp";

            context.Response.OutputStream.Write(
                data,
                0,
                data.Length);

            context.Response.OutputStream.Close();
        }
        catch
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // Ignore closed connections.
            }
        }
    }

    private static string GetContentType(
        string path)
    {
        string extension =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return extension switch
        {
            ".html" => "text/html; charset=utf-8",
            ".htm" => "text/html; charset=utf-8",
            ".js" => "application/javascript",
            ".mjs" => "application/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".wasm" => "application/wasm",
            ".epk" => "application/octet-stream",
            ".epw" => "application/octet-stream",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".txt" => "text/plain; charset=utf-8",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    private static void StopServer()
    {
        try
        {
            server?.Stop();
            server?.Close();
            server = null;
        }
        catch
        {
            // Ignore shutdown errors.
        }
    }

    private static void TryDeleteRuntime()
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot))
            return;

        try
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(
                    runtimeRoot,
                    recursive: true);
            }
        }
        catch
        {
            // CEF may still have a locked file.
            // Windows will clean the temp directory later.
        }
    }

    private sealed class MainForm : Form
    {
        private readonly ChromiumWebBrowser browser;

        public MainForm(int port)
        {
            Text =
                "Launcher Le Eaglercraft";

            Width = 1280;
            Height = 800;

            MinimumSize =
                new System.Drawing.Size(
                    900,
                    600);

            StartPosition =
                FormStartPosition.CenterScreen;

            browser =
                new ChromiumWebBrowser(
                    $"http://127.0.0.1:{port}/");

            browser.Dock =
                DockStyle.Fill;

            Controls.Add(browser);

            FormClosing +=
                MainForm_FormClosing;
        }

        private void MainForm_FormClosing(
            object? sender,
            FormClosingEventArgs e)
        {
            try
            {
                browser.StopLoading();
                browser.Dispose();
            }
            catch
            {
                // Ignore browser shutdown errors.
            }
        }
    }
}
