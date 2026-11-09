using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace AspNet.MasterPass.Internal;

/// <summary>
/// Requirement satisfied only by sessions that authenticated with the account's own password.
/// </summary>
internal sealed class RequireRealPasswordRequirement : IAuthorizationRequirement
{
    public static RequireRealPasswordRequirement Instance { get; } = new();

    private RequireRealPasswordRequirement()
    {
    }
}

/// <summary>
/// Fails any policy that carries <see cref="RequireRealPasswordRequirement"/> when the current
/// session was created with the master password.
/// </summary>
internal sealed class RequireRealPasswordHandler : AuthorizationHandler<RequireRealPasswordRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireRealPasswordRequirement requirement)
    {
        if (context.User.IsMasterPassLogin())
        {
            context.Fail(new AuthorizationFailureReason(
                this,
                "This action is not available to sessions signed in with the master password."));
        }
        else
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Default secret provider: reads <see cref="MasterPassOptions.Password"/> so configuration
/// reloads are picked up without a restart.
/// </summary>
internal sealed class ConfigurationMasterPassSecretProvider : IMasterPassSecretProvider
{
    private readonly Microsoft.Extensions.Options.IOptionsMonitor<MasterPassOptions> _options;

    public ConfigurationMasterPassSecretProvider(
        Microsoft.Extensions.Options.IOptionsMonitor<MasterPassOptions> options) => _options = options;

    public string? GetMasterPassword(MasterPassAttemptContext attempt) => _options.CurrentValue.Password;
}

/// <summary>
/// Default audit sink: logs grants at warning level so they surface in normal application logs.
/// </summary>
internal sealed class LoggingMasterPassAuditSink : IMasterPassAuditSink
{
    private readonly ILogger<LoggingMasterPassAuditSink> _logger;

    public LoggingMasterPassAuditSink(ILogger<LoggingMasterPassAuditSink> logger) => _logger = logger;

    public void Write(MasterPassAuditEvent auditEvent)
    {
        switch (auditEvent.Outcome)
        {
            case MasterPassAuditOutcome.Granted:
                _logger.LogWarning(
                    "Master password sign-in granted for user {UserId} from {RemoteIpAddress}.",
                    auditEvent.User,
                    auditEvent.RemoteIpAddress);
                break;
            case MasterPassAuditOutcome.Denied:
                _logger.LogInformation(
                    "Master password sign-in denied for user {UserId} from {RemoteIpAddress}: {Reason}",
                    auditEvent.User,
                    auditEvent.RemoteIpAddress,
                    auditEvent.Reason);
                break;
            default:
                _logger.LogInformation(
                    "Master password blocked by the environment guard for user {UserId} from {RemoteIpAddress}.",
                    auditEvent.User,
                    auditEvent.RemoteIpAddress);
                break;
        }
    }
}
