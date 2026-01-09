using System;

namespace Near.Core.Models;

public sealed record RepoContext(
    string RepoRoot,
    string GitDir,
    bool IsBare,
    HeadInfo Head,
    UpstreamInfo? Upstream,
    DirtySummary DirtySummary,
    DateTimeOffset LastRefreshedAt
);

public sealed record HeadInfo(
    string HeadRefName,
    string HeadCommitHash,
    bool IsDetached
);

public sealed record UpstreamInfo(
    string UpstreamRefName,
    int Ahead,
    int Behind
);

public sealed record DirtySummary(
    int Conflicts,
    int Unstaged,
    int Staged,
    int Untracked,
    int Ignored
);
