namespace AspNet.MasterPass;

/// <summary>
/// Request-scoped state that remembers whether the current sign-in used the master password.
/// </summary>
/// <remarks>
/// The framework fills this in when a password check succeeds through the master password.
/// Application code normally reads the claim instead of this type.
/// </remarks>
public sealed class MasterPassContext
{
    /// <summary>
    /// Gets a value indicating whether the current request signed in with the master password.
    /// </summary>
    public bool IsMasterPassUsed => UsedAt is not null;

    /// <summary>
    /// Gets the moment the master password was accepted.
    /// </summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>
    /// Marks the current request as authenticated with the master password.
    /// </summary>
    internal void MarkUsed() => UsedAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// Clears a previous mark so every password check starts from a clean state.
    /// </summary>
    internal void Reset() => UsedAt = null;
}