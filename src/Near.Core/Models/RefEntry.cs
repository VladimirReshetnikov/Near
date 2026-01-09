using System;

namespace Near.Core.Models;

public sealed record RefEntry(
    string Name,
    string FullName,
    string TargetHash,
    DateTimeOffset? Date,
    string? Upstream,
    int? Ahead,
    int? Behind
);

public sealed record StashEntry(
    string Name,
    DateTimeOffset Date,
    string Message
);
