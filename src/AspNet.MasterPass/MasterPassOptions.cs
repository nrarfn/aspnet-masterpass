using Microsoft.AspNetCore.Http;

namespace AspNet.MasterPass;

/// <summary>
/// Options that control the master password behaviour.
/// </summary>
/// <remarks>
/// The package is inert until a secret is available: with an empty <see cref="Password"/>
/// and no custom <see cref="IMasterPassSecretProvider"/> the hasher simply delegates to
/// the default ASP.NET Core Identity hasher.
/// </remarks>
public sealed class MasterPassOptions
{
    /// <summary>
    /// Configuration section name used by the <see cref="ServiceCollectionExtensions"/> overload
    /// that binds from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
    /// </summary>
    public const string SectionName = "MasterPass";

    /// <summary>
    /// Turns the feature on or off without removing the secret. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The master password. Either a plaintext secret or an ASP.NET Core Identity password hash
    /// (the base64 v3 format produced by <see cref="Microsoft.AspNetCore.Identity.IPasswordHasher{TUser}"/>).
    /// </summary>
    /// <remarks>
    /// Keep this in user secrets, environment variables or a vault, never in source control.
    /// </remarks>
    public string? Password { get; set; }

    /// <summary>
    /// Allows the master password to work while the host environment is Production.
    /// Defaults to <see langword="false"/>: the application fails at startup instead of
    /// silently shipping a backdoor.
    /// </summary>
    public bool AllowInProduction { get; set; }

    /// <summary>
    /// Optional per-user gate. Returning <see langword="false"/> rejects the master password for
    /// that account, which is how administrative accounts are protected.
    /// </summary>
    /// <example>
    /// <code>
    /// options.AllowForUser = attempt =>
    ///     attempt.User is IdentityUser user &amp;&amp; !user.UserName!.StartsWith("admin");
    /// </code>
    /// </example>
    public Func<MasterPassAttemptContext, bool>? AllowForUser { get; set; }

    /// <summary>
    /// The message returned in the audit event when <see cref="AllowForUser"/> denies an attempt.
    /// </summary>
    public string DeniedReason { get; set; } = "The master password is not allowed for this account.";

    /// <summary>
    /// The reason reported when the master password is disabled by the environment guard.
    /// </summary>
    public string BlockedByEnvironmentReason { get; set; } =
        "The master password is disabled outside of Development and Staging.";

    internal bool IsEnvironmentAllowed(string environmentName) =>
        AllowInProduction || !string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Everything the master password verification knows at the moment it runs.
/// </summary>
public sealed class MasterPassAttemptContext
{
    /// <summary>
    /// The user instance being signed in. Cast it to the application user type when needed.
    /// </summary>
    public object? User { get; init; }

    /// <summary>
    /// The current HTTP context, or <see langword="null"/> when verification runs outside a request.
    /// </summary>
    public HttpContext? HttpContext { get; init; }

    /// <summary>
    /// The remote IP address of the caller when one is available.
    /// </summary>
    public string? RemoteIpAddress => HttpContext?.Connection.RemoteIpAddress?.ToString();
}