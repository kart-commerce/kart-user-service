namespace Kart.User.ContractTests;

public static class YamlDictionaryExtensions
{
    public static Dictionary<object, object> Map(this Dictionary<object, object> dict, string key) =>
        (Dictionary<object, object>)dict[key];

    public static bool HasKey(this Dictionary<object, object> dict, string key) => dict.ContainsKey(key);

    public static string Str(this Dictionary<object, object> dict, string key) => (string)dict[key];
}
