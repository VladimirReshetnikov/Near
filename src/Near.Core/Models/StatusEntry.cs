namespace Near.Core.Models;

public enum StatusGroup
{
    Conflicts,
    Unstaged,
    Staged,
    Untracked,
    Ignored
}

public sealed record StatusEntry(
    StatusGroup Group,
    string Path,
    string? SecondaryPath,
    string Code,
    bool IsSubmodule
);
