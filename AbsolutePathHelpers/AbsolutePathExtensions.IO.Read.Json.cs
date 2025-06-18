using System.Diagnostics.CodeAnalysis;
using System.IO;
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
    /// Asynchronously reads and deserializes a JSON file into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the JSON content into.</typeparam>
    /// <param name="absolutePath">The absolute path of the JSON file to read.</param>
    /// <param name="jsonSerializerOptions">Options to control the behavior of the JSON serializer.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the deserialized object, or null if the JSON represents a null value.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="JsonException">The JSON is invalid or incompatible with type <typeparamref name="T"/>.</exception>
    /// <remarks>
    /// This method requires dynamic code generation capabilities which may be restricted in some environments.
    /// For AOT compilation scenarios, consider using the overload that accepts a <see cref="JsonTypeInfo{T}"/> parameter.
    /// </remarks>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode(RequiresDynamicCode)]
#endif
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static async Task<T?> ReadJson<T>(this AbsolutePath absolutePath, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        return await JsonSerializer.DeserializeAsync<T>(fileStream, jsonSerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads and deserializes a JSON file into an object of type <typeparamref name="T"/> using the provided type information.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the JSON content into.</typeparam>
    /// <param name="absolutePath">The absolute path of the JSON file to read.</param>
    /// <param name="jsonTypeInfo">Metadata about the type that guides the deserialization process.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the deserialized object, or null if the JSON represents a null value.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="JsonException">The JSON is invalid or incompatible with type <typeparamref name="T"/>.</exception>
    /// <remarks>
    /// This method is compatible with AOT compilation scenarios when used with source-generated type information.
    /// </remarks>
    public static async Task<T?> ReadJson<T>(this AbsolutePath absolutePath, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        return await JsonSerializer.DeserializeAsync(fileStream, jsonTypeInfo, cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads and parses a JSON file into a <see cref="JsonDocument"/> for low-level access to the JSON data.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the JSON file to read.</param>
    /// <param name="jsonDocumentOptions">Options to control the behavior of the JSON document parser.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the parsed <see cref="JsonDocument"/>.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="JsonException">The JSON is invalid or cannot be parsed.</exception>
    /// <remarks>
    /// The returned <see cref="JsonDocument"/> provides direct access to the JSON data without deserializing to a specific type.
    /// Remember to dispose the <see cref="JsonDocument"/> when you're done with it, as it maintains unmanaged resources.
    /// </remarks>
    public static async Task<JsonDocument> ReadJsonDocument(this AbsolutePath absolutePath, JsonDocumentOptions jsonDocumentOptions = default, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        return await JsonDocument.ParseAsync(fileStream, jsonDocumentOptions, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads and parses a JSON file into a <see cref="JsonNode"/> object model.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the JSON file to read.</param>
    /// <param name="jsonNodeOptions">Options to control the behavior of the JSON node parser.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the parsed <see cref="JsonNode"/>, or null if the JSON represents a null value.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="JsonException">The JSON is invalid or cannot be parsed.</exception>
    /// <remarks>
    /// The <see cref="JsonNode"/> class hierarchy provides a DOM-like API for working with JSON data,
    /// allowing dynamic traversal and modification of the JSON structure.
    /// In .NET 8 and later, this method uses the asynchronous parsing API. In earlier versions, it uses the synchronous API.
    /// </remarks>
    public static async Task<JsonNode?> ReadJsonNode(this AbsolutePath absolutePath, JsonNodeOptions jsonNodeOptions = default, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
#if NET8_0_OR_GREATER
        return await JsonNode.ParseAsync(fileStream, jsonNodeOptions, cancellationToken: cancellationToken);
#else
        return JsonNode.Parse(fileStream, jsonNodeOptions);
#endif
    }

    /// <summary>
    /// Asynchronously reads and parses a JSON file into a <see cref="JsonObject"/> representing a JSON object.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the JSON file to read.</param>
    /// <param name="jsonNodeOptions">Options to control the behavior of the JSON node parser.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the parsed <see cref="JsonObject"/>, or null if the JSON represents a null value or is not an object.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="JsonException">The JSON is invalid or cannot be parsed.</exception>
    /// <remarks>
    /// This method will return null if the root JSON element is not an object.
    /// Use <see cref="ReadJsonNode"/> if you're uncertain about the root JSON element type.
    /// </remarks>
    public static async Task<JsonObject?> ReadJsonObject(this AbsolutePath absolutePath, JsonNodeOptions jsonNodeOptions = default, CancellationToken cancellationToken = default)
    {
        return (await ReadJsonNode(absolutePath, jsonNodeOptions, cancellationToken: cancellationToken))?.AsObject();
    }

    /// <summary>
    /// Asynchronously reads and parses a JSON file into a <see cref="JsonArray"/> representing a JSON array.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the JSON file to read.</param>
    /// <param name="jsonNodeOptions">Options to control the behavior of the JSON node parser.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the parsed <see cref="JsonArray"/>, or null if the JSON represents a null value or is not an array.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="JsonException">The JSON is invalid or cannot be parsed.</exception>
    /// <remarks>
    /// This method will return null if the root JSON element is not an array.
    /// Use <see cref="ReadJsonNode"/> if you're uncertain about the root JSON element type.
    /// </remarks>
    public static async Task<JsonArray?> ReadJsonArray(this AbsolutePath absolutePath, JsonNodeOptions jsonNodeOptions = default, CancellationToken cancellationToken = default)
    {
        return (await ReadJsonNode(absolutePath, jsonNodeOptions, cancellationToken: cancellationToken))?.AsArray();
    }
}