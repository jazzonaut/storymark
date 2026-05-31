using System.Threading.Channels;
using Npgsql;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Infrastructure.Settings;

/// <summary>Holds a dedicated connection on <c>LISTEN settings_changed</c> and
/// refreshes the settings cache when the DB trigger fires pg_notify — including
/// for edits made directly with psql.</summary>
internal sealed class SettingsChangeListener(
    IConfiguration configuration,
    ISettingsService settings,
    ILogger<SettingsChangeListener> logger) : BackgroundService
{
    private const string ReloadAllToken = "*";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        var channel = Channel.CreateUnbounded<string>();
        var consumer = ConsumeAsync(channel.Reader, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                connection.Notification += (_, args) =>
                    channel.Writer.TryWrite(string.IsNullOrEmpty(args.Payload) ? ReloadAllToken : args.Payload);

                await using (var command = new NpgsqlCommand("LISTEN settings_changed", connection))
                {
                    await command.ExecuteNonQueryAsync(stoppingToken);
                }

                logger.LogInformation("Listening for settings_changed notifications");
                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "settings_changed listener dropped; reconnecting in 2s");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        channel.Writer.TryComplete();
        await consumer;
    }

    private async Task ConsumeAsync(ChannelReader<string> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var key in reader.ReadAllAsync(ct))
            {
                if (string.Equals(key, ReloadAllToken, StringComparison.Ordinal))
                {
                    await settings.LoadAllAsync(ct);
                }
                else
                {
                    await settings.RefreshKeyAsync(key, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // host shutdown
        }
    }
}
