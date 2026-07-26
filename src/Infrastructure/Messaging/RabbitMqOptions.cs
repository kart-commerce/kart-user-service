namespace Kart.User.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
