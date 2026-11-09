namespace AspNet.MasterPass;

/// <summary>
/// Supplies the master password at verification time.
/// </summary>
/// <remarks>
/// Replace the default registration when the secret lives in a database, a vault or a
/// tenant-specific store. Methods are synchronous because
/// <see cref="Microsoft.AspNetCore.Identity.IPasswordHasher{TUser}.VerifyHashedPassword"/>
/// is synchronous; cache the lookup if it is expensive.
/// </remarks>
public interface IMasterPassSecretProvider
{
    /// <summary>
    /// Returns the master password for the current attempt, or <see langword="null"/> to disable it.
    /// </summary>
    /// <param name="attempt">The attempt being verified.</param>
    string? GetMasterPassword(MasterPassAttemptContext attempt);
}

/// <summary>
/// Receives one event per master password attempt that reaches the decision point.
/// </summary>
public interface IMasterPassAuditSink
{
    /// <summary>
    /// Records an attempt. Implementations must never log the password itself.
    /// </summary>
    /// <param name="auditEvent">What happened.</param>
    void Write(MasterPassAuditEvent auditEvent);
}

/// <summary>
/// The outcome of a master password attempt.
/// </summary>
public enum MasterPassAuditOutcome
{
    /// <summary>The master password was accepted and the session is flagged as a master pass session.</summary>
    Granted,

    /// <summary>The master password matched but <see cref="MasterPassOptions.AllowForUser"/> rejected the account.</summary>
    Denied,

    /// <summary>The master password matched but the environment guard blocked it.</summary>
    BlockedByEnvironment
}

/// <summary>
/// Describes a single master password attempt.
/// </summary>
public sealed class MasterPassAuditEvent
{
    /// <summary>
    /// Gets the decision that was taken.
    /// </summary>
    public required MasterPassAuditOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the user the attempt targeted. Cast it to the application user type when needed.
    /// </summary>
    public object? User { get; init; }

    /// <summary>
    /// Gets the remote IP address of the caller, when the attempt ran inside a request.
    /// </summary>
    public string? RemoteIpAddress { get; init; }

    /// <summary>
    /// Gets the reason attached to non-granted outcomes.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the UTC moment of the attempt.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}