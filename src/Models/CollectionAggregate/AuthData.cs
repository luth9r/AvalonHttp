namespace AvalonHttp.Models.CollectionAggregate;

public class AuthData
{
    public string Type { get; set; } = "None";
    public string BasicUsername { get; set; } = "";
    public string BasicPassword { get; set; } = "";
    public string BearerToken { get; set; } = "";
    public string ApiKeyName { get; set; } = "";
    public string ApiKeyValue { get; set; } = "";
    public string ApiKeyLocation { get; set; } = "Header";
}