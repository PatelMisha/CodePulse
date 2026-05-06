using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.SignalR;
using backend.Hubs;
using System.Text;

namespace backend.Services;

public class ExecutionService
{
    private readonly DockerClient _docker;
    private readonly IHubContext<ExecutionHub> _hub;

    private static readonly Dictionary<string, (string Image, string Extension, string Command)> Languages = new()
    {
        ["python"]     = ("python:3.12-alpine",  "py", "python"),
        ["javascript"] = ("node:20-alpine",       "js", "node"),
    };

    public ExecutionService(IHubContext<ExecutionHub> hub)
    {
        _hub = hub;

        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST")
            ?? "unix:///Users/mp/.colima/default/docker.sock";

        _docker = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
    }

    public async Task<string> ExecuteAsync(string submissionId, string code, string language)
    {
        if (!Languages.TryGetValue(language, out var config))
            throw new ArgumentException($"Unsupported language: {language}");

        await _hub.Clients.Group(submissionId).SendAsync("executionStarted");
        await _hub.Clients.Group(submissionId).SendAsync("outputLine", "Running...");

        // Use home directory — Colima mounts /Users automatically, unlike /tmp
        var tmpDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codepulse", submissionId);
        Directory.CreateDirectory(tmpDir);
        await File.WriteAllTextAsync(Path.Combine(tmpDir, $"solution.{config.Extension}"), code);

        var startTime = DateTime.UtcNow;
        string? containerId = null;

        try
        {
            var container = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = config.Image,
                Cmd = [config.Command, $"/code/solution.{config.Extension}"],
                HostConfig = new HostConfig
                {
                    Binds = [$"{tmpDir}:/code:ro"],
                    Memory = 128 * 1024 * 1024,
                    NanoCPUs = 500_000_000,
                    NetworkMode = "none",
                },
                AttachStdout = true,
                AttachStderr = true,
            });

            containerId = container.ID;

            await _docker.Containers.StartContainerAsync(containerId, null);

            // Wait for container to finish (max 10 seconds)
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _docker.Containers.WaitContainerAsync(containerId, cts.Token);

            // Read full output after container finishes
            var logs = await _docker.Containers.GetContainerLogsAsync(containerId, false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true });

            var output = new StringBuilder();
            var buffer = new byte[4096];

            while (true)
            {
                var result = await logs.ReadOutputAsync(buffer, 0, buffer.Length, CancellationToken.None);
                if (result.EOF) break;
                var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                output.Append(chunk);
            }

            var outputStr = output.ToString().Trim();

            foreach (var line in outputStr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                await _hub.Clients.Group(submissionId).SendAsync("outputLine", line);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            await _hub.Clients.Group(submissionId)
                .SendAsync("executionComplete", $"Completed in {elapsed:F0}ms");

            return outputStr;
        }
        finally
        {
            // Clean up container and temp files
            if (containerId != null)
            {
                try { await _docker.Containers.RemoveContainerAsync(containerId,
                    new ContainerRemoveParameters { Force = true }); } catch { }
            }
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }
}
