using System;
using System.Collections.Generic;
using Near.Core.Models;

namespace Near.Core.State;

public sealed record PanelState(
    string Id,
    string CurrentDirectory,
    string? SelectedItem
);

public sealed record BackgroundTaskInfo(
    Guid Id,
    string Title,
    string? Message,
    double? Progress
);

public sealed record AppState(
    string ActivePanelId,
    PanelState LeftPanel,
    PanelState RightPanel,
    bool PanelsVisible,
    int TerminalHeight,
    RepoContext? ActiveRepo,
    IReadOnlyList<BackgroundTaskInfo> RunningTasks
)
{
    public static AppState CreateDefault(string initialDirectory)
    {
        var left = new PanelState("left", initialDirectory, null);
        var right = new PanelState("right", initialDirectory, null);

        return new AppState(
            ActivePanelId: left.Id,
            LeftPanel: left,
            RightPanel: right,
            PanelsVisible: true,
            TerminalHeight: 7,
            ActiveRepo: null,
            RunningTasks: Array.Empty<BackgroundTaskInfo>()
        );
    }

    public static AppState Reduce(AppState state, IAction action)
    {
        return state;
    }
}
