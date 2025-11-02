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
    /// <summary>
    /// Compresses the specified directory to an archive file with format determined by the archive file extension.
    /// </summary>
    /// <param name="directory">The directory containing files to compress.</param>
    /// <param name="archiveFile">The destination archive file path (.zip, .tar.gz, .tgz, .tar.bz2, .tbz2, or .tbz).</param>
    /// <param name="filter">Optional filter function to select which files to include in the archive.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous compression operation.</returns>
    /// <exception cref="Exception">Thrown if the archive file has an unsupported extension.</exception>
    public static Task CompressTo(this AbsolutePath directory, AbsolutePath archiveFile, Func<AbsolutePath, bool>? filter = null, CancellationToken cancellationToken = default)
    {
        var fileName = archiveFile.Name.ToLowerInvariant();

        if (fileName.EndsWith(".zip"))
            return directory.ZipTo(archiveFile, filter, cancellationToken: cancellationToken);
        if (fileName.EndsWith(".tar.gz") || fileName.EndsWith(".tgz"))
            return directory.TarGZipTo(archiveFile, filter, cancellationToken: cancellationToken);
        if (fileName.EndsWith(".tar.bz2") || fileName.EndsWith(".tbz2") || fileName.EndsWith(".tbz"))
            return directory.TarBZip2To(archiveFile, filter, cancellationToken: cancellationToken);
        if (fileName.EndsWith(".7z"))
            throw new NotSupportedException("Creating 7z archives is not supported. Use .zip or .tar.gz instead.");

        throw new Exception("Unknown archive extension for archive '" + Path.GetFileName(archiveFile) + "'");
    }

    /// <summary>
    /// Extracts the contents of an archive file to the specified directory, with format determined by the archive file extension.
    /// </summary>
    /// <param name="archiveFile">The archive file to extract (.zip, .tar.gz, .tgz, .tar.bz2, .tbz2, .tbz, or .7z).</param>
    /// <param name="directory">The destination directory where contents will be extracted.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous extraction operation.</returns>
    /// <exception cref="Exception">Thrown if the archive file has an unsupported extension.</exception>
    public static Task UncompressTo(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        var fileName = archiveFile.Name.ToLowerInvariant();

        if (fileName.EndsWith(".zip"))
            return archiveFile.UnZipTo(directory, cancellationToken: cancellationToken);
        if (fileName.EndsWith(".tar.gz") || fileName.EndsWith(".tgz"))
            return archiveFile.UnTarGZipTo(directory, cancellationToken: cancellationToken);
        if (fileName.EndsWith(".tar.bz2") || fileName.EndsWith(".tbz2") || fileName.EndsWith(".tbz"))
            return archiveFile.UnTarBZip2To(directory, cancellationToken: cancellationToken);
        if (fileName.EndsWith(".7z"))
            return archiveFile.UnSevenZipTo(directory, cancellationToken: cancellationToken);

        throw new Exception("Unknown archive extension for archive '" + Path.GetFileName(archiveFile) + "'");
    }

    /// <summary>
    /// Compresses the specified directory to a ZIP archive file.
    /// </summary>
    /// <param name="directory">The directory containing files to compress.</param>
    /// <param name="archiveFile">The destination ZIP archive file path.</param>
    /// <param name="filter">Optional filter function to select which files to include in the archive.</param>
    /// <param name="compressionLevel">The compression level to use (Fastest, Optimal, or NoCompression).</param>
    /// <param name="fileMode">The file mode to use when creating the archive file.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous ZIP compression operation.</returns>
    public static async Task ZipTo(this AbsolutePath directory, AbsolutePath archiveFile, Func<AbsolutePath, bool>? filter = null, CompressionLevel compressionLevel = CompressionLevel.Optimal, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        archiveFile.Parent?.CreateDirectory();

        filter ??= _ => true;

        List<AbsolutePath> list = [.. directory.GetFiles("*", int.MaxValue).Where(filter)];
        ZipArchive zipArchive;
        await using FileStream stream = File.Open(archiveFile, fileMode, FileAccess.ReadWrite);
        zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
        try
        {
            foreach (var item in list)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddFile(item);
            }
        }
        finally
        {
            if (zipArchive != null)
            {
                ((IDisposable)zipArchive).Dispose();
            }
        }

        void AddFile(AbsolutePath file)
        {
            zipArchive.CreateEntryFromFile(file, UnixRelativeName(file, directory), compressionLevel);
        }
    }

    /// <summary>
    /// Extracts the contents of a ZIP archive file to the specified directory.
    /// </summary>
    /// <param name="archiveFile">The ZIP archive file to extract.</param>
    /// <param name="directory">The destination directory where ZIP contents will be extracted.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous ZIP extraction operation.</returns>
    public static async Task UnZipTo(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        using var zipFile = System.IO.Compression.ZipFile.OpenRead(archiveFile);
        try
        {
            foreach (ZipArchiveEntry entry in zipFile.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await HandleEntry(entry);
            }
        }
        finally
        {
            if (zipFile != null)
            {
                ((IDisposable)zipFile).Dispose();
            }
        }

        async Task HandleEntry(ZipArchiveEntry entry)
        {
            AbsolutePath absolutePath = directory / entry.FullName;
            absolutePath.Parent?.CreateDirectory();
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                absolutePath.CreateDirectory();
            }
            else
            {
                await using Stream stream = entry.Open();
                await using FileStream destination = File.Open(absolutePath, FileMode.Create);
                await stream.CopyToAsync(destination, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Compresses the specified files from a directory to a TAR.GZ archive file.
    /// </summary>
    /// <param name="baseDirectory">The base directory containing the files to compress.</param>
    /// <param name="archiveFile">The destination TAR.GZ archive file path.</param>
    /// <param name="files">The collection of files to include in the archive.</param>
    /// <param name="fileMode">The file mode to use when creating the archive file.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous TAR.GZ compression operation.</returns>
    public static Task TarGZipTo(this AbsolutePath baseDirectory, AbsolutePath archiveFile, IEnumerable<AbsolutePath> files, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return CompressTar(baseDirectory, archiveFile, [.. files], fileMode, stream => new GZipOutputStream(stream), cancellationToken);
    }

    /// <summary>
    /// Compresses the specified directory to a TAR.GZ archive file.
    /// </summary>
    /// <param name="directory">The directory containing files to compress.</param>
    /// <param name="archiveFile">The destination TAR.GZ archive file path.</param>
    /// <param name="filter">Optional filter function to select which files to include in the archive.</param>
    /// <param name="fileMode">The file mode to use when creating the archive file.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous TAR.GZ compression operation.</returns>
    public static Task TarGZipTo(this AbsolutePath directory, AbsolutePath archiveFile, Func<AbsolutePath, bool>? filter = null, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            filter ??= _ => true;

            IEnumerable<AbsolutePath> files = directory.GetFiles("*", int.MaxValue).Where(filter);
            await directory.TarGZipTo(archiveFile, files, fileMode, cancellationToken);

        }, cancellationToken);
    }

    /// <summary>
    /// Compresses the specified files from a directory to a TAR.BZ2 archive file.
    /// </summary>
    /// <param name="directory">The directory containing the files to compress.</param>
    /// <param name="archiveFile">The destination TAR.BZ2 archive file path.</param>
    /// <param name="files">The collection of files to include in the archive.</param>
    /// <param name="fileMode">The file mode to use when creating the archive file.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous TAR.BZ2 compression operation.</returns>
    public static Task TarBZip2To(this AbsolutePath directory, AbsolutePath archiveFile, IEnumerable<AbsolutePath> files, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return CompressTar(directory, archiveFile, [.. files], fileMode, stream => new BZip2OutputStream(stream), cancellationToken);
    }

    /// <summary>
    /// Compresses the specified directory to a TAR.BZ2 archive file.
    /// </summary>
    /// <param name="directory">The directory containing files to compress.</param>
    /// <param name="archiveFile">The destination TAR.BZ2 archive file path.</param>
    /// <param name="filter">Optional filter function to select which files to include in the archive.</param>
    /// <param name="fileMode">The file mode to use when creating the archive file.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous TAR.BZ2 compression operation.</returns>
    public static Task TarBZip2To(this AbsolutePath directory, AbsolutePath archiveFile, Func<AbsolutePath, bool>? filter = null, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            filter ??= _ => true;

            IEnumerable<AbsolutePath> files = directory.GetFiles("*", int.MaxValue).Where(filter);
            await directory.TarBZip2To(archiveFile, files, fileMode, cancellationToken);

        }, cancellationToken);
    }

    /// <summary>
    /// Extracts the contents of a TAR.GZ archive file to the specified directory.
    /// </summary>
    /// <param name="archiveFile">The TAR.GZ archive file to extract.</param>
    /// <param name="directory">The destination directory where TAR.GZ contents will be extracted.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous TAR.GZ extraction operation.</returns>
    public static Task UnTarGZipTo(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        return UncompressTar(archiveFile, directory, stream => new GZipInputStream(stream), cancellationToken);
    }

    /// <summary>
    /// Extracts the contents of a TAR.BZ2 archive file to the specified directory.
    /// </summary>
    /// <param name="archiveFile">The TAR.BZ2 archive file to extract.</param>
    /// <param name="directory">The destination directory where TAR.BZ2 contents will be extracted.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous TAR.BZ2 extraction operation.</returns>
    public static Task UnTarBZip2To(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        return UncompressTar(archiveFile, directory, stream => new BZip2InputStream(stream), cancellationToken);
    }

    /// <summary>
    /// Compresses the specified directory to a 7z archive file.
    /// NOTE: Creating standard 7z archives is not currently supported due to library limitations.
    /// This method throws a NotSupportedException to clearly indicate the limitation.
    /// </summary>
    /// <param name="directory">The directory containing files to compress.</param>
    /// <param name="archiveFile">The destination 7z archive file path.</param>
    /// <param name="filter">Optional filter function to select which files to include in the archive.</param>
    /// <param name="compressionLevel">The compression level to use (1-9, where 9 is maximum compression).</param>
    /// <param name="fileMode">The file mode to use when creating the archive file.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous 7z compression operation.</returns>
    /// <exception cref="NotSupportedException">Thrown because standard 7z creation is not supported. Use ZIP or TAR.GZ instead.</exception>
    public static Task SevenZipTo(this AbsolutePath directory, AbsolutePath archiveFile, Func<AbsolutePath, bool>? filter = null, int compressionLevel = 5, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Creating standard 7z archives is not currently supported. " +
            "The underlying SharpCompress library does not support writing 7z files. " +
            "Consider using ZIP (.zip) or TAR.GZ (.tar.gz) formats instead, which provide excellent compression and are widely supported.");
    }

    /// <summary>
    /// Extracts the contents of a 7z archive file to the specified directory.
    /// This method can extract standard 7z files created by tools like 7-Zip, WinRAR, etc.
    /// </summary>
    /// <param name="archiveFile">The 7z archive file to extract.</param>
    /// <param name="directory">The destination directory where 7z contents will be extracted.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous 7z extraction operation.</returns>
    public static async Task UnSevenZipTo(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            directory.CreateDirectory();

            using var fileStream = File.OpenRead(archiveFile);
            using var archive = SevenZipArchive.Open(fileStream);

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                var targetPath = directory / entry.Key;
                targetPath.Parent?.CreateDirectory();

                using var entryStream = entry.OpenEntryStream();
                using var outputStream = File.Create(targetPath);
                entryStream.CopyTo(outputStream);
            }
        }, cancellationToken);
    }

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

    private static string UnixRelativeName(AbsolutePath file, AbsolutePath directory)
    {
        string relativeName = file.ToString().Replace(directory.ToString(), "", StringComparison.InvariantCultureIgnoreCase);

        if (Path.IsPathRooted(relativeName))
        {
            relativeName = relativeName[Path.GetPathRoot(relativeName)!.Length..];
        }
        relativeName = relativeName.Replace(@"\", "/");

        while ((relativeName.Length > 0) && (relativeName[0] == '/'))
        {
            relativeName = relativeName[1..];
        }

        return relativeName;
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