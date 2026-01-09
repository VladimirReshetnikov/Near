using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Near.Core.Models;

namespace Near.Services.Git;

public sealed record DiffRequest(
    string RepoRoot,
    string? Path,
    DiffTarget Target
);

public enum DiffTarget
{
    WorkingTreeVsHead,
    IndexVsHead,
    Commit,
    Stash
}

public sealed record LogRequest(
    string RepoRoot,
    string? Ref,
    int Skip,
    int Take,
    string? Path
);

public sealed record GitProcessRequest(
    string Arguments,
    string WorkingDirectory
);

public sealed record GitProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError
);

public interface IGitRepoLocator
{
    Task<RepoContext?> LocateAsync(string path, CancellationToken cancellationToken);
}

public interface IGitQueryService
{
    Task<RepoContext> GetRepoContextAsync(string repoRoot, CancellationToken cancellationToken);

    Task<IReadOnlyList<StatusEntry>> GetStatusAsync(string repoRoot, CancellationToken cancellationToken);

    Task<DiffDocument> GetDiffAsync(DiffRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommitEntry>> GetLogPageAsync(LogRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefEntry>> GetRefsAsync(string repoRoot, CancellationToken cancellationToken);

    Task<IReadOnlyList<StashEntry>> GetStashesAsync(string repoRoot, CancellationToken cancellationToken);
}

public interface IGitProcessRunner
{
    Task<GitProcessResult> RunAsync(GitProcessRequest request, CancellationToken cancellationToken);
}
