using YamlDotNet.Serialization;

namespace Kart.User.ContractTests;

/// <summary>Loads the vendored <c>contracts/api-contract.yaml</c> once per test class into a
/// loosely-typed dictionary tree, so contract-shape assertions can navigate it directly
/// (<c>paths./v1/users/{userId}.get.responses.200</c>, etc.) without a full OpenAPI model.</summary>
public static class ApiContractDocument
{
    private static readonly Lazy<Dictionary<object, object>> Root = new(Load);

    public static Dictionary<object, object> Paths => (Dictionary<object, object>)Root.Value["paths"];

    public static Dictionary<object, object> Components => (Dictionary<object, object>)Root.Value["components"];

    private static Dictionary<object, object> Load()
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "api-contract.yaml"));
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
