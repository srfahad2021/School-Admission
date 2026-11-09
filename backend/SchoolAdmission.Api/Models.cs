namespace SchoolAdmission.Api;

public static class HoldStatus
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}

public class SchoolClass
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Hold
{
    public string Id { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string Status { get; set; } = HoldStatus.Held;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public record HoldOptions(int DurationSeconds);

public record CreateClassRequest(string? Name, int? TotalSeats);

public record ClassView(string Id, string Name, int TotalSeats,
    int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}