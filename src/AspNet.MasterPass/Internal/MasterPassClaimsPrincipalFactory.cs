using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace AspNet.MasterPass.Internal;

/// <summary>
/// Decorates the registered claims factory so the authentication ticket created right after a
/// master password sign-in carries the <see cref="MasterPassDefaults.ClaimType"/> claim.
/// </summary>
internal sealed class MasterPassClaimsPrincipalFactory<TUser> : IUserClaimsPrincipalFactory<TUser>
    where TUser : class
{
    private readonly IUserClaimsPrincipalFactory<TUser> _inner;
    private readonly MasterPassContext _context;

    public MasterPassClaimsPrincipalFactory(
        IUserClaimsPrincipalFactory<TUser> inner,
        MasterPassContext context)
    {
        _inner = inner;
        _context = context;
    }

    public async Task<ClaimsPrincipal> CreateAsync(TUser user)
    {
        var principal = await _inner.CreateAsync(user).ConfigureAwait(false);

        if (!_context.IsMasterPassUsed)
        {
            return principal;
        }

        var claim = new Claim(MasterPassDefaults.ClaimType, "true");

        if (principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(claim);
        }
        else
        {
            principal.AddIdentity(new ClaimsIdentity(new[] { claim }));
        }

        return principal;
    }
}