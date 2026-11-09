using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNet.MasterPass.Internal;

/// <summary>
/// Decorates the registered <see cref="IPasswordHasher{TUser}"/>. The real password is verified
/// first; only when it fails is the master password considered. That keeps normal sign-in
/// untouched and never flags a session that used the account's own password.
/// </summary>
internal sealed class MasterPassPasswordHasher<TUser> : IPasswordHasher<TUser>
    where TUser : class
{
    private readonly IPasswordHasher<TUser> _inner;
    private readonly IOptionsMonitor<MasterPassOptions> _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _rootServices;
    private readonly IHostEnvironment _environment;

    public MasterPassPasswordHasher(
        IPasswordHasher<TUser> inner,
        IOptionsMonitor<MasterPassOptions> options,
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory scopeFactory,
        IServiceProvider rootServices,
        IHostEnvironment environment)
    {
        _inner = inner;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
        _scopeFactory = scopeFactory;
        _rootServices = rootServices;
        _environment = environment;
    }

    public string HashPassword(TUser user, string password) => _inner.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        var realResult = _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        if (realResult != PasswordVerificationResult.Failed)
        {
            return realResult;
        }

        var options = _options.CurrentValue;
        if (!options.Enabled || string.IsNullOrWhiteSpace(providedPassword))
        {
            return realResult;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        using var lease = new ServiceLease(httpContext?.RequestServices, _scopeFactory, _rootServices);
        var services = lease.Services;

        var masterPassContext = services.GetService<MasterPassContext>();
        masterPassContext?.Reset();

        var attempt = new MasterPassAttemptContext
        {
            User = user,
            HttpContext = httpContext
        };

        var secret = services.GetService<IMasterPassSecretProvider>()?.GetMasterPassword(attempt);
        if (string.IsNullOrEmpty(secret))
        {
            return realResult;
        }

        if (!MasterPassSecretComparer.Matches(providedPassword, secret))
        {
            return realResult;
        }

        var auditSink = services.GetService<IMasterPassAuditSink>();

        if (!options.IsEnvironmentAllowed(_environment.EnvironmentName))
        {
            auditSink?.Write(new MasterPassAuditEvent
            {
                Outcome = MasterPassAuditOutcome.BlockedByEnvironment,
                User = user,
                RemoteIpAddress = attempt.RemoteIpAddress,
                Reason = options.BlockedByEnvironmentReason
            });

            return realResult;
        }

        if (options.AllowForUser is not null && !options.AllowForUser(attempt))
        {
            auditSink?.Write(new MasterPassAuditEvent
            {
                Outcome = MasterPassAuditOutcome.Denied,
                User = user,
                RemoteIpAddress = attempt.RemoteIpAddress,
                Reason = options.DeniedReason
            });

            return realResult;
        }

        masterPassContext?.MarkUsed();
        auditSink?.Write(new MasterPassAuditEvent
        {
            Outcome = MasterPassAuditOutcome.Granted,
            User = user,
            RemoteIpAddress = attempt.RemoteIpAddress
        });

        return PasswordVerificationResult.Success;
    }

    /// <summary>
    /// Borrows the request scope when one exists, otherwise creates and disposes a short-lived
    /// scope so scoped services can be resolved from a singleton decorator.
    /// </summary>
    private readonly struct ServiceLease : IDisposable
    {
        private readonly IServiceScope? _scope;

        public ServiceLease(IServiceProvider? requestServices, IServiceScopeFactory scopeFactory, IServiceProvider rootServices)
        {
            if (requestServices is not null)
            {
                Services = requestServices;
                _scope = null;
                return;
            }

            _scope = scopeFactory.CreateScope();
            Services = _scope.ServiceProvider;
        }

        public IServiceProvider Services { get; }

        public void Dispose() => _scope?.Dispose();
    }
}