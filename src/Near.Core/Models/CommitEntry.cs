using System;
using System.Collections.Generic;

namespace Near.Core.Models;

public sealed record CommitEntry(
    string Hash,
    IReadOnlyList<string> Parents,
    string AuthorName,
    DateTimeOffset AuthorDate,
    string Subject,
    IReadOnlyList<string> Decorations
);
