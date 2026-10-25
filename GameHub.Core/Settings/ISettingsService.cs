namespace GameHub.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    /// <summary>Writes settings to disk; returns false if the file could not be written.</summary>
    bool Save();
}
