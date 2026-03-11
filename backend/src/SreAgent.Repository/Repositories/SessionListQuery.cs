namespace SreAgent.Repository.Repositories;

public class SessionListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public string? Source { get; set; }
    public string? Sort { get; set; } = "createdAt";
    public string? SortOrder { get; set; } = "desc";
    public string? Search { get; set; }
}
