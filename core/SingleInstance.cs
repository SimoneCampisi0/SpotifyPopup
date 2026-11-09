using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace SpotifyPopup;

internal sealed class SingleInstance : IDisposable
{
    private readonly string name = "SpotifyPopup-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName)))[..24];
    private Mutex? mutex;
    private bool owns;
    private readonly CancellationTokenSource stopping = new();
    internal bool Acquire()
    {
        mutex = new Mutex(false, "Local\\" + name);
        try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
        return owns;
    }
    internal async Task SignalAsync()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.ConnectAsync(1000);
                await pipe.WriteAsync(new byte[] { 1 }); return;
            }
            catch (Exception ex) { if (attempt == 2) Diagnostics.Record("instance-signal", ex); }
        }
    }
    internal async Task ListenAsync(Action reveal)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stopping.Token);
                var buffer = new byte[1];
                if (await pipe.ReadAsync(buffer, stopping.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2), stopping.Token) == 1 && buffer[0] == 1) reveal();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Diagnostics.Record("instance-listener", ex); await Task.Delay(200, stopping.Token).ConfigureAwait(false); }
        }
    }
    public void Dispose()
    {
        stopping.Cancel();
        if (owns) { mutex?.ReleaseMutex(); owns = false; }
        mutex?.Dispose();
    }
}
