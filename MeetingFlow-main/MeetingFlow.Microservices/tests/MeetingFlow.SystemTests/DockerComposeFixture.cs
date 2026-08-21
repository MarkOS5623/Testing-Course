using System.Diagnostics;

namespace MeetingFlow.SystemTests;

// Drives `docker compose up`/`down` for the whole deployed stack (Postgres, RabbitMQ,
// and all seven services) around the one system test. This is deliberately heavier and
// slower than the component/integration fixtures — it owns the same environment Part 0
// had you bring up by hand, so this test proves the deployed system works together, not
// just one boundary of it.
public class DockerComposeFixture : IAsyncLifetime
{
    static readonly string ComposeDirectory = FindComposeDirectory();
    static readonly HttpClient Gateway = new() { BaseAddress = new Uri("http://localhost:8080") };

    public async Task InitializeAsync()
    {
        await RunComposeAsync("up -d --build --wait", TimeSpan.FromMinutes(15));
        await WaitForGatewayReadyAsync(TimeSpan.FromMinutes(2));
    }

    public async Task DisposeAsync()
    {
        await RunComposeAsync("down", TimeSpan.FromMinutes(3));
    }

    static async Task WaitForGatewayReadyAsync(TimeSpan timeout)
    {
        // `--wait` only confirms containers are running/healthy at the Docker level.
        // The ASP.NET apps inside still need to finish EF EnsureCreated + seeding, and
        // Gateway's own /health doesn't check its downstream dependencies — so poll an
        // endpoint that actually round-trips through MeetingsManager and DataAccessor,
        // the same one Part 0 asks you to verify by hand.
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await Gateway.GetAsync("/meetings");
                if (response.IsSuccessStatusCode) return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(2000);
        }

        throw new TimeoutException(
            $"Gateway did not become ready within {timeout}.", lastError);
    }

    static async Task RunComposeAsync(string arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo("docker", $"compose {arguments}")
        {
            WorkingDirectory = ComposeDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the docker CLI.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(cts.Token);

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'docker compose {arguments}' exited with code {process.ExitCode}.\n" +
                $"--- stdout ---\n{stdOut}\n--- stderr ---\n{stdErr}");
        }
    }

    static string FindComposeDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find docker-compose.yml above {AppContext.BaseDirectory}.");
    }
}
