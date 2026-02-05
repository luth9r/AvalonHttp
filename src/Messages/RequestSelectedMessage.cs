using AvalonHttp.Models.CollectionAggregate;

namespace AvalonHttp.Messages;

/// <summary>
/// Message sent when a request is selected.
/// </summary>
public record RequestSelectedMessage(ApiRequest Request);