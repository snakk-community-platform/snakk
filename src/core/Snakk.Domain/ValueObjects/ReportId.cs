namespace Snakk.Domain.ValueObjects;

public record ReportId
{
    public string Value { get; }

    private ReportId(string value)
    {
        Value = value;
    }

    public static ReportId From(string value) => new(value);
    public static ReportId New() => new(Ulid.NewUlid().ToString());
    public static implicit operator string(ReportId id) => id.Value;
}
