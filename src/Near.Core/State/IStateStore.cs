using System;

namespace Near.Core.State;

public interface IStateStore<T>
{
    T Current { get; }

    IDisposable Subscribe(Action<T> observer);

    void Dispatch(IAction action);
}
