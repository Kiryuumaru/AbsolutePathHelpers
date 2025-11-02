using ICSharpCode.SharpZipLib.BZip2;
using System.IO;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
	/// <summary>
	/// Compresses the specified files from a directory to a TAR.BZ2 archive file.
	/// </summary>
	public static Task TarBZip2To(this AbsolutePath directory, AbsolutePath archiveFile, IEnumerable<AbsolutePath> files, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
	{
		return CompressTar(directory, archiveFile, [.. files], fileMode, stream => new BZip2OutputStream(stream), cancellationToken);
	}

	/// <summary>
	/// Compresses the specified directory to a TAR.BZ2 archive file.
	/// </summary>
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
	/// Extracts the contents of a TAR.BZ2 archive file to the specified directory.
	/// </summary>
	public static Task UnTarBZip2To(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
	{
		return UncompressTar(archiveFile, directory, stream => new BZip2InputStream(stream), cancellationToken);
	}
}