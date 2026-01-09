using System.Collections.Generic;

namespace Near.Core.Models;

public sealed record DiffDocument(IReadOnlyList<DiffFile> Files);

public sealed record DiffFile(
    string OldPath,
    string NewPath,
    IReadOnlyList<DiffHunk> Hunks,
    DiffStats? Stats
);

public sealed record DiffStats(int Added, int Deleted);

public sealed record DiffHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    IReadOnlyList<DiffLine> Lines
);

public enum DiffLineKind
{
    Context,
    Add,
    Remove,
    Meta
}

public sealed record DiffLine(DiffLineKind Kind, string Text);
