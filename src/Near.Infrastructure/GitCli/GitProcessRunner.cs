using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Near.Services.Git;

namespace Near.Infrastructure.GitCli;

public sealed class GitProcessRunner : IGitProcessRunner
{
    private readonly string _gitPath;

    public GitProcessRunner(string gitPath = "git")
    {
        _gitPath = gitPath;
    }

    public async Task<GitProcessResult> RunAsync(GitProcessRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _gitPath,
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var output = new StringBuilder();
        var error = new StringBuilder();

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                error.AppendLine(args.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start git process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new GitProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }
}
