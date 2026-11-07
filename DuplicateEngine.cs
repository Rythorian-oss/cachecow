using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace CacheCow;

public class DuplicateEngine : IDisposable
{
    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

    public async Task ScanAndCleanAsync(DuplicateConfig config, bool isWhatIf, CancellationToken token)
    {
        Log.Information("=== Initializing Duplicate Scanner ===");
        
        var filesByExtAndSize = new ConcurrentDictionary<string, List<string>>();
        var extensionsToScan = new HashSet<string>(config.TargetExtensions.Select(e => e.ToLowerInvariant()));

        // Step 1: Rapid Metadata Scan (Groups by Exact Size and Extension)
        foreach (var dir in config.TargetDirectories)
        {
            string resolved = CacheCowCore.ResolvePathForCurrentOS(dir, dir, dir);
            if (!Directory.Exists(resolved)) continue;

            var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
            foreach (var file in Directory.EnumerateFiles(resolved, "*", options))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!extensionsToScan.Contains(ext)) continue;

                try
                {
                    long size = new FileInfo(file).Length;
                    if (size == 0) continue; 
                    
                    string key = $"{ext}_{size}";
                    filesByExtAndSize.AddOrUpdate(key, 
                        _ => new List<string> { file }, 
                        (_, list) => { lock (list) list.Add(file); return list; });
                }
                catch { /* Skip inaccessible files */ }
            }
        }

        // Filter out unique items - only process groups with >1 file sharing the exact same size & extension
        var potentialDuplicates = filesByExtAndSize.Values.Where(list => list.Count > 1).ToList();
        
        if (!potentialDuplicates.Any())
        {
            Log.Information("[DUPLICATES] No matching duplicates found.");
            return;
        }

        // Step 2: Zero-Allocation Hashing & Deletion
        await Parallel.ForEachAsync(potentialDuplicates, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token }, async (group, ct) =>
        {
            var hashedFiles = new Dictionary<ulong, string>();

            foreach (var file in group)
            {
                ulong hash = await ComputeHashZeroAllocationAsync(file, ct);
                if (hash == 0) continue; // Skip read errors

                if (hashedFiles.TryGetValue(hash, out string existingFile))
                {
                    // Exact duplicate detected
                    try
                    {
                        long size = new FileInfo(file).Length;
                        if (!isWhatIf) File.Delete(file);
                        
                        Interlocked.Add(ref Program.TotalBytesFreed, size);
                        Interlocked.Increment(ref Program.TotalFilesDeleted);
                        Log.Information("[DUPLICATE DELETED] {File} (Matched {Existing})", file, existingFile);
                    }
                    catch { /* Skip on access error */ }
                }
                else
                {
                    hashedFiles[hash] = file;
                }
            }
        });
    }

    private async ValueTask<ulong> ComputeHashZeroAllocationAsync(string filePath, CancellationToken ct)
    {
        byte[] buffer = _pool.Rent(1024 * 1024); // 1MB rented chunk (Zero GC allocation)
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var xxHash = new XxHash64();
            
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                xxHash.Append(new ReadOnlySpan<byte>(buffer, 0, bytesRead));
            }
            
            return xxHash.GetCurrentHashAsUInt64();
        }
        catch
        {
            return 0; // Return 0 on permission denial or locked files
        }
        finally
        {
            _pool.Return(buffer); // Prevent memory leaks
        }
    }

    public void Dispose()
    {
        // Suppress finalization as ArrayPool handles its own memory lifecycle natively
        GC.SuppressFinalize(this);
    }
}
