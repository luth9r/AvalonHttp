using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvalonHttp.Models.CollectionAggregate;

namespace AvalonHttp.Services.Interfaces;

public interface ICollectionRepository
{
    Task<List<ApiCollection>> LoadAllAsync();
    Task<ApiCollection?> GetByIdAsync(Guid id);
    Task SaveAsync(ApiCollection collection);
    Task DeleteAsync(Guid collectionId);
}