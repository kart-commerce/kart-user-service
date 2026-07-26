using System.Text.Json;

namespace Kart.User.Infrastructure.Messaging;

/// <summary>Fail-fast JSON load of <c>contracts/message-bus-manifest.json</c> — a missing or
/// malformed manifest is a startup-time configuration error, not something to silently ignore.</summary>
public static class MessageBusManifestLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static MessageBusManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"message-bus manifest not found at '{path}'. This service cannot declare its RabbitMQ topology without it.",
                path);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<MessageBusManifest>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"message-bus manifest at '{path}' deserialized to null.");
    }
}
