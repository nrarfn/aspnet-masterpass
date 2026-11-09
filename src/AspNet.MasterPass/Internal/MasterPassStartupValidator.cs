using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNet.MasterPass.Internal;

/// <summary>
/// Resolves the options once during startup so the Production guard fails the application
/// immediately instead of at the first sign-in attempt.
/// </summary>
internal sealed class MasterPassStartupValidator : IHostedService
{
    private readonly IOptions<MasterPassOptions> _options;

    public MasterPassStartupValidator(IOptions<MasterPassOptions> options) => _options = options;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = _options.Value;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}