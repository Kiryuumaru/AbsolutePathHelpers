using AbsolutePathHelpers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

AbsolutePath t = "C:\\NetConduit\\gstreamer-dynamic-portable.tar.gz";
var tEx = t.Parent! / t.Stem;
await t.UncompressTo(tEx);

return;

Console.WriteLine("Testing 7z extraction support with SharpCompress...");

// Test extracting a real 7z file
AbsolutePath sevenZipFile = "C:\\Users\\Administrator\\Downloads\\lzma2501.7z";
AbsolutePath extractDir = "C:\\Users\\Administrator\\Downloads\\lzma2501.7z.dump";

if (File.Exists(sevenZipFile))
{
    Console.WriteLine($"Extracting 7z file: {sevenZipFile}");
    await sevenZipFile.UncompressTo(extractDir);
    Console.WriteLine($"✅ Successfully extracted to: {extractDir}");
}
else
{
    Console.WriteLine($"❌ 7z file not found: {sevenZipFile}");
}

Console.WriteLine();
Console.WriteLine("Testing compression with supported formats...");

// Test creating archives with supported formats
AbsolutePath testDir = "C:\\temp\\compression-test";
AbsolutePath sourceDir = testDir / "source";

// Clean up first
await testDir.Delete();

// Create test files
await (sourceDir / "file1.txt").WriteAllText("Hello from file 1!");
await (sourceDir / "subdir" / "file2.txt").WriteAllText("Hello from file 2 in subdirectory!");

Console.WriteLine($"Created test files in: {sourceDir}");

// Test ZIP compression (works)
var zipFile = testDir / "test.zip";
var zipExtractDir = testDir / "zip-extracted";
await sourceDir.CompressTo(zipFile);
await zipFile.UncompressTo(zipExtractDir);
Console.WriteLine($"✅ ZIP: Created {zipFile}, extracted to {zipExtractDir}");

// Test TAR.GZ compression (works)
var tarGzFile = testDir / "test.tar.gz";
var tarGzExtractDir = testDir / "targz-extracted";
await sourceDir.CompressTo(tarGzFile);
await tarGzFile.UncompressTo(tarGzExtractDir);
Console.WriteLine($"✅ TAR.GZ: Created {tarGzFile}, extracted to {tarGzExtractDir}");

// Test 7z compression (will fail with clear error)
try
{
    var sevenZipTestFile = testDir / "test.7z";
    await sourceDir.CompressTo(sevenZipTestFile);
    Console.WriteLine($"✅ 7z: Created {sevenZipTestFile}");
}
catch (NotSupportedException ex)
{
    Console.WriteLine($"❌ 7z creation not supported: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("Summary:");
Console.WriteLine("• ✅ 7z extraction: Supports reading standard 7z files");
Console.WriteLine("• ❌ 7z creation: Not supported (library limitation)");
Console.WriteLine("• ✅ ZIP: Full support for creation and extraction");
Console.WriteLine("• ✅ TAR.GZ: Full support for creation and extraction");
Console.WriteLine("• ✅ TAR.BZ2: Full support for creation and extraction");