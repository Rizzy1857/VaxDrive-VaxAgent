using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VaxDrive.VaxAgent.Checks.Yara;

/// <summary>
/// A Windows API wrapper for process memory inspection, used for integrity auditing.
/// Safely enumerates memory regions using VirtualQueryEx and reads chunks via ReadProcessMemory.
/// </summary>
public class ProcessMemoryReader : IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, IntPtr dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION64 lpBuffer, uint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private const int PROCESS_VM_READ = 0x0010;
    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_NOACCESS = 0x01;
    private const uint PAGE_GUARD = 0x100;
    private const int ERROR_ACCESS_DENIED = 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION64
    {
        public ulong BaseAddress;
        public ulong AllocationBase;
        public uint AllocationProtect;
        public uint __alignment1;
        public ulong RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
        public uint __alignment2;
    }

    private readonly IntPtr _hProcess;
    private readonly int _pid;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ProcessMemoryReader and opens a handle to the target process.
    /// </summary>
    /// <param name="pid">The Process ID to inspect.</param>
    public ProcessMemoryReader(int pid)
    {
        _pid = pid;
        _hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);

        if (_hProcess == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            if (errorCode == ERROR_ACCESS_DENIED)
            {
                throw new Win32Exception(errorCode, $"Access denied when opening process {pid}. Process may be protected or require higher privileges.");
            }
            throw new Win32Exception(errorCode, $"Failed to open process {pid}.");
        }
    }

    /// <summary>
    /// Enumerates all committed memory regions in the process that are accessible for reading.
    /// </summary>
    public IEnumerable<MemoryRegion> EnumerateReadableRegions()
    {
        long address = 0;
        long maxAddress = Environment.Is64BitOperatingSystem ? 0x7FFFFFFFFFF : 0x7FFFFFFF;
        uint structSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION64));

        while (address < maxAddress)
        {
            int result = VirtualQueryEx(_hProcess, (IntPtr)address, out MEMORY_BASIC_INFORMATION64 memInfo, structSize);
            
            if (result == 0)
                break;

            bool isCommitted = memInfo.State == MEM_COMMIT;
            bool isAccessible = (memInfo.Protect & PAGE_NOACCESS) == 0 && (memInfo.Protect & PAGE_GUARD) == 0;

            if (isCommitted && isAccessible)
            {
                yield return new MemoryRegion
                {
                    BaseAddress = (IntPtr)memInfo.BaseAddress,
                    RegionSize = (long)memInfo.RegionSize
                };
            }

            address = (long)memInfo.BaseAddress + (long)memInfo.RegionSize;
        }
    }

    /// <summary>
    /// Reads a chunk of memory from the target process. Caps reads at 4MB to prevent memory exhaustion.
    /// </summary>
    public byte[] ReadMemoryChunk(IntPtr baseAddress, int size)
    {
        if (size > 4 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Read chunk exceeds 4MB cap.");
        }

        byte[] buffer = new byte[size];
        bool success = ReadProcessMemory(_hProcess, baseAddress, buffer, (IntPtr)size, out IntPtr bytesRead);

        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            // Handle partial reads or access denied gracefully
            if (error == ERROR_ACCESS_DENIED)
            {
                Debug.WriteLine($"[ProcessMemoryReader] Access denied reading address {baseAddress.ToString("X")}");
                return Array.Empty<byte>();
            }
            
            // If partial read, resize the buffer
            if (bytesRead.ToInt64() > 0 && bytesRead.ToInt64() < size)
            {
                Array.Resize(ref buffer, (int)bytesRead.ToInt64());
                return buffer;
            }

            throw new Win32Exception(error, "Failed to read process memory.");
        }

        return buffer;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_hProcess != IntPtr.Zero)
            {
                CloseHandle(_hProcess);
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~ProcessMemoryReader()
    {
        Dispose();
    }
}

public class MemoryRegion
{
    public IntPtr BaseAddress { get; set; }
    public long RegionSize { get; set; }
}
