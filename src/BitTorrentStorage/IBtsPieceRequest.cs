using System;
using System.Threading;
using System.Threading.Tasks;

namespace BitTorrentStorage
{
    interface IBtsPieceRequest : IDisposable
    {
        Task AwaitFetching(CancellationToken ct);
    }
}