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
        ["python"]     = ("python:3.12-alpine",     "py",   "python"),
        ["javascript"] = ("node:20-alpine",          "js",   "node"),
        ["java"]       = ("openjdk:21-slim",         "java", "java"),
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

        var tmpDir = Path.Combine(Path.GetTempPath(), submissionId);
        Directory.CreateDirectory(tmpDir);
        var codeFile = Path.Combine(tmpDir, $"solution.{config.Extension}");
        await File.WriteAllTextAsync(codeFile, code);

        var output = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            await _hub.Clients.Group(submissionId)
                .SendAsync("outputLine", $"Preparing {language} environment...");

            await _docker.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = config.Image, Tag = "latest" },
                null, new Progress<JSONMessage>());

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
                    AutoRemove = true,
                },
                AttachStdout = true,
                AttachStderr = true,
            });

            await _docker.Containers.StartContainerAsync(container.ID, null);
            await _hub.Clients.Group(submissionId).SendAsync("outputLine", "Running...\n");

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // MultiplexedStream — read stdout and stderr in chunks
            var logs = await _docker.Containers.GetContainerLogsAsync(container.ID, false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = true });

            var buffer = new byte[4096];
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var result = await logs.ReadOutputAsync(buffer, 0, buffer.Length, cts.Token);
                    if (result.EOF) break;

                    var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count).TrimEnd('\r', '\n');
                    if (string.IsNullOrEmpty(chunk)) continue;

                    output.AppendLine(chunk);
                    await _hub.Clients.Group(submissionId).SendAsync("outputLine", chunk);
                }
            }
            catch (OperationCanceledException)
            {
                const string timeout = "Execution timed out (10s limit exceeded)";
                output.AppendLine(timeout);
                await _hub.Clients.Group(submissionId).SendAsync("outputLine", timeout);
            }
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var summary = $"Completed in {elapsed:F0}ms";
        await _hub.Clients.Group(submissionId).SendAsync("executionComplete", summary);

        return output.ToString();
    }
}
