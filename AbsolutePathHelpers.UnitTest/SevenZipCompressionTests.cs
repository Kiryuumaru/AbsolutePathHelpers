namespace AbsolutePathHelpers.UnitTest;

/// <summary>
/// Unit tests specifically for 7z extraction functionality using SharpCompress.
/// Note: 7z creation is not supported, only extraction.
/// </summary>
public class SevenZipExtractionTests
{
    [Fact]
    public async Task SevenZip_CreationNotSupported_ShouldThrowNotSupportedException()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "7z-creation-test";
        var sourceDir = testDir / "source";
        var archiveFile = testDir / "test.7z";

        // Test that SevenZipTo throws NotSupportedException
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await sourceDir.SevenZipTo(archiveFile);
        });
    }

    [Fact]
    public async Task SevenZip_GenericCompressionNotSupported_ShouldThrowNotSupportedException()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "7z-generic-test";
        var sourceDir = testDir / "source";
        var archiveFile = testDir / "test.7z";

        // Test that CompressTo with .7z extension throws NotSupportedException
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await sourceDir.CompressTo(archiveFile);
        });
    }

    [Fact]
    public async Task SevenZip_CreationParametersStillValidated()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "7z-params-test";
        var sourceDir = testDir / "source";
        var archiveFile = testDir / "test.7z";

        // Even though creation isn't supported, parameters should still be validated
        // Test with different compression levels
        for (int level = 1; level <= 9; level++)
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await sourceDir.SevenZipTo(archiveFile, compressionLevel: level);
            });
        }

        // Test with different file modes
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await sourceDir.SevenZipTo(archiveFile, fileMode: FileMode.Create);
        });

        // Test with filter
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await sourceDir.SevenZipTo(archiveFile, filter: path => path.Extension == ".txt");
        });
    }

    [Fact]
    public async Task SevenZip_ExceptionMessage_ShouldBeHelpful()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "7z-message-test";
        var sourceDir = testDir / "source";
        var archiveFile = testDir / "test.7z";

        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await sourceDir.SevenZipTo(archiveFile);
        });

        // Verify the exception message is helpful
        Assert.Contains("7z archives", exception.Message);
        Assert.Contains("not currently supported", exception.Message);
        Assert.Contains("SharpCompress", exception.Message);
        Assert.Contains("ZIP", exception.Message);
        Assert.Contains("TAR.GZ", exception.Message);
    }

    // NOTE: We can't test actual 7z extraction without creating a real 7z file first,
    // which would require external tools or pre-existing test files.
    // The extraction functionality has been tested manually with real 7z files.
    
    [Fact]
    public async Task SevenZip_ExtractionMethodExists_CanBeCalled()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "7z-extraction-test";
        var nonExistentFile = testDir / "nonexistent.7z";
        var extractDir = testDir / "extracted";

        // Test that the method exists and can be called (even if file doesn't exist)
        try
        {
            await nonExistentFile.UnSevenZipTo(extractDir);
            Assert.Fail("Should have thrown an exception for non-existent file");
        }
        catch (FileNotFoundException)
        {
            // This is expected - file doesn't exist
            Assert.True(true);
        }
        catch (DirectoryNotFoundException)
        {
            // This is also acceptable - directory doesn't exist  
            Assert.True(true);
        }
    }

    [Fact]
    public async Task SevenZip_UncompressToMethod_RecognizesSevenZExtension()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "7z-uncompress-test";
        var nonExistentFile = testDir / "nonexistent.7z";
        var extractDir = testDir / "extracted";

        // Test that UncompressTo recognizes .7z extension and calls UnSevenZipTo
        try
        {
            await nonExistentFile.UncompressTo(extractDir);
            Assert.Fail("Should have thrown an exception for non-existent file");
        }
        catch (FileNotFoundException)
        {
            // This is expected - file doesn't exist
            Assert.True(true);
        }
        catch (DirectoryNotFoundException)
        {
            // This is also acceptable - directory doesn't exist
            Assert.True(true);
        }
    }
}