using AvalonHttp.Models.CollectionAggregate;

namespace AvalonHttp.Messages;

/// <summary>
/// Message sent when a request is saved.
/// </summary>
public record RequestSavedMessage(ApiRequest Request);