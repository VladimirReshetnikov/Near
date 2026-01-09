using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;
using Near.UI.Layout;

namespace Near.UI;

public sealed class TerminalGuiAppHost : IAppHost
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        Application.Init();

        var top = Application.Top;
        top.Add(new MainLayoutView());

        using var registration = cancellationToken.Register(Application.RequestStop);
        Application.Run();
        Application.Shutdown();

        return Task.CompletedTask;
    }
}
