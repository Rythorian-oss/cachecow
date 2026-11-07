using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Velopack;

namespace CacheCow;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Initialize Structured Enterprise Logging
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheCow", "logs", "cachecow_.json"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("=== Cache Cow Enterprise Starting ===");
            bool isDryRun = args.Contains("--dry-run");

            // 2. Initialize Velopack Auto-Updater
            VelopackApp.Build().Run();
            
            // 3. Load Professional JSON Configuration
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 4. Background Update Check (Non-blocking)
            if (config.GetValue<bool>("UpdateSettings:EnableAutoUpdate"))
            {
                var updateUrl = config.GetValue<string>("UpdateSettings:ReleaseChannel");
                _ = Task.Run(() => CheckForUpdatesAsync(updateUrl));
            }

            // 5. Execution Loop
            var targets = config.GetSection("Targets").Get<CacheTarget[]>() ?? Array.Empty<CacheTarget>();
            long totalBytesProcessed = 0;

            foreach (var target in targets)
            {
                if (target.IsProtected)
                {
                    Log.Warning("[BLOCKED] {TargetName} is protected data. Skipping.", target.Name);
                    continue;
                }

                string resolvedPath = CacheCowCore.ResolvePathForCurrentOS(target);

                if (target.Type == "SystemObject" && resolvedPath == "RecycleBin")
                {
                    if (!isDryRun)
                    {
                        CacheCowCore.EmptyTrash();
                        Log.Information("[✓] System Trash/Recycle Bin Emptied.");
                    }
                    else
                    {
                        Log.Information("[WhatIf] Would empty System Trash/Recycle Bin.");
                    }
                    continue;
                }

                if (target.Type == "Directory" && Directory.Exists(resolvedPath))
                {
                    var result = CacheCowCore.ProcessDirectory(resolvedPath, isDryRun);
                    totalBytesProcessed += result.BytesProcessed;
                    Log.Information("[✓] Processed {Count} files in {TargetName} ({Bytes} bytes).", result.FilesMoved, target.Name, result.BytesProcessed);
                }
            }

            Log.Information("=== Cache Cow Complete | Total Bytes: {TotalBytes} ===", totalBytesProcessed);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "A fatal exception occurred during Cache Cow execution.");
        }
        finally
        {
            Log.CloseAndFlush();
            if (args.Contains("--pause"))
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }

    private static async Task CheckForUpdatesAsync(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            var mgr = new UpdateManager(url);
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion != null)
            {
                Log.Information("Downloading update: {Version}", newVersion.Version);
                await mgr.DownloadUpdatesAsync(newVersion);
                mgr.ApplyUpdatesAndRestart(newVersion);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for updates.");
        }
    }
}

public class CacheTarget
{
    public string Name { get; set; } = string.Empty;
    public string WindowsPath { get; set; } = string.Empty;
    public string MacPath { get; set; } = string.Empty;
    public string LinuxPath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsProtected { get; set; }
}