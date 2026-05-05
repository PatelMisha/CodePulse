namespace backend.Models;

public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? Output { get; set; }
    public string? AIReview { get; set; }
    public string Status { get; set; } = "pending"; // pending, running, completed, failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SubmitRequest
{
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = "python";
}
