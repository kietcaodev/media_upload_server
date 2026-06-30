using MediaUpload.Domain.Entities;

namespace MediaUpload.Domain.Interfaces;

public interface IUploadJobRepository
{
    Task<UploadJob?> GetByIdAsync(Guid id);
    Task<List<UploadJob>> GetPendingJobsAsync(int limit = 50);
    Task<List<UploadJob>> GetJobsAsync(int page, int pageSize, Domain.Enums.JobStatus? status);
    Task<int> CountAsync(Domain.Enums.JobStatus? status = null);
    Task AddAsync(UploadJob job);
    Task UpdateAsync(UploadJob job);
    Task DeleteAsync(Guid id);
    Task ResetStuckJobsAsync(); // Running → Pending on startup
}

public interface ITimeWindowConfigRepository
{
    Task<List<TimeWindowConfig>> GetAllAsync();
    Task<TimeWindowConfig?> GetByIdAsync(int id);
    Task AddAsync(TimeWindowConfig config);
    Task UpdateAsync(TimeWindowConfig config);
    Task DeleteAsync(int id);
}

public interface IErpEndpointConfigRepository
{
    Task<List<ErpEndpointConfig>> GetAllAsync();
    Task<ErpEndpointConfig?> GetByTargetAsync(string target);
    Task UpsertAsync(ErpEndpointConfig config);
}

public interface IApiCredentialRepository
{
    Task<List<ApiCredential>> GetAllAsync();
    Task<ApiCredential?> GetByIdAsync(int id);
    Task<List<ApiCredential>> GetByAuthTypeAsync(Domain.Enums.AuthType authType);
    Task AddAsync(ApiCredential credential);
    Task UpdateAsync(ApiCredential credential);
    Task DeleteAsync(int id);
}

public interface ISystemSettingRepository
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task<List<SystemSetting>> GetAllAsync();
    Task ResetToDefaultAsync(string key);
}
