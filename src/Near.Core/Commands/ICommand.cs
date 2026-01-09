using System.Threading;
using System.Threading.Tasks;

namespace Near.Core.Commands;

public interface ICommand
{
    Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken);
}
