namespace BookStore.UI.Common.Models;

public class ProblemDetails
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public Dictionary<string, object> Extensions { get; set; } = [];
}

public class ValidationProblemDetails : ProblemDetails
{
    public Dictionary<string, string[]> Errors { get; set; } = [];
}
