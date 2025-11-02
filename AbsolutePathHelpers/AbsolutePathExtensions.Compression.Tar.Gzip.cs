using ICSharpCode.SharpZipLib.GZip;
using System.IO;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
	/// <summary>
	/// Compresses the specified files from a directory to a TAR.GZ archive file.
	/// </summary>
	public static Task TarGZipTo(this AbsolutePath baseDirectory, AbsolutePath archiveFile, IEnumerable<AbsolutePath> files, FileMode fileMode = FileMode.CreateNew, CancellationToken cancellationToken = default)
	{
		return CompressTar(baseDirectory, archiveFile, [.. files], fileMode, stream => new GZipOutputStream(stream), cancellationToken);
	}

	/// <summary>
	/// Compresses the specified directory to a TAR.GZ archive file.
	/// </summary>
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
	/// Extracts the contents of a TAR.GZ archive file to the specified directory.
	/// </summary>
	public static Task UnTarGZipTo(this AbsolutePath archiveFile, AbsolutePath directory, CancellationToken cancellationToken = default)
	{
		return UncompressTar(archiveFile, directory, stream => new GZipInputStream(stream), cancellationToken);
	}
}