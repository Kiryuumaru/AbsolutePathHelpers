using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

#if NETSTANDARD
#elif NET5_0_OR_GREATER
using static AbsolutePathHelpers.Common.Internals.Message;
#endif

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Asynchronously reads and deserializes a YAML file into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the YAML content into.</typeparam>
    /// <param name="absolutePath">The absolute path to the YAML file to read.</param>
    /// <param name="deserializerBuilderFactory">An optional factory function to configure the <see cref="DeserializerBuilder"/> with custom settings.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the deserialized object of type <typeparamref name="T"/>.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="YamlDotNet.Core.YamlException">The YAML content is invalid or cannot be deserialized to type <typeparamref name="T"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method requires dynamic code generation capabilities which may be restricted in some environments.
    /// For AOT compilation scenarios, consider using the overload that accepts a <see cref="StaticContext"/> parameter.
    /// 
    /// The deserializer uses YamlDotNet's default settings unless a custom configuration is provided through the 
    /// <paramref name="deserializerBuilderFactory"/> parameter.
    /// </remarks>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode(RequiresDynamicCode)]
#endif
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static async Task<T> ReadYaml<T>(this AbsolutePath absolutePath, Func<DeserializerBuilder, DeserializerBuilder>? deserializerBuilderFactory = null, CancellationToken cancellationToken = default)
    {
        var deserializerBuilder = deserializerBuilderFactory?.Invoke(new DeserializerBuilder()) ?? new DeserializerBuilder();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        using var streamReader = new StreamReader(fileStream);
        var deserialized = deserializerBuilder.Build().Deserialize<T>(streamReader);
        cancellationToken.ThrowIfCancellationRequested();
        return deserialized;
    }

    /// <summary>
    /// Asynchronously reads and deserializes a YAML file into an object of type <typeparamref name="T"/> using a static context.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the YAML content into.</typeparam>
    /// <param name="absolutePath">The absolute path to the YAML file to read.</param>
    /// <param name="staticContext">The static context that provides type information for AOT-compatible deserialization.</param>
    /// <param name="staticDeserializerBuilderFactory">An optional factory function to configure the <see cref="StaticDeserializerBuilder"/> with custom settings.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the deserialized object of type <typeparamref name="T"/>.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="YamlDotNet.Core.YamlException">The YAML content is invalid or cannot be deserialized to type <typeparamref name="T"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method is compatible with AOT compilation scenarios when used with a properly configured <see cref="StaticContext"/>.
    /// 
    /// The <see cref="StaticContext"/> provides explicit type information that replaces the reflection-based type resolution
    /// used in the non-static deserializer, making it suitable for environments where dynamic code generation is restricted.
    /// </remarks>
    public static async Task<T> ReadYaml<T>(this AbsolutePath absolutePath, StaticContext staticContext, Func<StaticDeserializerBuilder, StaticDeserializerBuilder>? staticDeserializerBuilderFactory = null, CancellationToken cancellationToken = default)
    {
        var staticDeserializerBuilder = staticDeserializerBuilderFactory?.Invoke(new StaticDeserializerBuilder(staticContext)) ?? new StaticDeserializerBuilder(staticContext);
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        using var streamReader = new StreamReader(fileStream);
        var deserialized = staticDeserializerBuilder.Build().Deserialize<T>(streamReader);
        cancellationToken.ThrowIfCancellationRequested();
        return deserialized;
    }

    /// <summary>
    /// Asynchronously reads a YAML file into a <see cref="YamlStream"/> for low-level access to the YAML structure.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the YAML file to read.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the loaded <see cref="YamlStream"/>.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="YamlDotNet.Core.YamlException">The YAML content is invalid or cannot be parsed.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// The <see cref="YamlStream"/> provides a document object model (DOM) for YAML content, similar to how 
    /// <see cref="JsonDocument"/> provides a DOM for JSON content. This gives you direct access to the YAML 
    /// structure without deserializing to a specific type.
    /// 
    /// This method is useful when:
    /// <list type="bullet">
    ///   <item><description>You need to inspect or manipulate the YAML structure before deserialization</description></item>
    ///   <item><description>You're working with YAML documents that don't map directly to a predefined class</description></item>
    ///   <item><description>You need to access YAML-specific features like anchors, aliases, or document streams</description></item>
    /// </list>
    /// </remarks>
    public static async Task<YamlStream> ReadYamlStream(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        using var streamReader = new StreamReader(fileStream);
        var yaml = new YamlStream();
        yaml.Load(streamReader);
        cancellationToken.ThrowIfCancellationRequested();
        return yaml;
    }
}