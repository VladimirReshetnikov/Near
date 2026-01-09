using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Near.Core.Models;

namespace Near.Infrastructure.GitCli;

internal sealed record ParsedStatus(
    HeadInfo Head,
    UpstreamInfo? Upstream,
    DirtySummary Summary,
    IReadOnlyList<StatusEntry> Entries
);

internal static class GitOutputParser
{
    private static readonly Regex DiffHeaderRegex =
        new("@@ -(?<oldStart>\\d+)(,(?<oldCount>\\d+))? \\+(?<newStart>\\d+)(,(?<newCount>\\d+))? @@", RegexOptions.Compiled);

    public static ParsedStatus ParseStatusOutput(string output)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var entries = new List<StatusEntry>();
        string? headName = null;
        string? headHash = null;
        string? upstreamName = null;
        int? ahead = null;
        int? behind = null;

        var records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i];
            if (record.StartsWith("# ", StringComparison.Ordinal))
            {
                ParseBranchRecord(record, ref headName, ref headHash, ref upstreamName, ref ahead, ref behind);
                continue;
            }

            if (record.StartsWith("1 ", StringComparison.Ordinal) || record.StartsWith("u ", StringComparison.Ordinal))
            {
                var entry = ParseStandardEntry(record);
                if (entry is not null)
                {
                    entries.Add(entry);
                }

                continue;
            }

            if (record.StartsWith("2 ", StringComparison.Ordinal))
            {
                var secondary = i + 1 < records.Length ? records[i + 1] : null;
                var entry = ParseRenamedEntry(record, secondary);
                if (entry is not null)
                {
                    entries.Add(entry);
                }

                if (secondary is not null)
                {
                    i++;
                }

                continue;
            }

            if (record.StartsWith("? ", StringComparison.Ordinal))
            {
                entries.Add(new StatusEntry(StatusGroup.Untracked, record[2..], null, "?", false));
                continue;
            }

            if (record.StartsWith("! ", StringComparison.Ordinal))
            {
                entries.Add(new StatusEntry(StatusGroup.Ignored, record[2..], null, "!", false));
            }
        }

        var summary = new DirtySummary(
            Conflicts: entries.Count(e => e.Group == StatusGroup.Conflicts),
            Unstaged: entries.Count(e => e.Group == StatusGroup.Unstaged),
            Staged: entries.Count(e => e.Group == StatusGroup.Staged),
            Untracked: entries.Count(e => e.Group == StatusGroup.Untracked),
            Ignored: entries.Count(e => e.Group == StatusGroup.Ignored)
        );

        var head = new HeadInfo(
            HeadRefName: headName ?? "(unknown)",
            HeadCommitHash: headHash ?? string.Empty,
            IsDetached: string.Equals(headName, "(detached)", StringComparison.OrdinalIgnoreCase)
        );

        UpstreamInfo? upstream = null;
        if (!string.IsNullOrWhiteSpace(upstreamName))
        {
            upstream = new UpstreamInfo(upstreamName!, ahead ?? 0, behind ?? 0);
        }

        return new ParsedStatus(head, upstream, summary, entries);
    }

    public static IReadOnlyList<CommitEntry> ParseLogOutput(string output)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var entries = new List<CommitEntry>();
        var records = output.Split('\x1e', StringSplitOptions.RemoveEmptyEntries);
        foreach (var record in records)
        {
            var fields = record.Split('\x1f');
            if (fields.Length < 6)
            {
                continue;
            }

            var hash = fields[0];
            var parents = string.IsNullOrWhiteSpace(fields[1])
                ? Array.Empty<string>()
                : fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var author = fields[2];
            var date = ParseDate(fields[3]);
            var subject = fields[4];
            var decorations = string.IsNullOrWhiteSpace(fields[5])
                ? Array.Empty<string>()
                : fields[5].Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(decoration => decoration.Trim())
                    .ToArray();

            entries.Add(new CommitEntry(hash, parents, author, date, subject, decorations));
        }

        return entries;
    }

    public static IReadOnlyList<RefEntry> ParseRefOutput(string output)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var entries = new List<RefEntry>();
        var records = output.Split('\x1e', StringSplitOptions.RemoveEmptyEntries);
        foreach (var record in records)
        {
            var fields = record.Split('\x1f');
            if (fields.Length < 4)
            {
                continue;
            }

            var name = fields[0];
            var fullName = fields[1];
            var hash = fields[2];
            var date = ParseOptionalDate(fields.ElementAtOrDefault(3));
            var upstream = fields.ElementAtOrDefault(4);
            var ahead = ParseOptionalInt(fields.ElementAtOrDefault(5));
            var behind = ParseOptionalInt(fields.ElementAtOrDefault(6));

            entries.Add(new RefEntry(name, fullName, hash, date, string.IsNullOrWhiteSpace(upstream) ? null : upstream, ahead, behind));
        }

        return entries;
    }

    public static IReadOnlyList<StashEntry> ParseStashOutput(string output)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var entries = new List<StashEntry>();
        var records = output.Split('\x1e', StringSplitOptions.RemoveEmptyEntries);
        foreach (var record in records)
        {
            var fields = record.Split('\x1f');
            if (fields.Length < 3)
            {
                continue;
            }

            var name = fields[0];
            var date = ParseDate(fields[1]);
            var message = fields[2];
            entries.Add(new StashEntry(name, date, message));
        }

        return entries;
    }

    public static DiffDocument ParseDiffOutput(string output)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var files = new List<DiffFile>();
        DiffFileBuilder? currentFile = null;
        DiffHunkBuilder? currentHunk = null;

        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                if (currentFile is not null)
                {
                    files.Add(currentFile.Build());
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var oldPath = parts.Length > 2 ? TrimDiffPath(parts[2]) : string.Empty;
                var newPath = parts.Length > 3 ? TrimDiffPath(parts[3]) : string.Empty;
                currentFile = new DiffFileBuilder(oldPath, newPath);
                currentHunk = null;
                continue;
            }

            if (currentFile is null)
            {
                continue;
            }

            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                currentFile.OldPath = TrimDiffPath(line[4..]);
                continue;
            }

            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                currentFile.NewPath = TrimDiffPath(line[4..]);
                continue;
            }

            if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                var match = DiffHeaderRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var oldStart = int.Parse(match.Groups["oldStart"].Value, CultureInfo.InvariantCulture);
                var oldCount = ParseOptionalInt(match.Groups["oldCount"].Value) ?? 1;
                var newStart = int.Parse(match.Groups["newStart"].Value, CultureInfo.InvariantCulture);
                var newCount = ParseOptionalInt(match.Groups["newCount"].Value) ?? 1;

                currentHunk = new DiffHunkBuilder(oldStart, oldCount, newStart, newCount);
                currentFile.Hunks.Add(currentHunk);
                continue;
            }

            if (currentHunk is null)
            {
                continue;
            }

            if (line.StartsWith("+", StringComparison.Ordinal))
            {
                currentHunk.AddLine(DiffLineKind.Add, line[1..]);
                continue;
            }

            if (line.StartsWith("-", StringComparison.Ordinal))
            {
                currentHunk.AddLine(DiffLineKind.Remove, line[1..]);
                continue;
            }

            if (line.StartsWith("\\ No newline", StringComparison.Ordinal))
            {
                currentHunk.AddLine(DiffLineKind.Meta, line);
                continue;
            }

            if (line.StartsWith(" ", StringComparison.Ordinal))
            {
                currentHunk.AddLine(DiffLineKind.Context, line[1..]);
            }
        }

        if (currentFile is not null)
        {
            files.Add(currentFile.Build());
        }

        return new DiffDocument(files);
    }

    private static void ParseBranchRecord(
        string record,
        ref string? headName,
        ref string? headHash,
        ref string? upstream,
        ref int? ahead,
        ref int? behind)
    {
        var content = record[2..];
        var separator = content.IndexOf(' ');
        if (separator < 0)
        {
            return;
        }

        var key = content[..separator];
        var value = content[(separator + 1)..];
        switch (key)
        {
            case "branch.head":
                headName = value;
                break;
            case "branch.oid":
                headHash = value;
                break;
            case "branch.upstream":
                upstream = value;
                break;
            case "branch.ab":
                ParseAheadBehind(value, ref ahead, ref behind);
                break;
        }
    }

    private static void ParseAheadBehind(string value, ref int? ahead, ref int? behind)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith('+'))
            {
                ahead = ParseOptionalInt(part[1..]);
            }
            else if (part.StartsWith('-'))
            {
                behind = ParseOptionalInt(part[1..]);
            }
        }
    }

    private static StatusEntry? ParseStandardEntry(string record)
    {
        var parts = record.Split(' ', 9, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 9)
        {
            return null;
        }

        var xy = parts[1];
        var submodule = parts[2];
        var path = parts[8];

        return BuildStatusEntry(xy, submodule, path, null);
    }

    private static StatusEntry? ParseRenamedEntry(string record, string? secondaryPath)
    {
        var parts = record.Split(' ', 10, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 10)
        {
            return null;
        }

        var xy = parts[1];
        var submodule = parts[2];
        var path = parts[9];

        return BuildStatusEntry(xy, submodule, path, secondaryPath);
    }

    private static StatusEntry BuildStatusEntry(string xy, string submodule, string path, string? secondaryPath)
    {
        var group = DetermineGroup(xy);
        var isSubmodule = !string.Equals(submodule, "N", StringComparison.OrdinalIgnoreCase);
        return new StatusEntry(group, path, secondaryPath, xy, isSubmodule);
    }

    private static StatusGroup DetermineGroup(string xy)
    {
        if (xy.Length >= 2)
        {
            if (xy[0] == 'U' || xy[1] == 'U')
            {
                return StatusGroup.Conflicts;
            }

            var staged = xy[0] != '.';
            var unstaged = xy[1] != '.';
            if (staged && !unstaged)
            {
                return StatusGroup.Staged;
            }

            if (!staged && unstaged)
            {
                return StatusGroup.Unstaged;
            }

            if (staged && unstaged)
            {
                return StatusGroup.Unstaged;
            }
        }

        return StatusGroup.Unstaged;
    }

    private static string TrimDiffPath(string raw)
    {
        if (raw.StartsWith("a/", StringComparison.Ordinal) || raw.StartsWith("b/", StringComparison.Ordinal))
        {
            return raw[2..];
        }

        return raw;
    }

    private static DateTimeOffset ParseDate(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static DateTimeOffset? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ParseDate(value);
    }

    private static int? ParseOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed class DiffFileBuilder
    {
        public DiffFileBuilder(string oldPath, string newPath)
        {
            OldPath = oldPath;
            NewPath = newPath;
        }

        public string OldPath { get; set; }

        public string NewPath { get; set; }

        public List<DiffHunkBuilder> Hunks { get; } = new();

        public DiffFile Build()
        {
            var hunks = Hunks.Select(hunk => hunk.Build()).ToArray();
            var stats = ComputeStats(hunks);
            return new DiffFile(OldPath, NewPath, hunks, stats);
        }

        private static DiffStats? ComputeStats(IReadOnlyList<DiffHunk> hunks)
        {
            var added = 0;
            var deleted = 0;

            foreach (var hunk in hunks)
            {
                foreach (var line in hunk.Lines)
                {
                    switch (line.Kind)
                    {
                        case DiffLineKind.Add:
                            added++;
                            break;
                        case DiffLineKind.Remove:
                            deleted++;
                            break;
                    }
                }
            }

            if (added == 0 && deleted == 0)
            {
                return null;
            }

            return new DiffStats(added, deleted);
        }
    }

    private sealed class DiffHunkBuilder
    {
        private readonly List<DiffLine> _lines = new();

        public DiffHunkBuilder(int oldStart, int oldCount, int newStart, int newCount)
        {
            OldStart = oldStart;
            OldCount = oldCount;
            NewStart = newStart;
            NewCount = newCount;
        }

        public int OldStart { get; }

        public int OldCount { get; }

        public int NewStart { get; }

        public int NewCount { get; }

        public void AddLine(DiffLineKind kind, string text)
        {
            _lines.Add(new DiffLine(kind, text));
        }

        public DiffHunk Build()
        {
            return new DiffHunk(OldStart, OldCount, NewStart, NewCount, _lines.ToArray());
        }
    }
}
