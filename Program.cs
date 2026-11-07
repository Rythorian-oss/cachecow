using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Velopack;

namespace CacheCow;

class Program
{
    public static long TotalBytesFreed = 0;
    public static int TotalFilesDeleted = 0;

    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheCow", "logs", "sys_.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        using var cts = new CancellationTokenSource();
        Task? dashboardTask = null;

        try
        {
            Log.Information("=== Cache Cow Deep Optimizer Starting ===");
            bool isDryRun = args.Contains("--dry-run");

            dashboardTask = Task.Run(() => RunMemoryDashboard(cts.Token));

            try { VelopackApp.Build().Run(); } catch { /* Ignore if not packaged */ }

            string configJson = await File.ReadAllTextAsync("appsettings.json", cts.Token);
            AppConfig config = JsonSerializer.Deserialize(configJson, AppConfigContext.Default.AppConfig) 
                               ?? throw new InvalidOperationException("Failed to parse configuration.");

            if (config.UpdateSettings.EnableAutoUpdate && !string.IsNullOrWhiteSpace(config.UpdateSettings.ReleaseChannel))
            {
                _ = Task.Run(async () => {
                    try {
                        var mgr = new UpdateManager(config.UpdateSettings.ReleaseChannel);
                        var newVer = await mgr.CheckForUpdatesAsync();
                        if (newVer != null) {
                            Log.Information("Update {Ver} found. Downloading...", newVer.Version);
                            await mgr.DownloadUpdatesAsync(newVer);
                            mgr.ApplyUpdatesAndRestart(newVer);
                        }
                    } catch { Log.Warning("Auto-updater unreachable."); }
                });
            }

            if (config.Optimization.FlushDnsCache && !isDryRun)
            {
                CacheCowCore.FlushDnsCache();
            }

            foreach (var target in config.Targets)
            {
                if (target.IsProtected)
                {
                    Log.Warning("[GUARD] {TargetName} is protected. Bypassing.", target.Name);
                    continue;
                }

                string resolvedPath = CacheCowCore.ResolvePathForCurrentOS(target.WindowsPath, target.MacPath, target.LinuxPath);
                
                if (target.Type == "SystemObject" && resolvedPath == "RecycleBin")
                {
                    if (!isDryRun) CacheCowCore.EmptyTrash();
                    Log.Information("[TRASH] System Recycle Bin / Trash Emptied.");
                    continue;
                }

                if (target.Type == "Directory" && Directory.Exists(resolvedPath))
                {
                    await CacheCowCore.DeepCleanDirectoryAsync(resolvedPath, isDryRun, cts.Token);
                }
            }

            if (config.DuplicateScanner.Enabled)
            {
                using var duplicateEngine = new DuplicateEngine();
                await duplicateEngine.ScanAndCleanAsync(config.DuplicateScanner, isDryRun, cts.Token);
            }

            cts.Cancel();
            await dashboardTask;

            Log.Information("=== OPTIMIZATION COMPLETE ===");
            Log.Information("Total Space Freed: {TotalMB} MB", Math.Round(TotalBytesFreed / 1048576.0, 2));
            Log.Information("Total Files Removed: {Files}", TotalFilesDeleted);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "System exception occurred.");
        }
        finally
        {
            cts.Cancel();
            await Log.CloseAndFlushAsync();
            if (args.Contains("--pause"))
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }

    private static async Task RunMemoryDashboard(CancellationToken token)
    {
        using var process = Process.GetCurrentProcess();
        while (!token.IsCancellationRequested)
        {
            process.Refresh();
            long workingSetMb = process.WorkingSet64 / 1048576;
            long gcMemoryMb = GC.GetTotalMemory(false) / 1048576;
            double freedMb = TotalBytesFreed / 1048576.0;

            try 
            {
                Console.Title = $"Cache Cow Optimizer | WS: {workingSetMb}MB | GC: {gcMemoryMb}MB | Freed: {freedMb:F2}MB | Files: {TotalFilesDeleted}";
            }
            catch { /* Fail silently if terminal does not support Window titles */ }
            
            await Task.Delay(500, token);
        }
    }
}
