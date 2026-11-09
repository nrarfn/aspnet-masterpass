namespace AspNet.MasterPass;

/// <summary>
/// Shared constants used by the package and by application code.
/// </summary>
public static class MasterPassDefaults
{
    /// <summary>
    /// Claim type added to the authentication ticket when the session started with the master password.
    /// </summary>
    public const string ClaimType = "masterpass";

    /// <summary>
    /// Authorization policy that rejects sessions created with the master password.
    /// </summary>
    public const string RequireRealPasswordPolicy = "MasterPass.RequireRealPassword";
}