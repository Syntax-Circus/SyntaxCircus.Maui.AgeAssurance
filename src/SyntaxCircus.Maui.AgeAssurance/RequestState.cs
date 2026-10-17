namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Bounded, thread-safe request state shared between the caller and platform callback threads.
/// Port of the Godot library's <c>RequestState.kt</c>: only the active request may complete, be taken
/// or update the sharing flag; cancelled or superseded requests are ignored.
/// </summary>
internal sealed class RequestState
{
    private readonly object _gate = new();
    private int _active;
    private NativeAgeSignal? _answer;
    private bool _shared;

    internal void Begin(int request)
    {
        lock (_gate)
        {
            _active = request;
            _answer = null;
        }
    }

    internal bool IsActive(int request)
    {
        lock (_gate)
        {
            return _active == request;
        }
    }

    /// <summary>Whether access was shared earlier in this process (never reset by cancel).</summary>
    internal bool WasShared()
    {
        lock (_gate)
        {
            return _shared;
        }
    }

    internal bool UpdateShared(int request, bool value)
    {
        lock (_gate)
        {
            if (_active != request)
            {
                return false;
            }

            _shared = value;
            return true;
        }
    }

    /// <summary>Stores the answer if <paramref name="request"/> is still active; returns whether it was accepted.</summary>
    internal bool Complete(int request, NativeAgeSignal value)
    {
        lock (_gate)
        {
            if (_active != request)
            {
                return false;
            }

            _answer = value;
            return true;
        }
    }

    /// <summary>Takes the answer for the active request (once), or <see langword="null"/>.</summary>
    internal NativeAgeSignal? Take(int request)
    {
        lock (_gate)
        {
            if (_active != request)
            {
                return null;
            }

            var answer = _answer;
            _answer = null;
            return answer;
        }
    }

    internal void Cancel(int request)
    {
        lock (_gate)
        {
            if (_active == request)
            {
                _active = 0;
                _answer = null;
            }
        }
    }
}
