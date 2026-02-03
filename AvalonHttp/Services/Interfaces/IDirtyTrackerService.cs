using AvalonHttp.Models.CollectionAggregate;

namespace AvalonHttp.Services.Interfaces;

public interface IDirtyTrackerService
{
    string TakeSnapshot(ApiRequest request);
    bool IsDirty(ApiRequest request, string snapshot);
}