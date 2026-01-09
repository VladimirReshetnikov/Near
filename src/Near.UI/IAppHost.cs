using System.Threading;
using System.Threading.Tasks;

namespace Near.UI;

public interface IAppHost
{
    Task RunAsync(CancellationToken cancellationToken);
}
