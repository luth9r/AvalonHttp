using AvalonHttp.Models.CollectionAggregate;

namespace AvalonHttp.Services.Interfaces;

public interface IDirtyTrackerService
{
    string TakeSnapshot<T>(T obj);
    bool IsDirty<T>(T obj, string snapshot);
}