using Terminal.Gui;

namespace Near.UI.Layout;

public sealed class MainLayoutView : View
{
    private const int TopBarHeight = 1;
    private const int TerminalHeight = 7;
    private const int FooterHeight = 1;

    public MainLayoutView()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        var topBar = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = TopBarHeight
        };
        topBar.Add(new Label("Near - File & Git Manager")
        {
            X = 0,
            Y = 0
        });

        var workspace = new View
        {
            X = 0,
            Y = TopBarHeight,
            Width = Dim.Fill(),
            Height = Dim.Fill(TerminalHeight + FooterHeight)
        };

        var leftPanel = new FrameView("Left Panel")
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(50),
            Height = Dim.Fill()
        };

        var rightPanel = new FrameView("Right Panel")
        {
            X = Pos.Right(leftPanel),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        workspace.Add(leftPanel, rightPanel);

        var terminal = new FrameView("Terminal (pwsh)")
        {
            X = 0,
            Y = Pos.AnchorEnd(TerminalHeight + FooterHeight),
            Width = Dim.Fill(),
            Height = TerminalHeight
        };
        terminal.Add(new Label("Terminal session placeholder")
        {
            X = 1,
            Y = 0
        });

        var footer = new View
        {
            X = 0,
            Y = Pos.AnchorEnd(FooterHeight),
            Width = Dim.Fill(),
            Height = FooterHeight
        };
        footer.Add(new Label("F1 Help | Ctrl+O Toggle Panels | F10 Exit")
        {
            X = 0,
            Y = 0
        });

        Add(topBar, workspace, terminal, footer);
    }
}
