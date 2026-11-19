using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EzPrayer.Services
{
    public static class WallpaperHost
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string? windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        private const uint WM_SPAWN_WORKER = 0x052C;
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020; // Click-through
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        public static void AttachToWallpaper(Window window)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;

            IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)((long)exStyle | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT));

            IntPtr workerW = FindWallpaperWorker();
            if (workerW != IntPtr.Zero)
            {
                IntPtr originalStyle = GetWindowLongPtr(hwnd, GWL_STYLE);
                long childStyle = ((long)originalStyle | WS_CHILD) & ~((long)WS_POPUP);
                SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(childStyle));

                SetParent(hwnd, workerW);
                if (GetParent(hwnd) == workerW)
                {
                    SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    Console.WriteLine("[WIDGET] Terpasang pada WorkerW desktop.");
                    return;
                }

                SetWindowLongPtr(hwnd, GWL_STYLE, originalStyle);
                Console.WriteLine("[WIDGET] Gagal memasang WorkerW; memakai posisi paling belakang.");
            }
            else
            {
                Console.WriteLine("[WIDGET] WorkerW tidak ditemukan; memakai posisi paling belakang.");
            }

            FallbackAlwaysOnBottom(hwnd);
        }

        private static IntPtr FindWallpaperWorker()
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                SendMessageTimeout(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero,
                    0, 1000, out _);
            }

            IntPtr workerW = IntPtr.Zero;
            EnumWindows((topLevelWindow, _) =>
            {
                IntPtr desktopView = FindWindowEx(topLevelWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (desktopView == IntPtr.Zero)
                {
                    return true;
                }

                workerW = FindWindowEx(IntPtr.Zero, topLevelWindow, "WorkerW", null);
                return workerW == IntPtr.Zero;
            }, IntPtr.Zero);

            return workerW;
        }

        private static void FallbackAlwaysOnBottom(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
}