namespace Snakk.Web.Helpers;

public static class DiscussionTypeHelper
{
    public static (string Icon, string Label, string CssClass) GetTypeBadge(string type) =>
        type switch
        {
            "Question" => ("❓", "Question", "type-question"),
            "Poll" => ("📊", "Poll", "type-poll"),
            "Announcement" => ("📢", "Announcement", "type-announcement"),
            "Link" => ("🔗", "Link", "type-link"),
            "Images" => ("🖼️", "Images", "type-images"),
            "Guide" => ("📖", "Guide", "type-guide"),
            "Debate" => ("⚖️", "Debate", "type-debate"),
            "Journal" => ("📓", "Journal", "type-journal"),
            "Iama" => ("🎤", "AMA", "type-iama"),
            _ => ("", type, "type-standard")
        };

    public static string GetTypeName(int type) =>
        type switch
        {
            0 => "Standard",
            1 => "Question",
            2 => "Poll",
            3 => "Announcement",
            4 => "Link",
            5 => "Images",
            6 => "Guide",
            7 => "Debate",
            8 => "Journal",
            9 => "Iama",
            _ => "Standard"
        };
}
