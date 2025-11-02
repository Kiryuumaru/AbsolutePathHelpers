using AbsolutePathHelpers;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace AbsolutePathHelpers.UnitTest;

public class SymbolicLinkCompressionTests
{
	[Theory]
	[InlineData(".zip")]
	[InlineData(".tar.gz")]
	public async Task CompressAndExtract_ShouldHandleSymbolicLinks(string archiveExtension)
	{
		var testDir = AbsolutePath.Create(Path.Combine(Environment.CurrentDirectory, "bin", "symbolic-link-tests", Guid.NewGuid().ToString("N")));
		var sourceDir = testDir / "source";
		var extractDir = testDir / "extract";
		var archiveFile = testDir / ($"archive{archiveExtension}");

		try
		{
			await testDir.Delete();
			sourceDir.CreateDirectory();
			extractDir.CreateDirectory();

			var targetFile = sourceDir / "target.txt";
			var linkFile = sourceDir / "link.txt";

			await targetFile.WriteAllText($"Payload-{Guid.NewGuid():N}");

			if (!EnsureSymbolicLink(linkFile, targetFile))
			{
				return;
			}

			await sourceDir.CompressTo(archiveFile);
			Assert.True(archiveFile.FileExists(), $"Archive '{archiveFile}' should exist after compression.");

			await archiveFile.UncompressTo(extractDir);

			var extractedTarget = extractDir / "target.txt";
			var extractedLink = extractDir / "link.txt";

			Assert.True(extractedTarget.FileExists(), "Target file should exist after extraction.");

			var expectedContent = await targetFile.ReadAllText();
			await AssertLinkOrFileHasContent(extractedLink, expectedContent);
		}
		finally
		{
			await testDir.Delete();
		}
	}

	private static bool EnsureSymbolicLink(AbsolutePath linkPath, AbsolutePath targetPath)
	{
		try
		{
			linkPath.Parent?.CreateDirectory();

			if (linkPath.FileExists())
			{
				File.Delete(linkPath);
			}

			File.CreateSymbolicLink(linkPath, targetPath);

			var attributes = File.GetAttributes(linkPath);
			var isSymlink = attributes.HasFlag(FileAttributes.ReparsePoint);
			if (!isSymlink)
			{
				Console.WriteLine("Skipping symbolic-link compression test because created link is not marked as a symbolic link.");
				return false;
			}

			return true;
		}
		catch (Exception ex) when (IsSymbolicLinkUnsupported(ex))
		{
			Console.WriteLine($"Skipping symbolic-link compression test: {ex.Message}");
			return false;
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

	private static async Task AssertLinkOrFileHasContent(AbsolutePath linkPath, string expectedContent)
	{
		if (!linkPath.FileExists() && !linkPath.DirectoryExists())
		{
			Assert.Fail($"Expected '{linkPath}' to exist after extraction.");
		}

		if (linkPath.DirectoryExists())
		{
			Assert.Fail("Extracted symbolic link resolved to a directory unexpectedly.");
		}

		var fileInfo = new FileInfo(linkPath);
		if (!string.IsNullOrEmpty(fileInfo.LinkTarget))
		{
			var resolvedTarget = ResolveLinkTarget(linkPath, fileInfo.LinkTarget!);
			Assert.True(File.Exists(resolvedTarget), $"Link target '{resolvedTarget}' should exist.");

			var actualContent = await File.ReadAllTextAsync(resolvedTarget);
			Assert.Equal(expectedContent, actualContent);
		}
		else
		{
			var actualContent = await linkPath.ReadAllText();
			Assert.Equal(expectedContent, actualContent);
		}
	}

	private static string ResolveLinkTarget(AbsolutePath linkPath, string rawTarget)
	{
		var normalized = rawTarget;
		if (normalized.StartsWith("\\??\\", StringComparison.Ordinal))
		{
			normalized = normalized[4..];
		}

		if (Path.IsPathRooted(normalized))
		{
			return normalized;
		}

		var baseDirectory = linkPath.Parent?.Path ?? Environment.CurrentDirectory;
		return Path.GetFullPath(Path.Combine(baseDirectory, normalized));
	}
}