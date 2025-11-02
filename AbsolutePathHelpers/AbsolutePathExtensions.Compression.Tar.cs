using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.IO;
using System.IO.Compression;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    private static async Task CompressTar(AbsolutePath baseDirectory, AbsolutePath archiveFile, IReadOnlyCollection<AbsolutePath> files, FileMode fileMode, Func<Stream, Stream> outputStreamFactory, CancellationToken cancellationToken)
    {
        archiveFile.Parent?.CreateDirectory();

        await using var fileStream = File.Open(archiveFile, fileMode, FileAccess.ReadWrite);
        await using var outputStream = outputStreamFactory(fileStream);
        using var tarOutput = new TarOutputStream(outputStream, TarBuffer.DefaultBlockFactor, nameEncoding: null);
        tarOutput.IsStreamOwner = false;

        void AddFile(AbsolutePath file)
        {
            FileInfo fileInfo = new(file);
            var entryName = UnixRelativeName(file, baseDirectory);

            if (TryWriteSymbolicLinkEntry(fileInfo, file, entryName))
            {
                return;
            }

            var entry = TarEntry.CreateEntryFromFile(file);
            entry.Name = entryName;

            tarOutput.PutNextEntry(entry);
            using (var input = File.OpenRead(file))
            {
                input.CopyTo(tarOutput);
            }
            tarOutput.CloseEntry();
        }

        bool TryWriteSymbolicLinkEntry(FileInfo fileInfo, AbsolutePath originalPath, string entryName)
        {
            if (string.IsNullOrEmpty(fileInfo.LinkTarget))
            {
                return false;
            }

            var entry = TarEntry.CreateTarEntry(entryName);
            entry.TarHeader.TypeFlag = TarHeader.LF_SYMLINK;
            entry.TarHeader.LinkName = NormalizeTarLinkTargetForArchive(fileInfo, baseDirectory, originalPath);
            entry.ModTime = fileInfo.LastWriteTime;
            entry.Size = 0;

            tarOutput.PutNextEntry(entry);
            tarOutput.CloseEntry();
            return true;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddFile(file);
        }
    }

    private static async Task UncompressTar(AbsolutePath archiveFile, AbsolutePath directory, Func<Stream, Stream> inputStreamFactory, CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(archiveFile);
        await using var inputStream = inputStreamFactory(fileStream);
        using var tarInput = new TarInputStream(inputStream, TarBuffer.DefaultBlockFactor, nameEncoding: null);
        tarInput.IsStreamOwner = false;

        directory.CreateDirectory();

        var rootPath = Path.GetFullPath(directory.ToString());

        TarEntry? entry;
        while ((entry = tarInput.GetNextEntry()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativeName = NormalizeTarEntryName(entry.Name);
            if (relativeName.Length == 0 && !entry.IsDirectory)
            {
                continue;
            }

            var platformRelative = relativeName.Replace('/', Path.DirectorySeparatorChar);
            var destinationFullPath = Path.GetFullPath(Path.Combine(rootPath, platformRelative));

            if (!destinationFullPath.StartsWith(rootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Entry '{entry.Name}' is outside of the extraction directory.");
            }

            var destinationPath = AbsolutePath.Create(destinationFullPath);

            if (entry.IsDirectory)
            {
                destinationPath.CreateDirectory();
                continue;
            }

            switch (entry.TarHeader.TypeFlag)
            {
                case TarHeader.LF_SYMLINK:
                    HandleSymbolicLinkEntry(destinationPath, rootPath, entry);
                    break;
                case TarHeader.LF_LINK:
                    HandleHardLinkEntry(destinationPath, rootPath, entry);
                    break;
                default:
                    destinationPath.Parent?.CreateDirectory();
                    using (var output = File.Create(destinationPath))
                    {
                        tarInput.CopyEntryContents(output);
                    }
                    break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string NormalizeTarLinkTargetForArchive(FileInfo fileInfo, AbsolutePath baseDirectory, AbsolutePath linkPath)
    {
        var linkTarget = fileInfo.LinkTarget ?? string.Empty;

        if (linkTarget.StartsWith("\\??\\", StringComparison.Ordinal))
        {
            linkTarget = linkTarget[4..];
        }

        var linkParent = linkPath.Parent?.Path ?? baseDirectory.Path;

        if (Path.IsPathRooted(linkTarget))
        {
            var fullTarget = Path.GetFullPath(linkTarget);
            try
            {
                var relativeToParent = Path.GetRelativePath(linkParent, fullTarget);
                if (!relativeToParent.StartsWith("..", StringComparison.Ordinal))
                {
                    linkTarget = relativeToParent;
                }
                else
                {
                    var relativeToBase = Path.GetRelativePath(baseDirectory.Path, fullTarget);
                    if (!relativeToBase.StartsWith("..", StringComparison.Ordinal))
                    {
                        linkTarget = relativeToBase;
                    }
                    else
                    {
                        linkTarget = fullTarget;
                    }
                }
            }
            catch
            {
                linkTarget = fullTarget;
            }
        }

        linkTarget = linkTarget.Replace('\\', '/');

        if (linkTarget.Length == 0)
        {
            linkTarget = fileInfo.Name;
        }

        return linkTarget;
    }

    private static string NormalizeTarEntryName(string? entryName)
    {
        if (string.IsNullOrEmpty(entryName))
        {
            return string.Empty;
        }

        var name = entryName.Replace('\\', '/');

        while (name.StartsWith("./", StringComparison.Ordinal))
        {
            name = name[2..];
        }

        return name.TrimStart('/');
    }

    private static void HandleSymbolicLinkEntry(AbsolutePath destinationPath, string rootPath, TarEntry entry)
    {
        var linkName = entry.TarHeader.LinkName ?? string.Empty;
        if (linkName.Length == 0)
        {
            return;
        }

        var linkParent = destinationPath.Parent?.Path ?? rootPath;
        var (platformTarget, absoluteTarget, isDirectory) = ResolveTarLinkTargets(linkName, linkParent, rootPath);

        CreateSymbolicLinkOrFallback(destinationPath, platformTarget, absoluteTarget, isDirectory);
    }

    private static void HandleHardLinkEntry(AbsolutePath destinationPath, string rootPath, TarEntry entry)
    {
        var linkName = entry.TarHeader.LinkName ?? string.Empty;
        if (linkName.Length == 0)
        {
            return;
        }

        var normalized = NormalizeTarEntryName(linkName);
        if (normalized.Length == 0)
        {
            return;
        }

        var platformRelative = normalized.Replace('/', Path.DirectorySeparatorChar);
        var targetFullPath = Path.GetFullPath(Path.Combine(rootPath, platformRelative));

        if (!targetFullPath.StartsWith(rootPath, StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }

        destinationPath.Parent?.CreateDirectory();

        if (File.Exists(targetFullPath))
        {
            File.Copy(targetFullPath, destinationPath, true);
        }
        else if (Directory.Exists(targetFullPath))
        {
            Directory.CreateDirectory(destinationPath);
        }
    }

    private static (string platformTarget, string absoluteTarget, bool isDirectory) ResolveTarLinkTargets(string rawLinkName, string linkParent, string rootPath)
    {
        var linkName = rawLinkName.Replace('\\', '/');

        var indicatesDirectory = linkName.EndsWith("/", StringComparison.Ordinal);
        linkName = linkName.TrimEnd('/');

        string platformTarget;
        string absoluteTarget;

        if (linkName.Length == 0)
        {
            platformTarget = ".";
            absoluteTarget = linkParent;
        }
        else if (linkName.StartsWith("/", StringComparison.Ordinal))
        {
            var trimmed = linkName.TrimStart('/');
            var platformRelative = trimmed.Replace('/', Path.DirectorySeparatorChar);
            absoluteTarget = Path.GetFullPath(Path.Combine(rootPath, platformRelative));
            platformTarget = Path.GetRelativePath(linkParent, absoluteTarget);
        }
        else if (Path.IsPathRooted(linkName))
        {
            absoluteTarget = Path.GetFullPath(linkName);
            platformTarget = absoluteTarget;
        }
        else
        {
            platformTarget = linkName.Replace('/', Path.DirectorySeparatorChar);
            absoluteTarget = Path.GetFullPath(Path.Combine(linkParent, platformTarget));
        }

        bool isDirectory = indicatesDirectory || Directory.Exists(absoluteTarget);

        return (platformTarget, absoluteTarget, isDirectory);
    }

    private static void CreateSymbolicLinkOrFallback(AbsolutePath linkPath, string platformTarget, string absoluteTarget, bool isDirectory)
    {
        linkPath.Parent?.CreateDirectory();

        try
        {
            if (isDirectory)
            {
                Directory.CreateSymbolicLink(linkPath, platformTarget);
            }
            else
            {
                File.CreateSymbolicLink(linkPath, platformTarget);
            }
        }
        catch (Exception ex) when (IsSymbolicLinkUnsupported(ex))
        {
            if (!isDirectory && File.Exists(absoluteTarget))
            {
                File.Copy(absoluteTarget, linkPath, true);
                return;
            }

            if (Directory.Exists(absoluteTarget))
            {
                Directory.CreateDirectory(linkPath);
                return;
            }

            if (!isDirectory)
            {
                linkPath.Parent?.CreateDirectory();
                using var _ = File.Create(linkPath);
            }
        }
    }

    private static bool IsSymbolicLinkUnsupported(Exception ex)
    {
        if (ex is UnauthorizedAccessException or NotSupportedException or PlatformNotSupportedException)
        {
            return true;
        }

        if (ex is IOException ioException)
        {
            return ioException.HResult is unchecked((int)0x80070005) or unchecked((int)0x80070057);
        }

        return ex.Message.Contains("privilege", StringComparison.OrdinalIgnoreCase);
    }
}