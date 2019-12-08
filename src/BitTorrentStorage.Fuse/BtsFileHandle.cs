using System;
using System.Threading;

namespace BitTorrentStorage.Fuse
{
    sealed class BtsFileHandle : IDisposable
    {
        public readonly IntPtr Handle;
        public readonly BtsFileStream Stream;
        public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        public readonly CancellationTokenSource Cts = new CancellationTokenSource();

        public BtsFileHandle(IntPtr handle, BtsFileStream stream)
        {
            Handle = handle;
            Stream = stream;
        }

        public void Dispose()
        {
            Stream.Dispose();
            Cts.Dispose();
        }
    }
}