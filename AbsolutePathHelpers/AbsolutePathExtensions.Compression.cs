using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

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
        return archiveFile.Extension.ToLowerInvariant() switch
        {
            ".zip" => directory.ZipTo(archiveFile, filter, cancellationToken: cancellationToken),
            ".tar.gz" or ".tgz" => directory.TarGZipTo(archiveFile, filter, cancellationToken: cancellationToken),
            ".tar.bz2" or ".tbz2" or ".tbz" => directory.TarBZip2To(archiveFile, filter, cancellationToken: cancellationToken),
            _ => throw new Exception("Unknown archive extension for archive '" + Path.GetFileName(archiveFile) + "'"),
        };
    }

    /// <summary>
    /// Extracts the contents of an archive file to the specified directory, with format determined by the archive file extension.
    /// </summary>
    /// <param name="archiveFile">The archive file to extract (.zip, .tar.gz, .tgz, .tar.bz2, .tbz2, or .tbz).</param>
    /// <param name="directory">The destination directory where contents will be extracted.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous extraction operation.</returns>
    /// <exception cref="Exception">Thrown if the archive file has an unsupported extension.</exception>
    public static Task UncompressTo(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        return archiveFile.Extension.ToLowerInvariant() switch
        {
            ".zip" => archiveFile.UnZipTo(directory, cancellationToken: cancellationToken),
            ".tar.gz" or ".tgz" => archiveFile.UnTarGZipTo(directory, cancellationToken: cancellationToken),
            ".tar.bz2" or ".tbz2" or ".tbz" => archiveFile.UnTarBZip2To(directory, cancellationToken: cancellationToken),
            _ => throw new Exception("Unknown archive extension for archive '" + Path.GetFileName(archiveFile) + "'"),
        };
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