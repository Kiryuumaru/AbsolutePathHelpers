using AbsolutePathHelpers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

AbsolutePath execDir = Environment.CurrentDirectory;
AbsolutePath playDir = execDir / "play";

await playDir.CreateOrCleanDirectory();

AbsolutePath jsonFilePath = playDir / "example.json";
AbsolutePath yamlFilePath = playDir / "example.yaml";

await yamlFilePath.W(new SampleJsonStaticClass()
{
    Val1 = "Hello, JSON!",
    Val2 = 42,
    Val3 = DateTimeOffset.UtcNow
}, SampleJsonSerializerContext.Default.SampleJsonStaticClass);



Console.WriteLine($"JSON written to {jsonFilePath.Path}:\n{await jsonFilePath.ReadAllText()}");



Console.WriteLine($"{await jsonFilePath.GetHashSHA256()}");





[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
class SampleDynamicClass
{
    public required string Val1 { get; set; }

    public required int Val2 { get; set; }

    public required DateTimeOffset Val3 { get; set; }
}

[YamlSerializable]
class SampleYamlStaticClass
{
    public required string Val1 { get; set; }

    public required int Val2 { get; set; }

    public required DateTimeOffset Val3 { get; set; }
}

class SampleJsonStaticClass
{
    public required string Val1 { get; set; }

    public required int Val2 { get; set; }

    public required DateTimeOffset Val3 { get; set; }
}

[YamlStaticContext]
[YamlSerializable(typeof(SampleYamlStaticClass))]
partial class SampleStaticContext : StaticContext
{

}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SampleJsonStaticClass))]
partial class SampleJsonSerializerContext : JsonSerializerContext
{

}

