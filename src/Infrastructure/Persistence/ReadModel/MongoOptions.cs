namespace Kart.User.Infrastructure.Persistence.ReadModel;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "kart_user";
    public string CollectionName { get; set; } = "user_read_model";
}
