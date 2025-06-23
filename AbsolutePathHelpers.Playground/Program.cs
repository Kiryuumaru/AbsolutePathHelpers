using AbsolutePathHelpers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

AbsolutePath zipPath = "D:\\Downloads\\asc\\Logistics-v3.0.0-alpha.zip";
AbsolutePath extractPath = "D:\\Downloads\\asc\\Logistics-v3.0.0-alpha";


await zipPath.UnZipTo(extractPath);
