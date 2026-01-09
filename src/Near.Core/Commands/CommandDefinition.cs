using System.Collections.Generic;

namespace Near.Core.Commands;

public sealed record CommandDefinition(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<KeyBinding> DefaultBindings,
    CommandContextType Context
);

public enum CommandContextType
{
    Any,
    Global,
    FilePanel,
    GitPanel,
    Terminal
}

public sealed record KeyBinding(string Gesture);
