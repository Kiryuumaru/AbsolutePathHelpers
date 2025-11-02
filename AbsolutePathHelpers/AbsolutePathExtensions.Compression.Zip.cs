using System.IO;
using System.IO.Compression;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
	/// <summary>
	/// Compresses the specified directory to a ZIP archive file.
	/// </summary>
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
}