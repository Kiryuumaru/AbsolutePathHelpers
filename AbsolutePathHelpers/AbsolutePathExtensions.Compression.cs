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