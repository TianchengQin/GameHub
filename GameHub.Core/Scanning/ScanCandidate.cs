namespace GameHub.Core.Scanning;

public class ExeCandidate
{
    public string FullPath { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class ScanCandidate
{
    public string FolderPath { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public List<ExeCandidate> ExeCandidates { get; set; } = new();
    public string? BestExePath => ExeCandidates.Count > 0 ? ExeCandidates[0].FullPath : null;
    public bool IncludeByDefault => ExeCandidates.Count > 0;
}
