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

        await dirToCompress.ZipTo(zipFile);
        await dirToCompress.Delete();
        await zipFile.UnZipTo(dirToCompress);

        Assert.Equal("test1", await testFile1.ReadAllText());
        Assert.Equal("test2", await testFile2.ReadAllText());
        Assert.Equal("test3", await testFile3.ReadAllText());
        Assert.Equal("test4", await testFile4.ReadAllText());

        await dirToCompress.TarGZipTo(tarFile);
        await dirToCompress.Delete();
        await tarFile.UnTarGZipTo(dirToCompress);

        Assert.Equal("test1", await testFile1.ReadAllText());
        Assert.Equal("test2", await testFile2.ReadAllText());
        Assert.Equal("test3", await testFile3.ReadAllText());
        Assert.Equal("test4", await testFile4.ReadAllText());
    }

    [Fact]
    public async void Test2()
    {
        AbsolutePath path1 = "C:\\ManagedCICDRunner";

        await Task.Delay(1000);

        var ss1 = await path1.GetProcesses();

        int ss = 1;
    }
}
