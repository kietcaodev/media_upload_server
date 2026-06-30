namespace MediaUpload.Domain.Enums;

public enum AuthType
{
    Bearer = 0,
    Basic = 1,
    ApiKey = 2
}

public enum Permission
{
    Upload = 0,
    ReadJobs = 1,
    Config = 2
}
