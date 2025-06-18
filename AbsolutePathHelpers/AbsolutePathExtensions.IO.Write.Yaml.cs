using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#if NETSTANDARD
#elif NET5_0_OR_GREATER
using static AbsolutePathHelpers.Common.Internals.Message;
#endif

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Asynchronously serializes an object to YAML and writes it to a file at the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize to YAML.</typeparam>
    /// <param name="absolutePath">The absolute path to the file where the YAML will be written.</param>
    /// <param name="obj">The object to serialize to YAML.</param>
    /// <param name="serializerBuilderFactory">An optional factory function to configure the <see cref="SerializerBuilder"/> with custom settings.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="YamlDotNet.Core.YamlException">An error occurred during YAML serialization.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.OpenOrCreate"/> mode.
    /// 
    /// The YAML serializer uses YamlDotNet's default settings unless a custom configuration is provided 
    /// through the <paramref name="serializerBuilderFactory"/> parameter. Common customizations include:
    /// <list type="bullet">
    ///   <item><description>Setting a different naming convention (e.g., CamelCaseNamingConvention)</description></item>
    ///   <item><description>Configuring flow style or JSON compatibility</description></item>
    ///   <item><description>Handling of default values and empty collections</description></item>
    /// </list>
    /// 
    /// This method requires dynamic code generation capabilities which may be restricted in some environments.
    /// For AOT compilation scenarios, consider using the overload that accepts a <see cref="StaticContext"/> parameter.
    /// </remarks>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode(RequiresDynamicCode)]
#endif
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static async Task WriteYaml<T>(this AbsolutePath absolutePath, T obj, Func<SerializerBuilder, SerializerBuilder>? serializerBuilderFactory = null, CancellationToken cancellationToken = default)
    {
        var serializerBuilder = serializerBuilderFactory?.Invoke(new SerializerBuilder()) ?? new SerializerBuilder();
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.OpenOrCreate, FileAccess.Write);
        await using var streamWriter = new StreamWriter(fileStream);
        serializerBuilder.Build().Serialize(streamWriter, obj);
        cancellationToken.ThrowIfCancellationRequested();
#if NET8_0_OR_GREATER
        await streamWriter.FlushAsync(cancellationToken);
#else
        await streamWriter.FlushAsync();
#endif
        await fileStream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously serializes an object to YAML using a static context and writes it to a file at the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize to YAML.</typeparam>
    /// <param name="absolutePath">The absolute path to the file where the YAML will be written.</param>
    /// <param name="obj">The object to serialize to YAML.</param>
    /// <param name="staticContext">The static context that provides type information for AOT-compatible serialization.</param>
    /// <param name="staticSerializerBuilderFactory">An optional factory function to configure the <see cref="StaticSerializerBuilder"/> with custom settings.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="YamlDotNet.Core.YamlException">An error occurred during YAML serialization.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.OpenOrCreate"/> mode.
    /// 
    /// This method is compatible with AOT compilation scenarios when used with a properly configured 
    /// <see cref="StaticContext"/>. The <see cref="StaticContext"/> provides explicit type information 
    /// that replaces the reflection-based type resolution used in the non-static serializer.
    /// 
    /// Use this method in environments where dynamic code generation is restricted, such as iOS apps
    /// or applications compiled with AOT settings. You must configure the <see cref="StaticContext"/>
    /// with all types that will be encountered during serialization.
    /// </remarks>
    public static async Task WriteYaml<T>(this AbsolutePath absolutePath, T obj, StaticContext staticContext, Func<StaticSerializerBuilder, StaticSerializerBuilder>? staticSerializerBuilderFactory = null, CancellationToken cancellationToken = default)
    {
        var staticSerializerBuilder = staticSerializerBuilderFactory?.Invoke(new StaticSerializerBuilder(staticContext)) ?? new StaticSerializerBuilder(staticContext);
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.OpenOrCreate, FileAccess.Write);
        await using var streamWriter = new StreamWriter(fileStream);
        staticSerializerBuilder.Build().Serialize(streamWriter, obj);
        cancellationToken.ThrowIfCancellationRequested();
#if NET8_0_OR_GREATER
        await streamWriter.FlushAsync(cancellationToken);
#else
        await streamWriter.FlushAsync();
#endif
        await fileStream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes a <see cref="YamlStream"/> to a file at the specified path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file where the YAML will be written.</param>
    /// <param name="yamlStream">The <see cref="YamlStream"/> to write to the file.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="yamlStream"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.OpenOrCreate"/> mode.
    /// 
    /// <see cref="YamlStream"/> provides a document object model (DOM) for YAML content, similar to how 
    /// <see cref="JsonDocument"/> provides a DOM for JSON content. This method is useful when:
    /// <list type="bullet">
    ///   <item><description>You need to construct or manipulate a YAML document programmatically</description></item>
    ///   <item><description>You're working with multi-document YAML streams</description></item>
    ///   <item><description>You need precise control over the YAML structure and formatting</description></item>
    /// </list>
    /// 
    /// This method is more low-level than the object serialization methods and requires you to build
    /// the YAML structure manually before writing it.
    /// </remarks>
    public static async Task WriteYamlStream(this AbsolutePath absolutePath, YamlStream yamlStream, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.OpenOrCreate, FileAccess.Write);
        await using var streamWriter = new StreamWriter(fileStream);
        yamlStream.Save(streamWriter);
        cancellationToken.ThrowIfCancellationRequested();
#if NET8_0_OR_GREATER
        await streamWriter.FlushAsync(cancellationToken);
#else
        await streamWriter.FlushAsync();
#endif
        await fileStream.FlushAsync(cancellationToken);
    }
}