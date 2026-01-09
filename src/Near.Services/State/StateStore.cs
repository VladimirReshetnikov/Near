using System;
using System.Collections.Generic;
using Near.Core.State;

namespace Near.Services.State;

public sealed class StateStore<T> : IStateStore<T>
{
    private readonly object _lock = new();
    private readonly List<Action<T>> _observers = new();
    private readonly Func<T, IAction, T> _reducer;

    public StateStore(T initialState, Func<T, IAction, T> reducer)
    {
        Current = initialState;
        _reducer = reducer;
    }

    public T Current { get; private set; }

    public IDisposable Subscribe(Action<T> observer)
    {
        if (observer is null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        lock (_lock)
        {
            _observers.Add(observer);
        }

        observer(Current);

        return new Subscription(() => Unsubscribe(observer));
    }

    public void Dispatch(IAction action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        List<Action<T>> observersSnapshot;

        lock (_lock)
        {
            Current = _reducer(Current, action);
            observersSnapshot = new List<Action<T>>(_observers);
        }

        foreach (var observer in observersSnapshot)
        {
            observer(Current);
        }
    }

    private void Unsubscribe(Action<T> observer)
    {
        lock (_lock)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public Subscription(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _dispose();
            _isDisposed = true;
        }
    }
}
