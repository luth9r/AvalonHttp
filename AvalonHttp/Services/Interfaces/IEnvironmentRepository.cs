using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Environment = AvalonHttp.Models.EnvironmentAggregate.Environment;

namespace AvalonHttp.Services.Interfaces;

public interface IEnvironmentRepository
{
    Task<List<Environment>> LoadAllAsync();
    Task SaveAsync(Environment environment);
    Task DeleteAsync(Guid environmentId);
    Task<Environment?> GetActiveEnvironmentAsync();
    Task SetActiveEnvironmentAsync(Guid? environmentId);
    
    Task<List<Environment>> EnsureDefaultEnvironmentsAsync();
}