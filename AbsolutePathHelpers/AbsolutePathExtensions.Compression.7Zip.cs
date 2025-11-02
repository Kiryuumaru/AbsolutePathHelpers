using SharpCompress.Archives.SevenZip;
using System.IO;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
	/// <summary>
	/// Compresses the specified directory to a 7z archive file.
	/// NOTE: Creating standard 7z archives is not currently supported due to library limitations.
	/// This method throws a NotSupportedException to clearly indicate the limitation.
	/// </summary>
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
}