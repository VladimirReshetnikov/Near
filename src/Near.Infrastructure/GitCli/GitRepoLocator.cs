using System;
using System.Threading;
using System.Threading.Tasks;
using Near.Core.Models;
using Near.Services.Git;

namespace Near.Infrastructure.GitCli;

public sealed class GitRepoLocator : IGitRepoLocator
{
    private readonly IGitProcessRunner _runner;

    public GitRepoLocator(IGitProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task<RepoContext?> LocateAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var rootResult = await _runner.RunAsync(
            new GitProcessRequest("rev-parse --show-toplevel", path),
            cancellationToken).ConfigureAwait(false);

        if (rootResult.ExitCode != 0)
        {
            return null;
        }

        var repoRoot = rootResult.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return null;
        }

        return await BuildRepoContextAsync(repoRoot, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RepoContext?> BuildRepoContextAsync(string repoRoot, CancellationToken cancellationToken)
    {
        var gitDirResult = await _runner.RunAsync(
            new GitProcessRequest("rev-parse --git-dir", repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (gitDirResult.ExitCode != 0)
        {
            return null;
        }

        var isBareResult = await _runner.RunAsync(
            new GitProcessRequest("rev-parse --is-bare-repository", repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (isBareResult.ExitCode != 0)
        {
            return null;
        }

        var statusResult = await _runner.RunAsync(
            new GitProcessRequest("status --porcelain=v2 --branch -z", repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (statusResult.ExitCode != 0)
        {
            return null;
        }

        var parsedStatus = GitOutputParser.ParseStatusOutput(statusResult.StandardOutput);
        var gitDir = gitDirResult.StandardOutput.Trim();
        var isBare = string.Equals(isBareResult.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase);

        return new RepoContext(
            repoRoot,
            gitDir,
            isBare,
            parsedStatus.Head,
            parsedStatus.Upstream,
            parsedStatus.Summary,
            DateTimeOffset.UtcNow);
    }
}
