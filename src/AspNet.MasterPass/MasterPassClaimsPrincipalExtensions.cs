using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AspNet.MasterPass;

/// <summary>
/// Convenience helpers for reading the master pass claim.
/// </summary>
public static class MasterPassClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> when the principal was created by a master password sign-in.
    /// </summary>
    /// <param name="principal">The current user.</param>
    public static bool IsMasterPassLogin(this ClaimsPrincipal? principal) =>
        principal?.HasClaim(MasterPassDefaults.ClaimType, "true") == true;

    /// <summary>
    /// Returns <see langword="true"/> when the current request signed in with the master password.
    /// </summary>
    /// <param name="httpContext">The current request, or <see langword="null"/>.</param>
    public static bool IsMasterPassLogin(this HttpContext? httpContext) =>
        httpContext?.User.IsMasterPassLogin() == true;
}