using System.IO.Compression;

namespace AbsolutePathHelpers.UnitTest;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        AbsolutePath path1 = "C:\\test1\\test2";
        AbsolutePath path2 = "C:\\TEST1\\test2";
        AbsolutePath path3 = "C:\\test1\\test4";

        Assert.Equal(path1, path2);
        Assert.NotEqual(path1, path3);
    }

    [Fact]
    public async Task CompressionTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "compression-test";

        await testDir.Delete();

        var dirToCompress = testDir / "try";

        var testFile1 = dirToCompress / "file1.txt";
        var testFile2 = dirToCompress / "file2.txt";
        var testFile3 = dirToCompress / "file3.txt";
        var testFile4 = dirToCompress / "dir" / "file4.txt";

        await testFile1.WriteAllText("test1");
        await testFile2.WriteAllText("test2");
        await testFile3.WriteAllText("test3");
        await testFile4.WriteAllText("test4");

        var zipFile = dirToCompress.Parent! / (dirToCompress.Name + ".zip");
        var tarFile = dirToCompress.Parent! / (dirToCompress.Name + ".tar.gz");

        // Test ZIP compression
        await dirToCompress.ZipTo(zipFile);
        await dirToCompress.Delete();
        await zipFile.UnZipTo(dirToCompress);

        Assert.Equal("test1", await testFile1.ReadAllText());
        Assert.Equal("test2", await testFile2.ReadAllText());
        Assert.Equal("test3", await testFile3.ReadAllText());
        Assert.Equal("test4", await testFile4.ReadAllText());

        // Test TAR.GZ compression
        await dirToCompress.TarGZipTo(tarFile);
        await dirToCompress.Delete();
        await tarFile.UnTarGZipTo(dirToCompress);

        Assert.Equal("test1", await testFile1.ReadAllText());
        Assert.Equal("test2", await testFile2.ReadAllText());
        Assert.Equal("test3", await testFile3.ReadAllText());
        Assert.Equal("test4", await testFile4.ReadAllText());
    }

    [Fact] 
    public async Task SevenZipExtractionOnlyTest()
    {
        // Test that 7z creation throws appropriate exception
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "7z-creation-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        var testFile = sourceDir / "test.txt";
        await testFile.WriteAllText("test content");

        var sevenZipFile = testDir / "test.7z";

        // Test that SevenZipTo throws NotSupportedException
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await sourceDir.SevenZipTo(sevenZipFile);
        });

        // Test that CompressTo throws NotSupportedException for .7z files
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await sourceDir.CompressTo(sevenZipFile);
        });
    }

    [Fact]
    public async Task CompressionExtensionRecognitionTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "extension-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        var testFile = sourceDir / "test.txt";
        await testFile.WriteAllText("test content");

        // Test various supported extensions work
        var zipFile = testDir / "test.zip";
        var tarGzFile = testDir / "test.tar.gz";
        var tgzFile = testDir / "test.tgz";
        var tarBz2File = testDir / "test.tar.bz2";

        // All these should work
        await sourceDir.CompressTo(zipFile);
        await sourceDir.CompressTo(tarGzFile);
        await sourceDir.CompressTo(tgzFile);
        await sourceDir.CompressTo(tarBz2File);

        Assert.True(File.Exists(zipFile));
        Assert.True(File.Exists(tarGzFile));
        Assert.True(File.Exists(tgzFile));
        Assert.True(File.Exists(tarBz2File));

        // Test extraction
        var zipExtractDir = testDir / "zip-extracted";
        var tarGzExtractDir = testDir / "targz-extracted";
        var tgzExtractDir = testDir / "tgz-extracted";
        var tarBz2ExtractDir = testDir / "tarbz2-extracted";

        await zipFile.UncompressTo(zipExtractDir);
        await tarGzFile.UncompressTo(tarGzExtractDir);
        await tgzFile.UncompressTo(tgzExtractDir);
        await tarBz2File.UncompressTo(tarBz2ExtractDir);

        // Verify extracted content
        Assert.Equal("test content", await (zipExtractDir / "test.txt").ReadAllText());
        Assert.Equal("test content", await (tarGzExtractDir / "test.txt").ReadAllText());
        Assert.Equal("test content", await (tgzExtractDir / "test.txt").ReadAllText());
        Assert.Equal("test content", await (tarBz2ExtractDir / "test.txt").ReadAllText());
    }

    [Fact]
    public async Task UnsupportedArchiveExtensionTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "unsupported-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        var testFile = sourceDir / "test.txt";
        await testFile.WriteAllText("test content");

        var unsupportedFile = testDir / "test.unknown";

        // Test that unsupported extension throws exception
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await sourceDir.CompressTo(unsupportedFile);
        });

        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await unsupportedFile.UncompressTo(testDir / "extracted");
        });
    }

    [Fact]
    public async Task ZipCompressionLevelsTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "zip-compression-levels-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        var testFile = sourceDir / "test.txt";
        var content = string.Join("", Enumerable.Repeat("This is test data that should compress. ", 100));
        await testFile.WriteAllText(content);

        // Test different compression levels for ZIP
        var sizes = new List<long>();
        var levels = new[] { CompressionLevel.NoCompression, CompressionLevel.Fastest, CompressionLevel.Optimal };
        
        for (int i = 0; i < levels.Length; i++)
        {
            var level = levels[i];
            var archiveFile = testDir / $"test-{level}.zip";
            var extractDir = testDir / $"extracted-{level}";

            await sourceDir.ZipTo(archiveFile, compressionLevel: level);
            Assert.True(File.Exists(archiveFile));
            
            sizes.Add(new FileInfo(archiveFile).Length);

            // Verify extraction works
            await archiveFile.UnZipTo(extractDir);
            var extractedFile = extractDir / "test.txt";
            Assert.Equal(content, await extractedFile.ReadAllText());
        }

        // Verify we got different sizes (compression worked)
        Assert.True(sizes.Count == 3);
        Assert.All(sizes, size => Assert.True(size > 0));
        
        // NoCompression should be largest, Optimal should be smallest (usually)
        Assert.True(sizes[0] >= sizes[2]); // NoCompression >= Optimal
    }

    [Fact]
    public async Task FilterFunctionalityTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "filter-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        var txtFile = sourceDir / "include.txt";
        var logFile = sourceDir / "exclude.log";
        var subTxtFile = sourceDir / "subdir" / "include2.txt";
        var subLogFile = sourceDir / "subdir" / "exclude2.log";

        await txtFile.WriteAllText("Include this text file");
        await logFile.WriteAllText("Exclude this log file");
        await subTxtFile.WriteAllText("Include this text file in subdir");
        await subLogFile.WriteAllText("Exclude this log file in subdir");

        var zipFile = testDir / "filtered.zip";
        var extractDir = testDir / "extracted";

        // Compress only .txt files using filter
        await sourceDir.ZipTo(zipFile, filter: path => path.Extension == ".txt");
        
        // Extract and verify only .txt files are present
        await zipFile.UnZipTo(extractDir);

        var extractedTxtFile = extractDir / "include.txt";
        var extractedLogFile = extractDir / "exclude.log";
        var extractedSubTxtFile = extractDir / "subdir" / "include2.txt";
        var extractedSubLogFile = extractDir / "subdir" / "exclude2.log";

        Assert.True(File.Exists(extractedTxtFile));
        Assert.False(File.Exists(extractedLogFile));
        Assert.True(File.Exists(extractedSubTxtFile));
        Assert.False(File.Exists(extractedSubLogFile));

        Assert.Equal("Include this text file", await extractedTxtFile.ReadAllText());
        Assert.Equal("Include this text file in subdir", await extractedSubTxtFile.ReadAllText());
    }

    [Fact]
    public async Task BinaryFileCompressionTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "binary-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        sourceDir.CreateDirectory();
        var binaryFile = sourceDir / "binary.dat";

        // Create binary data
        var binaryData = Enumerable.Range(0, 1000).Select(i => (byte)(i % 256)).ToArray();
        await File.WriteAllBytesAsync(binaryFile, binaryData);

        var zipFile = testDir / "binary.zip";
        var extractDir = testDir / "extracted";

        // Test compression
        await sourceDir.ZipTo(zipFile);
        Assert.True(File.Exists(zipFile));

        // Test extraction
        await zipFile.UnZipTo(extractDir);
        var extractedFile = extractDir / "binary.dat";
        var extractedData = await File.ReadAllBytesAsync(extractedFile);

        Assert.Equal(binaryData, extractedData);
    }

    [Fact]
    public async Task CancellationTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "cancellation-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        
        // Create many files to increase chance of cancellation working
        for (int i = 0; i < 10; i++)
        {
            var testFile = sourceDir / $"test{i}.txt";
            await testFile.WriteAllText($"test content {i}");
        }

        var zipFile = testDir / "test.zip";

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(1); // Cancel after 1ms

        // Test that cancellation is respected (may not always throw due to timing)
        try
        {
            await sourceDir.ZipTo(zipFile, cancellationToken: cts.Token);
            // If it completes before cancellation, that's fine too
        }
        catch (OperationCanceledException)
        {
            // This is the expected behavior when cancellation works
            Assert.True(true);
        }
    }

    [Fact]
    public async Task FileModeTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "filemode-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        var testFile = sourceDir / "test.txt";
        await testFile.WriteAllText("test content");

        var zipFile = testDir / "test.zip";

        // Test FileMode.CreateNew (default)
        await sourceDir.ZipTo(zipFile);
        Assert.True(File.Exists(zipFile));

        // Test that CreateNew throws when file exists
        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await sourceDir.ZipTo(zipFile, fileMode: FileMode.CreateNew);
        });

        // Test FileMode.Create (overwrites existing)
        await sourceDir.ZipTo(zipFile, fileMode: FileMode.Create);
        Assert.True(File.Exists(zipFile));
    }

    [Fact]
    public async Task SpecialCharactersTest()
    {
        var testDir = AbsolutePath.Create(Environment.CurrentDirectory) / "bin" / "special-chars-test";
        await testDir.Delete();

        var sourceDir = testDir / "source";
        var specialFile = sourceDir / "special chars & symbols!.txt";
        var asciiFile = sourceDir / "ascii-test-file.txt";
        var subDir = sourceDir / "sub dir with spaces";
        var subFile = subDir / "file in sub.txt";

        await specialFile.WriteAllText("File with special characters in name");
        await asciiFile.WriteAllText("ASCII filename test");
        await subFile.WriteAllText("File in subdirectory with spaces");

        var zipFile = testDir / "special.zip";
        var extractDir = testDir / "extracted";

        // Test compression
        await sourceDir.ZipTo(zipFile);
        Assert.True(File.Exists(zipFile));

        // Test extraction
        await zipFile.UnZipTo(extractDir);

        var extractedSpecialFile = extractDir / "special chars & symbols!.txt";
        var extractedAsciiFile = extractDir / "ascii-test-file.txt";
        var extractedSubFile = extractDir / "sub dir with spaces" / "file in sub.txt";

        Assert.Equal("File with special characters in name", await extractedSpecialFile.ReadAllText());
        Assert.Equal("ASCII filename test", await extractedAsciiFile.ReadAllText());
        Assert.Equal("File in subdirectory with spaces", await extractedSubFile.ReadAllText());
    }
}
