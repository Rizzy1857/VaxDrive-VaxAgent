#if NET35
namespace System.Threading;

public struct CancellationToken
{
    public bool IsCancellationRequested { get; internal set; }
    
    public void ThrowIfCancellationRequested()
    {
        if (IsCancellationRequested)
        {
            throw new Exception("Operation canceled.");
        }
    }

    public static CancellationToken None => new CancellationToken();
}

public class CancellationTokenSource : IDisposable
{
    private CancellationToken _token = new CancellationToken();
    public CancellationToken Token => _token;

    public void Cancel()
    {
        _token.IsCancellationRequested = true;
    }

    public void Dispose()
    {
    }
}
#endif
