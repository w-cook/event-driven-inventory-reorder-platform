namespace InventoryReorderPlatform.Api.Security;

public static class AppRoles
{
    public const string Viewer = "Viewer";
    public const string Operator = "Operator";
    public const string Administrator = "Administrator";

    public static IReadOnlyList<string> All { get; } =
    [
        Viewer,
        Operator,
        Administrator
    ];

    public static string? Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return All.FirstOrDefault(
            candidate =>
                string.Equals(
                    candidate,
                    role.Trim(),
                    StringComparison.OrdinalIgnoreCase));
    }
}