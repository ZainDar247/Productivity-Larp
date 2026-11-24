using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Larp.Core;

/// <summary>
/// Starts a program with no console of its own and outside our process group/job, so closing a terminal
/// (or quitting this app) never takes it down. Plain ShellExecute can hand a console-subsystem launcher a
/// fresh console window; closing that window kills everything attached to it.
/// </summary>
public static class DetachedLauncher
{
    private const uint DETACHED_PROCESS = 0x00000008;
    private const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb; public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION { public nint hProcess, hThread; public int dwProcessId, dwThreadId; }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(string? app, string cmd, nint pa, nint ta, bool inherit, uint flags, nint env,
        string? cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint h);

    /// <summary>Whether <paramref name="target"/> is an existing .exe that this launcher should start itself.</summary>
    public static bool IsExecutable(string target) =>
        OperatingSystem.IsWindows() && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target);

    public static void Start(string exe, string? cwd = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var cmd = $"\"{exe}\"";
        cwd ??= Path.GetDirectoryName(exe);
        var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
        var flags = DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP | CREATE_UNICODE_ENVIRONMENT;
        // leaving a job is not always permitted: try with breakaway first, then without
        if (!CreateProcessW(null, cmd, 0, 0, false, flags | CREATE_BREAKAWAY_FROM_JOB, 0, cwd, ref si, out var pi)
            && !CreateProcessW(null, cmd, 0, 0, false, flags, 0, cwd, ref si, out pi))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
    }
}
