using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using ConquerorServer.LocalBackend;
using ConquerorServer;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace frameworkApp
{
	internal class Program
	{
		static void Main(string[] args)
		{
			if (args.Length == 0 || (HasOption(args, "--skip-initial-call") && !HasOption(args, "--endpoint1") && !HasOption(args, "--endpoint2")))
			{
				RunRepro(args);
				return;
			}

			if (HasOption(args, "--server"))
			{
				RunServer(int.Parse(GetOption(args, "--port", "8017")));
				return;
			}

			RunClient(args);
		}

		private static void RunRepro(string[] clientArgs)
		{
			using (var server1 = CreateServer(8017, "server 1"))
			using (var server2 = CreateServer(8018, "server 2"))
			{
				server1.Start();
				server2.Start();
				Console.WriteLine("Health servers started on ports 8017 and 8018.");
				RunClient(clientArgs);
			}
		}

		private static void RunServer(int port)
		{
			using (var host = CreateServer(port, "server"))
			{
				Console.WriteLine("Health server listening on http://localhost:{0}/healthprobe", port);
				host.Run();
			}
		}

		private static IWebHost CreateServer(int port, string serverName)
		{
			return WebHost.CreateDefaultBuilder()
				.UseKestrel(options => options.Listen(IPAddress.Loopback, port))
				.Configure(app => app.Run(async (HttpContext context) =>
				{
					var traceParent = context.Request.Headers["traceparent"].ToString();
					Console.WriteLine("{0:O} {1} (port {2}): traceparent {3}", DateTime.UtcNow, serverName, port,
						string.IsNullOrEmpty(traceParent) ? "MISSING" : traceParent);
					await context.Response.WriteAsync("Healthy");
				}))
				.Build();
		}

		private static void RunClient(string[] args)
		{
			var server1 = new Uri(GetOption(args, "--endpoint1", "http://localhost:8017/healthprobe"));
			var server2 = new Uri(GetOption(args, "--endpoint2", "http://localhost:8018/healthprobe"));
			var skipInitialCall = HasOption(args, "--skip-initial-call");

			if (!skipInitialCall)
			{
				Console.WriteLine("Making health calls before dependency injection.");
				Activity.DefaultIdFormat = ActivityIdFormat.W3C;
				Activity.ForceDefaultIdFormat = true;
				var activity = new Activity("Initial health request");
				activity.Start();
				using (var client = new System.Net.Http.HttpClient())
				{
					try
					{
					client.GetAsync(server1).GetAwaiter().GetResult();
					}
					finally
					{
						activity.Stop();
					}
				}
			}

			var services = new ServiceCollection();
			services.AddSingleton<IHostingEnvironment>(new ReproHostingEnvironment
			{
				ApplicationName = typeof(Program).Assembly.GetName().Name,
				ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
				EnvironmentName = "Production"
			});
			services.AddHttpClient();
			Console.WriteLine("Configuring Application Insights telemetry and dependency tracking.");
			services.AddApplicationInsights();
			services.AddSingleton(new HealthProbeOptions(server1, server2));
			services.AddSingleton<HealthProbeBackgroundJob>();

			using (var provider = services.BuildServiceProvider())
			using (var stop = new ManualResetEventSlim())
			{
				var job = provider.GetRequiredService<HealthProbeBackgroundJob>();
				using (var cancellation = new CancellationTokenSource())
				{
					Console.CancelKeyPress += (sender, eventArgs) =>
					{
						eventArgs.Cancel = true;
						cancellation.Cancel();
						stop.Set();
					};
					job.StartAsync(cancellation.Token).GetAwaiter().GetResult();
					Console.WriteLine("DI configured. Making 3 calls to both servers every 5 seconds, then every 2 minutes. Press Ctrl+C to stop.");
					stop.Wait();
					job.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
				}
			}
		}

		private static bool HasOption(string[] args, string option) =>
			Array.Exists(args, arg => string.Equals(arg, option, StringComparison.OrdinalIgnoreCase));

		private static string GetOption(string[] args, string option, string defaultValue)
		{
			for (var i = 0; i < args.Length - 1; i++)
			{
				if (string.Equals(args[i], option, StringComparison.OrdinalIgnoreCase))
				{
					return args[i + 1];
				}
			}

			return defaultValue;
		}

		private sealed class ReproHostingEnvironment : IHostingEnvironment
		{
			public string EnvironmentName { get; set; }
			public string ApplicationName { get; set; }
			public string WebRootPath { get; set; }
			public string ContentRootPath { get; set; }
			public IFileProvider WebRootFileProvider { get; set; }
			public IFileProvider ContentRootFileProvider { get; set; }
		}
	}
}
