using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using VaxDrive.VaxAgent.Checks.Yara;

namespace VaxDrive.VaxAgent.Tests.Checks.Yara;

public class ProcessMemoryReaderTests
{
    private class StubMemoryReader : ProcessMemoryReader
    {
        private readonly byte[] _memory;

        public StubMemoryReader(byte[] memory) : base(System.Diagnostics.Process.GetCurrentProcess().Id)
        {
            _memory = memory;
            Environment.SetEnvironmentVariable("VAXDRIVE_YARA_OVERLAP_BYTES", "4");
        }

        public override byte[] ReadMemoryChunk(IntPtr baseAddress, int size)
        {
            int offset = (int)baseAddress;
            if (offset >= _memory.Length) return Array.Empty<byte>();

            int length = Math.Min(size, _memory.Length - offset);
            byte[] chunk = new byte[length];
            Array.Copy(_memory, offset, chunk, 0, length);
            return chunk;
        }

        public IEnumerable<byte[]> TestReadWithoutOverlap(long totalSize)
        {
            int chunkSize = 4 * 1024 * 1024;
            long offset = 0;
            while (offset < totalSize)
            {
                int toRead = (int)Math.Min(chunkSize, totalSize - offset);
                yield return ReadMemoryChunk(new IntPtr(offset), toRead);
                offset += toRead;
            }
        }
    }

    [Fact]
    public void ReadChunksWithOverlap_BoundarySpanningSignature_Detected()
    {
        // 4MB chunk size = 4194304 bytes. 
        // We will create a memory array exactly that size + a bit more, with our signature spanning the boundary.
        int chunkSize = 4 * 1024 * 1024;
        byte[] memory = new byte[chunkSize + 100];
        
        // "MALWARE" signature spanning boundary: 
        // "MALW" at the end of chunk 1, "ARE" at start of chunk 2.
        byte[] signature = System.Text.Encoding.ASCII.GetBytes("MALWARE");
        Array.Copy(signature, 0, memory, chunkSize - 4, signature.Length);

        using var reader = new StubMemoryReader(memory);

        // Without overlap, it should miss it.
        bool foundWithoutOverlap = false;
        foreach (var chunk in reader.TestReadWithoutOverlap(memory.Length))
        {
            if (Contains(chunk, signature)) foundWithoutOverlap = true;
        }
        Assert.False(foundWithoutOverlap, "Should miss without overlap");

        // With overlap, it should find it in the second chunk
        bool foundWithOverlap = false;
        foreach (var chunk in reader.ReadChunksWithOverlap(IntPtr.Zero, memory.Length))
        {
            if (Contains(chunk, signature)) foundWithOverlap = true;
        }
        Assert.True(foundWithOverlap, "Should detect with overlap");
    }

    private bool Contains(byte[] source, byte[] pattern)
    {
        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }
}
