using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Gets all processes that are currently locking the specified file or any files within the specified directory.
    /// </summary>
    /// <param name="path">The absolute path to the file or directory to check for locks.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The result contains an array of <see cref="Process"/> 
    /// objects representing the processes that have open handles to the specified file or directory.
    /// </returns>
    /// <exception cref="NotSupportedException">The current operating system is not supported.</exception>
    /// <exception cref="IOException">An I/O error occurred while checking for locks.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <remarks>
    /// This method uses different approaches depending on the operating system:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       On Windows, it uses the Sysinternals Handle.exe utility (embedded as a resource) to identify 
    ///       processes with open handles to the specified path.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       On Linux, it uses the 'lsof' command to identify processes with open file descriptors 
    ///       pointing to the specified path.
    ///     </description>
    ///   </item>
    /// </list>
    /// 
    /// The method filters out processes that have exited before returning the results.
    /// Currently only Windows and Linux platforms are supported.
    /// </remarks>
    public static async Task<Process[]> GetProcesses(this AbsolutePath path, CancellationToken cancellationToken = default)
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

    // Path to the embedded Handle.exe resource
    private const string _HandleExeEmbeddedPath = "AbsolutePathHelpers.Assets.handle.exe";

    // SHA256 hash of the embedded Handle.exe for verification
    private const string _HandleExeSHA256 = "84c22579ca09f4fd8a8d9f56a6348c4ad2a92d4722c9f1213dd73c2f68a381e3";

    // Semaphore to ensure only one instance of Handle.exe runs at a time
    private static readonly SemaphoreSlim _handleLocker = new(1);

    /// <summary>
    /// Identifies processes that have open handles to the specified file or directory on Windows.
    /// </summary>
    /// <param name="path">The path to check for locked files.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>An array of processes that have open handles to the specified path.</returns>
    /// <remarks>
    /// This method uses the Sysinternals Handle.exe utility to identify processes with open handles.
    /// The utility is extracted from embedded resources if it doesn't exist in the temp directory
    /// or if the existing file doesn't match the expected hash.
    /// 
    /// The output from Handle.exe is parsed to extract process IDs, which are then used to 
    /// get the corresponding Process objects.
    /// </remarks>
    private static async Task<Process[]> WhoIsLockingWindows(string path, CancellationToken cancellationToken)
    {
        AbsolutePath handlePath = Path.GetTempPath();
        handlePath /= "sysinternals";
        handlePath /= "handle.exe";

        if (!handlePath.FileExists() || await handlePath.GetHashSHA256(cancellationToken: cancellationToken) != _HandleExeSHA256)
        {
            using var stream = Assembly.GetAssembly(typeof(AbsolutePath))!.GetManifestResourceStream(_HandleExeEmbeddedPath)!;
            byte[] bytes = new byte[(int)stream.Length];
            var _ = await stream.ReadAsync(bytes, cancellationToken);
            handlePath.Parent?.CreateDirectory();
            File.WriteAllBytes(handlePath, bytes);
        }

        ConcurrentDictionary<int, Process> processMap = [];

        var startInfo = new ProcessStartInfo
        {
            FileName = handlePath,
            Arguments = $"-accepteula -nobanner -v \"{path}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var handleProcess = new Process { StartInfo = startInfo };

        string? handleResult = null;

        try
        {
            await _handleLocker.WaitAsync(cancellationToken);

            handleProcess.Start();

#if NET7_0_OR_GREATER
            handleResult = await handleProcess.StandardOutput.ReadToEndAsync(cancellationToken);
#else
            handleResult = await handleProcess.StandardOutput.ReadToEndAsync();
#endif
        }
        finally
        {
            _handleLocker.Release();
        }

        var handleResultSplit = handleResult.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        if (handleResultSplit.Length > 1)
        {
            for (int i = 1; i < handleResultSplit.Length; i++)
            {
                var line = handleResultSplit[i].Split(',');
                if (line.Length > 1 && int.TryParse(line[1], out var pid))
                {
                    try
                    {
                        var lockingProcess = Process.GetProcessById(pid);
                        processMap.TryAdd(pid, lockingProcess);
                    }
                    catch { }
                }
            }
        }

        return [.. processMap.Values.Where(i => !i.HasExited)];
    }

    #endregion

    #region Linux Native File Management

    /// <summary>
    /// Identifies processes that have open file descriptors to the specified file or directory on Linux.
    /// </summary>
    /// <param name="path">The path to check for locked files.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>An array of processes that have open file descriptors to the specified path.</returns>
    /// <remarks>
    /// This method uses the 'lsof' command-line utility to identify processes with open file descriptors.
    /// The 'lsof -t' command outputs just the process IDs, which are then used to get the corresponding
    /// Process objects.
    /// 
    /// Note that this implementation requires the 'lsof' utility to be installed on the system.
    /// </remarks>
    private static async Task<Process[]> WhoIsLockingLinux(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return [];
        }

        ConcurrentDictionary<int, Process> processMap = [];

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

#if NET7_0_OR_GREATER
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
#else
        var output = await process.StandardOutput.ReadToEndAsync();
#endif
        await process.WaitForExitAsync(cancellationToken);

        var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (int.TryParse(line, out var pid))
            {
                try
                {
                    var lockingProcess = Process.GetProcessById(pid);
                    processMap.TryAdd(pid, lockingProcess);
                }
                catch { }
            }
        }

        return [.. processMap.Values.Where(i => !i.HasExited)];
    }

    #endregion
}