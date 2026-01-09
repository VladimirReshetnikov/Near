using System;
using System.Threading;
using System.Threading.Tasks;
using Near.Core.Models;
using Near.Services.Git;

namespace Near.Infrastructure.GitCli;

public sealed class GitQueryService : IGitQueryService
{
    private readonly IGitProcessRunner _runner;

    public GitQueryService(IGitProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task<RepoContext> GetRepoContextAsync(string repoRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new ArgumentException("Repo root is required.", nameof(repoRoot));
        }

        var gitDirResult = await _runner.RunAsync(
            new GitProcessRequest("rev-parse --git-dir", repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (gitDirResult.ExitCode != 0)
        {
            throw new InvalidOperationException(gitDirResult.StandardError);
        }

        var isBareResult = await _runner.RunAsync(
            new GitProcessRequest("rev-parse --is-bare-repository", repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (isBareResult.ExitCode != 0)
        {
            throw new InvalidOperationException(isBareResult.StandardError);
        }

        var statusResult = await _runner.RunAsync(
            new GitProcessRequest("status --porcelain=v2 --branch -z", repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (statusResult.ExitCode != 0)
        {
            throw new InvalidOperationException(statusResult.StandardError);
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

    public async Task<IReadOnlyList<StatusEntry>> GetStatusAsync(string repoRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new ArgumentException("Repo root is required.", nameof(repoRoot));
        }

        var statusResult = await _runner.RunAsync(
            new GitProcessRequest("status --porcelain=v2 --branch -z", repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (statusResult.ExitCode != 0)
        {
            throw new InvalidOperationException(statusResult.StandardError);
        }

        return GitOutputParser.ParseStatusOutput(statusResult.StandardOutput).Entries;
    }

    public async Task<DiffDocument> GetDiffAsync(DiffRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var arguments = request.Target switch
        {
            DiffTarget.WorkingTreeVsHead => "diff --no-color --patch",
            DiffTarget.IndexVsHead => "diff --cached --no-color --patch",
            DiffTarget.Commit => BuildCommitDiffArguments(request.Reference),
            DiffTarget.Stash => BuildStashDiffArguments(request.Reference),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Target), request.Target, null)
        };

        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            arguments = $"{arguments} -- \"{request.Path}\"";
        }

        var diffResult = await _runner.RunAsync(
            new GitProcessRequest(arguments, request.RepoRoot),
            cancellationToken).ConfigureAwait(false);

        if (diffResult.ExitCode != 0)
        {
            throw new InvalidOperationException(diffResult.StandardError);
        }

        return GitOutputParser.ParseDiffOutput(diffResult.StandardOutput);
    }

    public async Task<IReadOnlyList<CommitEntry>> GetLogPageAsync(LogRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var reference = string.IsNullOrWhiteSpace(request.Ref) ? "HEAD" : request.Ref;
        var arguments = "log " + reference +
                        " --date=iso-strict --pretty=format:%H%x1f%P%x1f%an%x1f%ad%x1f%s%x1f%D%x1e" +
                        $" --max-count={request.Take} --skip={request.Skip}";

        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            arguments += $" -- \"{request.Path}\"";
        }

        var logResult = await _runner.RunAsync(
            new GitProcessRequest(arguments, request.RepoRoot),
            cancellationToken).ConfigureAwait(false);

        if (logResult.ExitCode != 0)
        {
            throw new InvalidOperationException(logResult.StandardError);
        }

        return GitOutputParser.ParseLogOutput(logResult.StandardOutput);
    }

    public async Task<IReadOnlyList<RefEntry>> GetRefsAsync(string repoRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new ArgumentException("Repo root is required.", nameof(repoRoot));
        }

        var arguments = "for-each-ref refs/heads refs/remotes refs/tags " +
                        "--format=%(refname:short)%x1f%(refname)%x1f%(objectname)%x1f%(committerdate:iso-strict)%x1f%(upstream:short)%x1f%(ahead)%x1f%(behind)%x1e";

        var refResult = await _runner.RunAsync(
            new GitProcessRequest(arguments, repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (refResult.ExitCode != 0)
        {
            throw new InvalidOperationException(refResult.StandardError);
        }

        return GitOutputParser.ParseRefOutput(refResult.StandardOutput);
    }

    public async Task<IReadOnlyList<StashEntry>> GetStashesAsync(string repoRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new ArgumentException("Repo root is required.", nameof(repoRoot));
        }

        var arguments = "stash list --date=iso-strict --format=%gd%x1f%cd%x1f%gs%x1e";

        var stashResult = await _runner.RunAsync(
            new GitProcessRequest(arguments, repoRoot),
            cancellationToken).ConfigureAwait(false);

        if (stashResult.ExitCode != 0)
        {
            throw new InvalidOperationException(stashResult.StandardError);
        }

        return GitOutputParser.ParseStashOutput(stashResult.StandardOutput);
    }

    private static string BuildCommitDiffArguments(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return "show --no-color --patch";
        }

        return $"show --no-color --patch {reference}";
    }

    private static string BuildStashDiffArguments(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return "stash show -p --no-color";
        }

        return $"stash show -p --no-color {reference}";
    }
}
