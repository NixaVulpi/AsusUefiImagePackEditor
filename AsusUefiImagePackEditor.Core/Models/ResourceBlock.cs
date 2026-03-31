using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AsusUefiImagePackEditor.Core.Models;

public sealed class ResourceBlock: ICloneable
{
    private byte[] _data = [];

    public byte[] Header { get; set; } = new byte[0x50];

    public byte[] Data
    {
        get => _data;
        set
        {
            _data = value;
            DataSize = (uint) value.Length;
        }
    }

    private ushort UshortId
    {
        get
        {
            unsafe
            {
                fixed (byte* ptr = &Header[0x0E])
                {
                    return *(ushort*) ptr;
                }
            }
        }
        set
        {
            unsafe
            {
                fixed (byte* ptr = &Header[0x0E])
                {
                    *(ushort*) ptr = value;
                }
            }
        }
    }

    private string StringId
    {
        get
        {
            unsafe
            {
                fixed (byte* ptr = &Header[0x0C])
                {
                    return Marshal.PtrToStringUni((IntPtr) ptr) ?? string.Empty;
                }
            }
        }
        set
        {
            unsafe
            {
                fixed (byte* ptr = &Header[0x0C])
                {
                    Marshal.Copy(value.ToCharArray(), 0, (IntPtr) ptr, value.Length);
                }
            }
        }
    }

    public string Id
    {
        get
        {
            if (HeaderSize == 0x20)
            {
                return UshortId.ToString();
            }
            else
            {
                return StringId;
            }
        }
        set
        {
            if (HeaderSize == 0x20 && ushort.TryParse(value, out ushort ushortId))
            {
                UshortId = ushortId;
            }
            else
            {
                StringId = value;
            }
        }
    }

    public uint HeaderSize
    {
        get
        {
            unsafe
            {
                fixed (byte* ptr = &Header[0x04])
                {
                    return *(uint*) ptr;
                }
            }
        }
        private set
        {
            unsafe
            {
                fixed (byte* ptr = &Header[0x04])
                {
                    *(uint*) ptr = value;
                }
            }
        }
    }

    public uint DataSize
    {
        get
        {
            unsafe
            {
                fixed (byte* ptr = Header)
                {
                    return *(uint*) ptr;
                }
            }
        }
        private set
        {
            unsafe
            {
                fixed (byte* ptr = Header)
                {
                    *(uint*) ptr = value;
                }
            }
        }
    }

    public object Clone()
    {
        return new ResourceBlock
        {
            Header = (byte[]) Header.Clone(),
            Data = (byte[]) Data.Clone()
        };
    }

    public static async Task<ResourceBlock?> ParseFromAsync(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream is not readable.", nameof(stream));
        }

        ResourceBlock block = new();

        if (!await ReadExactAsync(stream, block.Header, 0, 0x08))
        {
            return null;
        }

        int remainingHeaderLength = block.HeaderSize == 0x20 ? 0x18 : 0x48;
        if (!await ReadExactAsync(stream, block.Header, 0x08, remainingHeaderLength))
        {
            return null;
        }

        block.Data = new byte[block.DataSize];

        if (!await ReadExactAsync(stream, block.Data, 0, block.Data.Length))
        {
            return null;
        }

        uint alignedSize = (block.DataSize + 3u) & ~3u;
        uint paddingSize = alignedSize - block.DataSize;

        if (paddingSize > 0)
        {
            byte[] padding = new byte[paddingSize];
            if (!await ReadExactAsync(stream, padding, 0, padding.Length))
            {
                return null;
            }
        }

        return block;
    }

    public async Task SerializeToAsync(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanWrite)
        {
            throw new ArgumentException("Stream is not writable.", nameof(stream));
        }

        int headerLength = HeaderSize == 0x20 ? 0x20 : 0x50;
        await stream.WriteAsync(Header, 0, headerLength);
        await stream.WriteAsync(Data, 0, Data.Length);

        uint alignedSize = (DataSize + 3u) & ~3u;
        uint paddingSize = alignedSize - DataSize;

        if (paddingSize > 0)
        {
            byte[] padding = new byte[paddingSize];
            await stream.WriteAsync(padding, 0, padding.Length);
        }
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count)
    {
        int read = await stream.ReadAsync(buffer, offset, count);

        if (read == 0 && offset == 0)
        {
            return false;
        }

        if (read != count)
        {
            throw new EndOfStreamException($"Expected to read {count} bytes, but only read {read} bytes.");
        }

        return true;
    }
}
