using System.IO;

public sealed class UmaAssetBundleStream : FileStream
{
    private const int HeaderSize = 256;
    private const int FileBufferSize = 128 * 1024;

    private readonly byte[] keys;
    private readonly object readLock = new object();

    public UmaAssetBundleStream(string filename, byte[] keys)
        : base(filename,FileMode.Open,FileAccess.Read,FileShare.Read,FileBufferSize,FileOptions.SequentialScan)
    {
        this.keys = keys;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (readLock)
        {
            long startPosition = Position;
            int read = base.Read(buffer, offset, count);

            if (read <= 0 || keys == null || keys.Length == 0)
                return read;

            int begin = 0;

            if (startPosition < HeaderSize)
            {
                long remainingHeader = HeaderSize - startPosition;
                if (remainingHeader >= read)
                    return read;

                begin = (int)remainingHeader;
            }

            int keyLength = keys.Length;
            int keyIndex = (int)((startPosition + begin) % keyLength);
            int end = offset + read;

            for (int bufferIndex = offset + begin;
                 bufferIndex < end;
                 bufferIndex++)
            {
                buffer[bufferIndex] ^= keys[keyIndex];
                keyIndex++;

                if (keyIndex == keyLength)
                    keyIndex = 0;
            }

            return read;
        }
    }
}
