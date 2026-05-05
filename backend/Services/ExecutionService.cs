using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.SignalR;
using backend.Hubs;
using System.Text;

namespace backend.Services;

// Spins up a throwaway Docker container for each code submission.
// The user's code never runs directly on our server — always inside
// an isolated container with strict CPU, memory and time limits.
public class ExecutionService
{
    private readonly DockerClient _docker;
    private readonly IHubContext<ExecutionHub> _hub;

    // Each language maps to an official Docker image
    private static readonly Dictionary<string, (string Image, string Extension, string Command)> Languages = new()
    {
        ["python"]     = ("python:3.12-alpine",    "py",  "python"),
        ["javascript"] = ("node:20-alpine",         "js",  "node"),
        ["java"]       = ("openjdk:21-slim",        "java","java"),
        ["csharp"]     = ("mcr.microsoft.com/dotnet/script:8.0", "csx", "dotnet-script"),
    };

    public ExecutionService(IHubContext<ExecutionHub> hub)
    {
        _hub = hub;
        _docker = new DockerClientConfiguration().CreateClient();
    }

    public async Task<string> ExecuteAsync(string submissionId, string code, string language)
    {
        if (!Languages.TryGetValue(language, out var config))
            throw new ArgumentException($"Unsupported language: {language}");

        await _hub.Clients.Group(submissionId).SendAsync("executionStarted");

        // Write code to a temp file — container will run this file
        var tmpDir = Path.Combine(Path.GetTempPath(), submissionId);
        Directory.CreateDirectory(tmpDir);
        var codeFile = Path.Combine(tmpDir, $"solution.{config.Extension}");
        await File.WriteAllTextAsync(codeFile, code);

        var output = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            // Pull image if not present locally
            await _hub.Clients.Group(submissionId).SendAsync("outputLine", $"Preparing {language} environment...");
            await _docker.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = config.Image, Tag = "latest" },
                null, new Progress<JSONMessage>());

            // Create container with strict resource limits
            var container = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = config.Image,
                Cmd = [config.Command, $"/code/solution.{config.Extension}"],
                HostConfig = new HostConfig
                {
                    Binds = [$"{tmpDir}:/code:ro"],   // mount code as read-only
                    Memory = 128 * 1024 * 1024,        // 128MB RAM limit
                    NanoCPUs = 500_000_000,            // 0.5 CPU limit
                    NetworkMode = "none",              // no internet access
                    AutoRemove = true,                 // delete container when done
                },
                AttachStdout = true,
                AttachStderr = true,
            });

            await _docker.Containers.StartContainerAsync(container.ID, null);
            await _hub.Clients.Group(submissionId).SendAsync("outputLine", "Running...\n");

            // Stream logs back to browser in real time
            var logs = await _docker.Containers.GetContainerLogsAsync(container.ID, false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = true });

            using var reader = new StreamReader(logs);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10s hard limit
            var lineBuffer = new StringBuilder();

            try
            {
                while (!reader.EndOfStream && !cts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cts.Token);
                    if (line is null) break;
                    // Docker log stream has 8-byte header per line — strip it
                    var clean = line.Length > 8 ? line[8..] : line;
                    output.AppendLine(clean);
                    await _hub.Clients.Group(submissionId).SendAsync("outputLine", clean);
                }
            }
            catch (OperationCanceledException)
            {
                var timeout = "Execution timed out (10s limit exceeded)";
                output.AppendLine(timeout);
                await _hub.Clients.Group(submissionId).SendAsync("outputLine", timeout);
            }
        }
        finally
        {
            // Always clean up temp files
            Directory.Delete(tmpDir, recursive: true);
        }

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var summary = $"\nExecution completed in {elapsed:F0}ms";
        await _hub.Clients.Group(submissionId).SendAsync("executionComplete", summary);

        return output.ToString();
    }
}
