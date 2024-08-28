using System.Diagnostics;
using System.IO;
using System;
using System.Threading;
using System.Linq;
using Windows.Win32;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Plasmawatch_Launcher
{
    internal sealed class Program
    {
        readonly static byte[] pinSignature = [0xFF, 0x50, 0x38];
        readonly static byte[] jneSignature = [0x0F, 0x85, 0x78, 0x01, 0x00, 0x00];
        readonly static byte[] curlSslSignature = [0x74, 0x10];
        readonly static byte[] curlSslPatch = [0x75, 0x10];

        readonly static nint pPinRVA = 0x1100106;
        readonly static nint pJneCodeRVA = 0x110010D;
        readonly static nint pCurlRVA = 0x7C106;
        static void Main(string[] args)
        {
            EditHostsFile();
            // Print Options -- Set Game Dir, Edit pre set args
            /*Console.WriteLine("[S] Set Overwatch Beta 0.8.0.24919 folder");
            Console.WriteLine("[A] Edit or change command line arguments");
            do
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.S:
                        // Set Game Path logic
                        break;

                    case ConsoleKey.A:
                        // Edit our args
                        break;
                }
            } while (true);*/

            if (args.Length > 0 && File.Exists(args[0]))
            {
                LaunchGame(args[0]);
            }
            else if (File.Exists("GameClientApp.exe"))
            {
                LaunchGame("GameClientApp.exe");
            }
            else
            {
                Console.Write("Enter the game directory (drag and drop GameClientApp.exe into this window then press enter)");
                var read = Console.ReadLine();
                while (true)
                {
                    if (read == null || !File.Exists("GameClientApp.exe"))
                        Console.WriteLine("Invaild path");
                    else
                        LaunchGame(read);
                }
            }
        }

        public unsafe static void LaunchGame(string filePath = "GameClientApp.exe")
        {

            List<string> cmdArgs = ["--BNetServer=bnet-emu.fish:1119"];

            Console.Write("Enter additional command line arguements, if any: ");
            var read = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(read))
                cmdArgs.AddRange(read.Split(' '));

            Console.WriteLine($"Launching with arguments: {string.Join(' ', cmdArgs)}");

            var overwatch = Process.Start(filePath, cmdArgs);

            //var overwatch = Process.Start(filePath, " --BNetServer=bnet-emu.fish:1119");

            overwatch.WaitForInputIdle();

            var baseAddr = overwatch.Modules[0].BaseAddress;

            var pPinAddr = baseAddr + pPinRVA;
            var pJneCodeAddr = baseAddr + pJneCodeRVA;
            var pCurlAddr = baseAddr + pCurlRVA;

            SafeHandle hProc = overwatch.SafeHandle;

            bool showPatchWaiting = true;

            while (true) // wait for the game to unpack
            {
                if(Memory.IsMemoryAvailable(hProc, pPinAddr, pinSignature, (uint)pinSignature.Length) &&
                    Memory.IsMemoryAvailable(hProc, pJneCodeAddr, jneSignature, (uint)jneSignature.Length))
                {
                    Console.WriteLine("Ready to patch region1");
                    break;
                }
                else
                {
                    if (showPatchWaiting)
                    {
                        showPatchWaiting = false;
                        Console.WriteLine("Waiting for region1 to be unpacked");
                    }
                    Thread.Sleep(50);
                }
            }

            showPatchWaiting = true;

            overwatch.Refresh();

            /*PInvoke.DuplicateHandle(hProc, hProc, hProc, out var writeHandle, 0x0008 | 0x0020, false, 0);*/

            var writeHandle = PInvoke.OpenProcess_SafeHandle(Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_VM_WRITE |
                                                             Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_VM_OPERATION,
                                                             false, (uint)overwatch.Id);

            Memory.NopEx((byte*)pPinAddr, (uint)pinSignature.Length, writeHandle);
            Memory.NopEx((byte*)pJneCodeAddr, (uint)jneSignature.Length, writeHandle);

            Console.WriteLine("Patch 1 applied");

            while (true)
            {
                if (Memory.IsMemoryAvailable(hProc, pCurlAddr, curlSslSignature, (uint)curlSslSignature.Length))
                {
                    Console.WriteLine("Ready to patch region2");
                    break;
                }
                else
                {
                    if (showPatchWaiting)
                    {
                        showPatchWaiting = false;
                        Console.WriteLine("Waiting for region2 to be unpacked");
                    }
                    Thread.Sleep(50);
                }
            }

            //overwatch.Refresh();
            fixed (void* ptr = curlSslPatch)
                Memory.PatchEx((void*)pCurlAddr, ptr, (uint)curlSslPatch.Length, writeHandle);

            Console.WriteLine("Patch 2 applied");

            hProc.Dispose();
            writeHandle.Dispose();
        }
        private static void EditHostsFile()
        {
            // edit the hosts file, this will need admin rights
            const string configFileName = @"C:\Windows\System32\drivers\etc\hosts";

            string[] loginServerLines =
                ["#These are for the Plasmawatch Login Server",
                "127.0.0.1 bnet-emu.fish",
                "127.0.0.1 us.game.bwattle.uwu"];
            try
            {
                foreach (var line in loginServerLines)
                {
                    if (!File.ReadAllLines(configFileName).Contains(line))
                        File.AppendAllText(configFileName, $"\n{line}"); // will fail without admin
                }
            }
            catch
            {
                Error("host file may not be set, please launch this application as an Administrator to set it");
            }
        }

        public static void Error(string message)
        {
            ConsoleColor old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = old;
        }
    }
}
