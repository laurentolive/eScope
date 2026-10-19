using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace PcSimHost;

/// <summary>
/// UART driver of the simulator whose "wire" is a TCP server: eScope connects to it with a TCP link
/// (TCP:127.0.0.1,port), the firmware sees a UART. One client, a new one replaces the previous one.
/// Received bytes are queued by the socket thread and delivered by the scheduler (Deliver), in the same
/// context as the other simulated interrupts: the simulation stays deterministic.
/// </summary>
sealed class SimUart : IDisposable
{
    readonly TcpListener listener;
    readonly ConcurrentQueue<byte> rx = new();
    readonly CancellationTokenSource cts = new();
    readonly object clientLock = new();
    TcpClient? client;

    public int Port { get; }

    public SimUart(int port)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start(1);
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        new Thread(AcceptLoop) { IsBackground = true, Name = "uart-accept" }.Start();
    }

    void AcceptLoop()
    {
        while (!cts.IsCancellationRequested)
        {
            TcpClient c;
            try { c = listener.AcceptTcpClient(); }
            catch (SocketException) { return; }
            c.NoDelay = true;
            lock (clientLock)
            {
                client?.Close();
                client = c;
            }
            Console.WriteLine("eScope connected");
            new Thread(() => ReadLoop(c)) { IsBackground = true, Name = "uart-rx" }.Start();
        }
    }

    void ReadLoop(TcpClient c)
    {
        var buf = new byte[4096];
        try
        {
            var stream = c.GetStream();
            int n;
            while ((n = stream.Read(buf, 0, buf.Length)) > 0)
            {
                for (int i = 0; i < n; i++)
                    rx.Enqueue(buf[i]);
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException) { }
        lock (clientLock)
        {
            if (client == c)
            {
                client = null;
                Console.WriteLine("eScope disconnected");
            }
        }
    }

    /// <summary>scheduler: received bytes given to the firmware UART reception interrupt</summary>
    public void Deliver(Action<byte> rxInterrupt)
    {
        while (rx.TryDequeue(out byte b))
            rxInterrupt(b);
    }

    /// <summary>firmware transmission (scheduler thread); without client the bytes are lost, as on an unplugged line</summary>
    public void Transmit(ReadOnlySpan<byte> data)
    {
        TcpClient? c;
        lock (clientLock) c = client;
        if (c == null) return;
        try { c.GetStream().Write(data); }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    public void Dispose()
    {
        cts.Cancel();
        listener.Stop();
        lock (clientLock) client?.Close();
    }
}
