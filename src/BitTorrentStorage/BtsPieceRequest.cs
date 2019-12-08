using System;
using System.Threading;
using System.Threading.Tasks;

namespace BitTorrentStorage
{
    sealed class BtsPieceRequest : IBtsPieceRequest
    {
        readonly Action _onDispose;
        readonly TaskCompletionSource<int> _tcs;
        int _refCounter;

        public BtsPieceRequest(Action onDispose)
        {
            _onDispose = onDispose;
            _tcs = new TaskCompletionSource<int>();
            _refCounter = 1;
        }

        public void Complete() => _tcs.TrySetResult(0);

        public void Cancel() => _tcs.TrySetCanceled();
        
        public async Task AwaitFetching(CancellationToken ct)
        {
            var cancelTcs = new TaskCompletionSource<int>();
            ct.Register(cancelTcs.SetCanceled);
            await Task.WhenAny(cancelTcs.Task, _tcs.Task);
        }

        public void AddRef() => _refCounter++;

        public void Dispose()
        {
            var res = --_refCounter;
            if (res < 0) throw new InvalidOperationException();
            if (res == 0) _onDispose();
        }
    }
}