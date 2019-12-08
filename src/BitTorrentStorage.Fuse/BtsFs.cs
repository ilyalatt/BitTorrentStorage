using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Fuse.NETStandard;
using Mono.Unix.Native;

namespace BitTorrentStorage.Fuse
{
	public sealed class BtsFs : FileSystem
    {
	    public readonly BtsTorrentManager TorrentManager;
	    public BtsTorrent Torrent => TorrentManager.Torrent;
	    public readonly BtsTrie Trie;
	    int _handleCounter;
	    readonly ConcurrentDictionary<IntPtr, BtsFileHandle> _fileHandles =
		    new ConcurrentDictionary<IntPtr, BtsFileHandle>();

	    public BtsFs(BtsTorrentManager torrentManager)
	    {
		    TorrentManager = torrentManager;
		    Trie = BtsTrie.Build(Torrent.Files);
	    }

	    protected override Errno OnGetPathStatus(string path, out Stat stbuf)
	    {
		    var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).AsMemory();
		    var parentDir = pathSegments.Length < 2 ? Trie : Trie.TryFindDirectory(pathSegments.Slice(0, pathSegments.Length - 1));
		    var file = pathSegments.Length > 0
			    ? parentDir?.TryFindFile(pathSegments.Span[pathSegments.Length - 1])
			    : null;
		    var dir = pathSegments.Length > 0
			    ? parentDir?.TryFindDirectory(pathSegments.Span[pathSegments.Length - 1])
			    : parentDir;
		    
		    if (dir != null)
		    {
			    stbuf = new Stat
			    {
				    st_mode = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0755"),
				    st_nlink = 2
			    };
			    return 0;
		    }

		    if (file != null)
		    {
			    stbuf = new Stat
			    {
				    st_mode = FilePermissions.S_IFREG | NativeConvert.FromOctalPermissionString("0444"),
				    st_nlink = 1,
				    st_size = file.Length
			    };
			    return 0;
		    }
		    
		    stbuf = new Stat();
			return Errno.ENOENT;
	    }

	    protected override Errno OnReadDirectory(
		    string path,
		    OpenedPathInfo fi,
		    out IEnumerable<DirectoryEntry>? paths
		)
		{
			var dir = Trie.TryFindDirectory(path);
			if (dir == null)
			{
				paths = null;
				return Errno.ENOENT;
			}

			paths = dir.Directories.Keys.Concat(dir.Files.Keys).Select(x => new DirectoryEntry(x)).ToList();
			return 0;
		}

		protected override Errno OnOpenHandle(string path, OpenedPathInfo fi)
		{
			var file = Trie.TryFindFile(path);
			if (file == null) return Errno.ENOENT;
			if (fi.OpenAccess != OpenFlags.O_RDONLY) return Errno.EACCES;
			
			var handle = (IntPtr) Interlocked.Increment(ref _handleCounter);
			fi.Handle = handle;
			var stream = TorrentManager.OpenFileStream(file);
			_fileHandles[handle] = new BtsFileHandle(handle, stream);
			return 0;
		}

		protected override Errno OnReleaseHandle(string file, OpenedPathInfo info)
		{
			_fileHandles.TryRemove(info.Handle, out var handle);
			handle.Cts.Cancel();
			handle.Dispose();
			return 0;
		}

		protected override Errno OnReadHandle(string path, OpenedPathInfo fi, byte[] buf, long offset, out int bytesWritten)
		{
			if (!_fileHandles.TryGetValue(fi.Handle, out var handle))
			{
				bytesWritten = 0;
				return Errno.ENOENT;
			}

			async Task<int> ReadAsync()
			{	
				var stream = handle.Stream;
				var toRead = (int) Math.Min(buf.Length, stream.Length - offset);
				var ct = handle.Cts.Token;
				var semaphore = handle.Semaphore;
				
				try
				{
					await semaphore.WaitAsync(ct);
					stream.Seek(offset, SeekOrigin.Begin);
					return await stream.ReadAsync(buf, 0, toRead, ct);
				}
				catch (OperationCanceledException)
				{
					return 0;
				}
				finally
				{
					semaphore.Release();
				}
			}

			bytesWritten = ReadAsync().Result;
			return 0;
		}

		protected override Errno OnGetPathExtendedAttribute(string path, string name, byte[] value, out int bytesWritten)
		{
			bytesWritten = 0;
			return 0;
		}

		protected override Errno OnSetPathExtendedAttribute(string path, string name, byte[] value, XattrFlags flags)
		{
			return 0;
		}

		protected override Errno OnRemovePathExtendedAttribute(string path, string name)
		{
			return 0;
		}

		protected override Errno OnListPathExtendedAttributes(string path, out string[]? names)
		{
			names = null;
			return 0;
		}
    }
}
