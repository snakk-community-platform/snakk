namespace Snakk.Application.Services;

using System.Collections.Frozen;
using System.Reflection;

public static class DisposableEmailValidator
{
    private static readonly FrozenSet<string> BlockedDomains = LoadBlockedDomains();

    private static FrozenSet<string> LoadBlockedDomains()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Snakk.Application.Data.disposable_email_blocklist.conf";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length > 0 && line[0] != '#')
                domains.Add(line);
        }

        return domains.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public static (bool IsValid, string? Error) Validate(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email is required.");

        var atIndex = email.IndexOf('@');
        if (atIndex < 0)
            return (false, "Invalid email address.");

        var domain = email[(atIndex + 1)..].Trim();

        if (BlockedDomains.Contains(domain))
            return (false, "Registration is not allowed with this email provider.");

        return (true, null);
    }

    public static bool IsDisposableDomain(string domain)
        => BlockedDomains.Contains(domain.Trim());
}
