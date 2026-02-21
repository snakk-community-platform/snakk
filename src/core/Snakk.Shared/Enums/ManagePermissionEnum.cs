using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManagePermissionEnum
{
    ViewDashboard = 1,
    ManageContent = 2,
    ManageReports = 3,
    ManageBans = 4,
    ManageSettings = 5,
    ManageTeam = 6,
    ManageWebhooks = 7
}
