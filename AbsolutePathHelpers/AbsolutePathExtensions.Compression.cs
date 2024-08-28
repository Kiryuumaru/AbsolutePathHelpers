using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Compresses the specified directory to the specified archive file.
    /// </summary>
    /// <param name="directory">The directory to compress.</param>
    /// <param name="archiveFile">The destination archive file.</param>
    /// <param name="filter">Optional filter to select files to include in the archive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown if the archive file extension is unknown.</exception>
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
    /// Uncompresses the specified archive file to the specified directory.
    /// </summary>
    /// <param name="archiveFile">The archive file to uncompress.</param>
    /// <param name="directory">The destination directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown if the archive file extension is unknown.</exception>
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
    /// Compresses the specified directory to a ZIP archive.
    /// </summary>
    /// <param name="directory">The directory to compress.</param>
    /// <param name="archiveFile">The destination ZIP archive file.</param>
    /// <param name="filter">Optional filter to select files to include in the archive.</param>
    /// <param name="compressionLevel">The level of compression to apply.</param>
    /// <param name="fileMode">The file mode to use when creating the archive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task ZipTo(this AbsolutePath directory, AbsolutePath archiveFile, Func<AbsolutePath, bool>? filter = null, CompressionLevel compressionLevel = CompressionLevel.Optimal, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            archiveFile.Parent?.CreateDirectory();

            filter ??= (AbsolutePath _) => true;

            List<AbsolutePath> list = directory.GetFiles("*", int.MaxValue).Where(filter).ToList();
            ZipArchive zipArchive;
            using FileStream stream = File.Open(archiveFile, fileMode, FileAccess.ReadWrite);
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

        }, cancellationToken);
    }

    /// <summary>
    /// Unzips the specified ZIP archive to the specified directory.
    /// </summary>
    /// <param name="archiveFile">The ZIP archive file to uncompress.</param>
    /// <param name="directory">The destination directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task UnZipTo(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
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
                using Stream stream = entry.Open();
                using FileStream destination = File.Open(absolutePath, FileMode.Create);
                await stream.CopyToAsync(destination, cancellationToken);
            }

        }, cancellationToken);
    }

    /// <summary>
    /// Compresses the specified files in a directory to a TAR.GZ archive.
    /// </summary>
    /// <param name="baseDirectory">The base directory containing the files to compress.</param>
    /// <param name="archiveFile">The destination TAR.GZ archive file.</param>
    /// <param name="files">The files to compress.</param>
    /// <param name="fileMode">The file mode to use when creating the archive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task TarGZipTo(this AbsolutePath baseDirectory, AbsolutePath archiveFile, IEnumerable<AbsolutePath> files, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return CompressTar(baseDirectory, archiveFile, files.ToList(), fileMode, (Stream x) => new GZipOutputStream(x), cancellationToken);
    }

    /// <summary>
    /// Compresses the specified directory to a TAR.GZ archive.
    /// </summary>
    /// <param name="directory">The directory to compress.</param>
    /// <param name="archiveFile">The destination TAR.GZ archive file.</param>
    /// <param name="filter">Optional filter to select files to include in the archive.</param>
    /// <param name="fileMode">The file mode to use when creating the archive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task TarGZipTo(this AbsolutePath directory, AbsolutePath archiveFile, Func<AbsolutePath, bool>? filter = null, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            filter ??= (AbsolutePath _) => true;

            IEnumerable<AbsolutePath> files = directory.GetFiles("*", int.MaxValue).Where(filter);
            await directory.TarGZipTo(archiveFile, files, fileMode, cancellationToken);

        }, cancellationToken);
    }

    /// <summary>
    /// Compresses the specified files in a directory to a TAR.BZ2 archive.
    /// </summary>
    /// <param name="directory">The directory containing the files to compress.</param>
    /// <param name="archiveFile">The destination TAR.BZ2 archive file.</param>
    /// <param name="files">The files to compress.</param>
    /// <param name="fileMode">The file mode to use when creating the archive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task TarBZip2To(this AbsolutePath directory, AbsolutePath archiveFile, IEnumerable<AbsolutePath> files, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return CompressTar(directory, archiveFile, files.ToList(), fileMode, (Stream x) => new BZip2OutputStream(x), cancellationToken);
    }

    /// <summary>
    /// Compresses the specified directory to a TAR.BZ2 archive.
    /// </summary>
    /// <param name="directory">The directory to compress.</param>
    /// <param name="archiveFile">The destination TAR.BZ2 archive file.</param>
    /// <param name="filter">Optional filter to select files to include in the archive.</param>
    /// <param name="fileMode">The file mode to use when creating the archive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task TarBZip2To(this AbsolutePath directory, AbsolutePath archiveFile, Func<AbsolutePath, bool>? filter = null, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            filter ??= (AbsolutePath _) => true;

            IEnumerable<AbsolutePath> files = directory.GetFiles("*", int.MaxValue).Where(filter);
            await directory.TarBZip2To(archiveFile, files, fileMode, cancellationToken);

        }, cancellationToken);
    }

    /// <summary>
    /// Uncompresses the specified TAR.GZ archive to the specified directory.
    /// </summary>
    /// <param name="archiveFile">The TAR.GZ archive file to uncompress.</param>
    /// <param name="directory">The destination directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task UnTarGZipTo(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        return UncompressTar(archiveFile, directory, (Stream x) => new GZipInputStream(x), cancellationToken);
    }

    /// <summary>
    /// Uncompresses the specified TAR.BZ2 archive to the specified directory.
    /// </summary>
    /// <param name="archiveFile">The TAR.BZ2 archive file to uncompress.</param>
    /// <param name="directory">The destination directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task UnTarBZip2To(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
    {
        return UncompressTar(archiveFile, directory, (Stream x) => new BZip2InputStream(x), cancellationToken);
    }

    private static Task CompressTar(AbsolutePath baseDirectory, AbsolutePath archiveFile, IReadOnlyCollection<AbsolutePath> files, FileMode fileMode, Func<Stream, Stream> outputStreamFactory, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            archiveFile.Parent?.CreateDirectory();

            using var fileStream = File.Open(archiveFile, fileMode, FileAccess.ReadWrite);
            using var outputStream = outputStreamFactory(fileStream);
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

        }, cancellationToken);
    }

    private static Task UncompressTar(AbsolutePath archiveFile, AbsolutePath directory, Func<Stream, Stream> inputStreamFactory, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var fileStream = File.OpenRead(archiveFile);
            using var inputStream = inputStreamFactory(fileStream);
            using var tarArchive = TarArchive.CreateInputTarArchive(inputStream, nameEncoding: null);

            directory.CreateDirectory();

            tarArchive.ExtractContents(directory);

        }, cancellationToken);
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
            relativeName = relativeName.Remove(0, 1);
        }

        return relativeName;
    }
}
