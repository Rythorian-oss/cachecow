using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace CacheCow;

public class CacheCowResult 
{
    public long BytesProcessed = 0;
    public int FilesMoved = 0;
    public int Errors = 0;
}

public static class CacheCowCore 
{
    public static string ResolvePathForCurrentOS(CacheTarget target)
    {
        string rawPath = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            rawPath = target.WindowsPath;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            rawPath = target.MacPath;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            rawPath = target.LinuxPath;

        if (string.IsNullOrEmpty(rawPath)) return string.Empty;

        // Resolve cross-platform home directory mapping
        if (rawPath.StartsWith("~/"))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            rawPath = Path.Combine(home, rawPath.Substring(2));
        }

        return Environment.ExpandEnvironmentVariables(rawPath);
    }

    public static void EmptyTrash()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows native implementation
            SHEmptyRecycleBin(IntPtr.Zero, null, 0x00000001 | 0x00000002 | 0x00000004);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS native implementation
            string trashPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
            ProcessDirectory(trashPath, isWhatIf: false);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux native implementation (FreeDesktop standard)
            string trashPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash", "files");
            ProcessDirectory(trashPath, isWhatIf: false);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    public static CacheCowResult ProcessDirectory(string sourcePath, bool isWhatIf) 
    {
        CacheCowResult result = new CacheCowResult();
        if (!Directory.Exists(sourcePath)) return result;

        try 
        {
            var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
            var files = Directory.EnumerateFiles(sourcePath, "*", enumOptions);
            
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file => {
                try {
                    FileInfo info = new FileInfo(file);
                    Interlocked.Add(ref result.BytesProcessed, info.Length);
                    
                    if (!isWhatIf) 
                    {
                        if ((info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) 
                            info.Attributes = FileAttributes.Normal;
                        
                        File.Delete(file);
                        Interlocked.Increment(ref result.FilesMoved);
                    }
                } 
                catch { Interlocked.Increment(ref result.Errors); }
            });
        } 
        catch { Interlocked.Increment(ref result.Errors); }
        
        return result;
    }
}