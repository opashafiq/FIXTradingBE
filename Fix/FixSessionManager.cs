
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;

namespace TradingBackend.Api.Fix;

public class FixSessionManager : IHostedService
{
    private readonly FixApplication _application;
    private readonly ILogger<FixSessionManager> _logger;

    private SocketInitiator? _initiator;

    public FixSessionManager(
        FixApplication application,
        ILogger<FixSessionManager> logger)
    {
        _application = application;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            string configPath = Path.Combine(
                AppContext.BaseDirectory,
                "Configuration",
                "fix-uat.cfg"
            );

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(
                    "FIX configuration file was not found.",
                    configPath
                );
            }

            _logger.LogInformation(
                "Loading FIX configuration from {ConfigPath}",
                configPath
            );

            var settings = new SessionSettings(configPath);

            var storeFactory = new FileStoreFactory(settings);
            var logFactory = new FileLogFactory(settings);

            _initiator = new SocketInitiator(
                _application,
                storeFactory,
                settings,
                logFactory
            );

            _initiator.Start();

            _logger.LogInformation(
                "QuickFIX/n initiator started successfully."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start the FIX initiator."
            );

            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_initiator != null)
        {
            _logger.LogInformation(
                "Stopping QuickFIX/n initiator."
            );

            _initiator.Stop();
            _initiator = null;
        }

        return Task.CompletedTask;
    }
}
