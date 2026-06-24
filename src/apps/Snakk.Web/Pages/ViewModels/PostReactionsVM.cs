namespace Snakk.Web.Pages.ViewModels;

public record PostReactionsVM(
    string PostPublicId,
    Dictionary<string, int> Counts,
    List<string> MyReactions,
    bool IsOp,
    bool IsAuthenticated,
    bool IsLocked)
{
    private static readonly Dictionary<string, string> EmojiMap = new()
    {
        ["Agree"] = "👍", ["Love"] = "❤️", ["Funny"] = "😂",
        ["Thinking"] = "🤔", ["Watching"] = "👀", ["Fire"] = "🔥",
        ["Thanks"] = "🙏", ["MindBlown"] = "🤯", ["ShipIt"] = "🚀"
    };

    public bool HasReactions => Counts.Any(kvp => kvp.Value > 0);
    public bool CanReact => IsAuthenticated && !IsLocked;
    public string CountsJson => System.Text.Json.JsonSerializer.Serialize(Counts);
    public string MyReactionsJson => System.Text.Json.JsonSerializer.Serialize(MyReactions);
    public string EmojiFor(string type) => EmojiMap.GetValueOrDefault(type, "?");
}
