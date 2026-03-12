namespace SreAgent.Repository.Repositories;

public class DashboardStatsResult
{
    public int TotalSessionsToday { get; set; }
    public double AutoResolutionRate { get; set; }
    public int AvgProcessingTimeSeconds { get; set; }
    public int PendingApprovals { get; set; }
}
