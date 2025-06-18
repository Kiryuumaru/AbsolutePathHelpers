using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

#if NETSTANDARD
#elif NET5_0_OR_GREATER
using static AbsolutePathHelpers.Common.Internals.Message;
#endif

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Asynchronously serializes an object to JSON and writes it to a file at the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize to JSON.</typeparam>
    /// <param name="absolutePath">The absolute path to the file where the JSON will be written.</param>
    /// <param name="obj">The object to serialize to JSON.</param>
    /// <param name="jsonSerializerOptions">Options to control the JSON serialization behavior.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="JsonException">An error occurred during JSON serialization.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.OpenOrCreate"/> mode.
    /// 
    /// This method requires dynamic code generation capabilities which may be restricted in some environments.
    /// For AOT compilation scenarios, consider using the overload that accepts a <see cref="JsonTypeInfo{T}"/> parameter.
    /// </remarks>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode(RequiresDynamicCode)]
#endif
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static async Task WriteJson<T>(this AbsolutePath absolutePath, T obj, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.OpenOrCreate, FileAccess.Write);
        await JsonSerializer.SerializeAsync(fileStream, obj, jsonSerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously serializes an object to JSON using source-generated metadata and writes it to a file at the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize to JSON.</typeparam>
    /// <param name="absolutePath">The absolute path to the file where the JSON will be written.</param>
    /// <param name="obj">The object to serialize to JSON.</param>
    /// <param name="jsonTypeInfo">Source-generated metadata about the type that guides the serialization process.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="JsonException">An error occurred during JSON serialization.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.OpenOrCreate"/> mode.
    /// 
    /// This method is compatible with AOT compilation scenarios when used with source-generated JSON type information.
    /// </remarks>
    public static async Task WriteJson<T>(this AbsolutePath absolutePath, T obj, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.OpenOrCreate, FileAccess.Write);
        await JsonSerializer.SerializeAsync(fileStream, obj, jsonTypeInfo, cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes a <see cref="JsonDocument"/> to a file at the specified path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file where the JSON will be written.</param>
    /// <param name="jsonDocument">The <see cref="JsonDocument"/> to write to the file.</param>
    /// <param name="jsonWriterOptions">Options to control the JSON writing behavior, such as indentation and escaping.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="ObjectDisposedException">The <paramref name="jsonDocument"/> has been disposed.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="File.Create(string)"/>, which opens the file
    /// with <see cref="FileMode.Create"/> mode.
    /// </remarks>
    public static async Task WriteJsonDocument(this AbsolutePath absolutePath, JsonDocument jsonDocument, JsonWriterOptions jsonWriterOptions = default, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = File.Create(absolutePath.Path);
        await using var jsonWriter = new Utf8JsonWriter(fileStream, jsonWriterOptions);
        jsonDocument.WriteTo(jsonWriter);
        await jsonWriter.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes a <see cref="JsonNode"/> to a file at the specified path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file where the JSON will be written.</param>
    /// <param name="jsonNode">The <see cref="JsonNode"/> to write to the file.</param>
    /// <param name="jsonWriterOptions">Options to control the JSON writing behavior, such as indentation and escaping.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="jsonNode"/> is null.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="File.Create(string)"/>, which opens the file
    /// with <see cref="FileMode.Create"/> mode.
    /// 
    /// <see cref="JsonNode"/> is the base class for <see cref="JsonObject"/> and <see cref="JsonArray"/>,
    /// making this method suitable for writing any JSON node type.
    /// </remarks>
    public static async Task WriteJsonNode(this AbsolutePath absolutePath, JsonNode jsonNode, JsonWriterOptions jsonWriterOptions = default, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = File.Create(absolutePath.Path);
        await using var jsonWriter = new Utf8JsonWriter(fileStream, jsonWriterOptions);
        jsonNode.WriteTo(jsonWriter);
        await jsonWriter.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes a <see cref="JsonObject"/> to a file at the specified path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file where the JSON will be written.</param>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to write to the file.</param>
    /// <param name="jsonWriterOptions">Options to control the JSON writing behavior, such as indentation and escaping.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="jsonObject"/> is null.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="File.Create(string)"/>, which opens the file
    /// with <see cref="FileMode.Create"/> mode.
    /// 
    /// Use this method when you specifically want to write a JSON object (with key-value pairs) to a file.
    /// For other JSON node types, consider using <see cref="WriteJsonNode"/> instead.
    /// </remarks>
    public static async Task WriteJsonObject(this AbsolutePath absolutePath, JsonObject jsonObject, JsonWriterOptions jsonWriterOptions = default, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = File.Create(absolutePath.Path);
        await using var jsonWriter = new Utf8JsonWriter(fileStream, jsonWriterOptions);
        jsonObject.WriteTo(jsonWriter);
        await jsonWriter.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes a <see cref="JsonArray"/> to a file at the specified path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file where the JSON will be written.</param>
    /// <param name="jsonArray">The <see cref="JsonArray"/> to write to the file.</param>
    /// <param name="jsonWriterOptions">Options to control the JSON writing behavior, such as indentation and escaping.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="jsonArray"/> is null.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="File.Create(string)"/>, which opens the file
    /// with <see cref="FileMode.Create"/> mode.
    /// 
    /// Use this method when you specifically want to write a JSON array to a file.
    /// For other JSON node types, consider using <see cref="WriteJsonNode"/> instead.
    /// </remarks>
    public static async Task WriteJsonArray(this AbsolutePath absolutePath, JsonArray jsonArray, JsonWriterOptions jsonWriterOptions = default, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = File.Create(absolutePath.Path);
        await using var jsonWriter = new Utf8JsonWriter(fileStream, jsonWriterOptions);
        jsonArray.WriteTo(jsonWriter);
        await jsonWriter.FlushAsync(cancellationToken);
    }
}