using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ConquerorServer.LocalBackend
{
	internal sealed class HealthProbeBackgroundJob : IHostedService, IDisposable
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<HealthProbeBackgroundJob> _logger;
		private readonly HealthProbeOptions _options;
		private readonly SemaphoreSlim _runLock = new SemaphoreSlim(1, 1);
		private int _probeCount;
		private Timer _timer;
		private CancellationToken _stoppingToken;

		public HealthProbeBackgroundJob(IHttpClientFactory httpClientFactory, ILogger<HealthProbeBackgroundJob> logger, HealthProbeOptions options)
		{
			_httpClientFactory = httpClientFactory;
			_logger = logger;
			_options = options;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_stoppingToken = cancellationToken;
			_timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_timer?.Change(Timeout.Infinite, Timeout.Infinite);
			return Task.CompletedTask;
		}

		public void Dispose()
		{
			_timer?.Dispose();
			_runLock.Dispose();
		}

		private void OnTimerTick(object state)
		{
			_ = ExecuteAsync();
		}

		private async Task ExecuteAsync()
		{
			if (!await _runLock.WaitAsync(0).ConfigureAwait(false))
			{
				return;
			}

			try
			{
				if (_stoppingToken.IsCancellationRequested)
				{
					return;
				}

				var client = _httpClientFactory.CreateClient();
				await ProbeAsync(client, _options.Server1).ConfigureAwait(false);
				await ProbeAsync(client, _options.Server2).ConfigureAwait(false);
				if (Interlocked.Increment(ref _probeCount) == 3)
				{
					_timer.Change(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
				}
			}
			catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Health probe calls failed.");
			}
			finally
			{
				_runLock.Release();
			}
		}

		private async Task ProbeAsync(HttpClient client, Uri probeUri)
		{
			Activity.DefaultIdFormat = ActivityIdFormat.W3C;
			Activity.ForceDefaultIdFormat = true;
			var activity = new Activity("Periodic health probe");
			activity.Start();
			try
			{
				using (var response = await client.GetAsync(probeUri, _stoppingToken).ConfigureAwait(false))
				{
					response.EnsureSuccessStatusCode();
				}
			}
			finally
			{
				activity.Stop();
			}

			_logger.LogInformation("Health probe call to {ProbeUri} succeeded.", probeUri);
		}
	}

	internal sealed class HealthProbeOptions
	{
		public HealthProbeOptions(Uri server1, Uri server2)
		{
			Server1 = server1;
			Server2 = server2;
		}

		public Uri Server1 { get; }
		public Uri Server2 { get; }
	}
}
