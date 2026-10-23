namespace GameHub.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
}
