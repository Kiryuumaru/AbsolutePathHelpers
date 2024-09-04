using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Gets a list of processes that are currently locking the specified file or any file within the specified directory.
    /// </summary>
    /// <param name="path">The path to the file or directory to check for locked files.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation that returns a list of processes locking the file(s).</returns>
    public static async Task<Process[]> GetProcesses(this AbsolutePath path, CancellationToken cancellationToken = default)
    {
        ConcurrentDictionary<int, Process> processMap = [];
        if (path.FileExists())
        {
            foreach (var proc in await WhoIsLocking(path, cancellationToken))
            {
                processMap.TryAdd(proc.Id, proc);
            }
        }
        else if (path.DirectoryExists())
        {
            var fileMap = GetFileMap(path);

            List<AbsolutePath> pathsToCheck = [];
            pathsToCheck.AddRange(fileMap.Files);
            pathsToCheck.AddRange(fileMap.Folders);

            List<Task> tasks = new List<Task>();
            foreach (var pathToCheck in pathsToCheck)
            {
                tasks.Add(Task.Run(async () =>
                {
                    foreach (var proc in await WhoIsLocking(pathToCheck, cancellationToken))
                    {
                        processMap.TryAdd(proc.Id, proc);
                    }
                }));
            }
            await Task.WhenAll(tasks);
        }
        return [.. processMap.Values];
    }

    private static async Task<List<Process>> WhoIsLocking(string path, CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await WhoIsLockingWindows(path, cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await WhoIsLockingLinux(path, cancellationToken);
        }

        throw new NotSupportedException(RuntimeInformation.OSDescription);
    }

    #region Windows Native File Management

    /// <summary>
    /// Embarrasing way to check file and folder locks
    /// </summary>

    // https://learn.microsoft.com/en-us/sysinternals/downloads/handle
    private const string _HandleExeEmbeddedPath = "AbsolutePathHelpers.Assets.handle.exe";
    private const string _HandleExeSHA256 = "84c22579ca09f4fd8a8d9f56a6348c4ad2a92d4722c9f1213dd73c2f68a381e3";

    private static async Task<List<Process>> WhoIsLockingWindows(string path, CancellationToken cancellationToken)
    {
        AbsolutePath handlePath = Path.GetTempPath();
        handlePath /= "sysinternals";
        handlePath /= "handle.exe";

        if (!handlePath.FileExists() || await handlePath.GetHashSHA256() != _HandleExeSHA256)
        {
            using var stream = Assembly.GetAssembly(typeof(AbsolutePath))!.GetManifestResourceStream(_HandleExeEmbeddedPath)!;
            byte[] bytes = new byte[(int)stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            handlePath.Parent?.CreateDirectory();
            File.WriteAllBytes(handlePath, bytes);
        }

        List<Process> processes = [];

        var startInfo = new ProcessStartInfo
        {
            FileName = handlePath,
            Arguments = $"-accepteula -nobanner -v \"{path}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var handleResult = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var handleResultSplit = handleResult.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        if (handleResultSplit.Length > 1)
        {
            for (int i = 1; i < handleResultSplit.Length; i++)
            {
                var line = handleResultSplit[i].Split(',');
                if (line.Length > 1 && int.TryParse(line[1], out var processId))
                {
                    try
                    {
                        processes.Add(Process.GetProcessById(processId));
                    }
                    catch { }
                }
            }
        }

        return processes;
    }

    #endregion

    #region Linux Native File Management

    /// <summary>
    /// Not sure if works, not tested
    /// </summary>

    private static async Task<List<Process>> WhoIsLockingLinux(string path, CancellationToken cancellationToken)
    {
        var processes = new List<Process>();

        var startInfo = new ProcessStartInfo
        {
            FileName = "lsof",
            Arguments = $"-t \"{path}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (int.TryParse(line, out var pid))
            {
                try
                {
                    processes.Add(Process.GetProcessById(pid));
                }
                catch { }
            }
        }

        return processes;
    }

    #endregion
}
