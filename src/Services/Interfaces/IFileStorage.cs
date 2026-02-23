using System;
using System.Threading.Tasks;

namespace AvalonHttp.Services.Interfaces;

public interface IFileStorage<T> where T : class, new()
{
    Task<T> LoadAsync();
    T Load();
    Task UpdateAsync(Action<T> updateAction);
    Task ClearAsync();
}