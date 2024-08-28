using System;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Memory;


namespace Plasmawatch_Launcher
{
    public unsafe sealed class Memory
    {
        public static void PatchEx(void* destination, void* source, uint size, SafeHandle handleToProcess)
        {
            if (!PInvoke.VirtualProtectEx(handleToProcess, destination, size, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out var oldProtect))
            {
                Console.WriteLine("Failed to change memory protection Wide IO. Error code: " + Marshal.GetLastWin32Error());
                //Environment.Exit(-1);
            }
            PInvoke.WriteProcessMemory(handleToProcess, destination, source, size, null);
            PInvoke.VirtualProtectEx(handleToProcess, destination, size, oldProtect, out oldProtect);
        }

        public static void NopEx(byte* destination, uint size, SafeHandle handleToProcess)
        {
            byte[] nopArray = new byte[size];
            Array.Fill(nopArray, (byte)0x90);
            fixed (byte* ptr = nopArray)
                PatchEx(destination, ptr, size, handleToProcess);

            Console.WriteLine($"Wrote {size} NOPs at 0x{(nint)destination:X}");
        }

        public static bool IsMemoryAvailable(SafeHandle handle, nint pMemory, byte[] pSigniture, UIntPtr sigSize)
        {
            var buffer = new byte[sigSize];

            fixed (void* bufferPtr = buffer)
                PInvoke.ReadProcessMemory(handle, (void*)pMemory, bufferPtr, sigSize, null);

            return buffer.SequenceEqual(pSigniture);
        }


    }
}
