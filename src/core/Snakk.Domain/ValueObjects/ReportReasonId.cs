namespace Snakk.Domain.ValueObjects;

public record ReportReasonId
{
    public string Value { get; }

    private ReportReasonId(string value)
    {
        Value = value;
    }

    public static ReportReasonId From(string value) => new(value);
    public static ReportReasonId New() => new(Ulid.NewUlid().ToString());
    public static implicit operator string(ReportReasonId id) => id.Value;
}
