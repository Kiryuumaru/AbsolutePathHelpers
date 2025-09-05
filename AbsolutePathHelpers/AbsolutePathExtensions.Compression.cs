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
        using var tarArchive = TarArchive.CreateOutputTarArchive(outputStream);

        void AddFile(AbsolutePath file)
        {
            var entry = TarEntry.CreateEntryFromFile(file);
            entry.Name = UnixRelativeName(file, baseDirectory);

            tarArchive.WriteEntry(entry, recurse: false);
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
        using var tarArchive = TarArchive.CreateInputTarArchive(inputStream, nameEncoding: null);

        directory.CreateDirectory();

        tarArchive.ExtractContents(directory);

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
}