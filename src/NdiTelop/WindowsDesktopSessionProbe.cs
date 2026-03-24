using System.Runtime.InteropServices;

namespace NdiTelop;

internal static class WindowsDesktopSessionProbe
{
    private const int UserObjectFlagsIndex = 1;
    private const int WindowStationVisibleFlag = 0x0001;

    public static bool HasVisibleWindowStation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var windowStation = GetProcessWindowStation();
        if (windowStation == IntPtr.Zero)
        {
            return false;
        }

        var flags = new UserObjectFlags();
        if (!GetUserObjectInformation(windowStation, UserObjectFlagsIndex, ref flags, Marshal.SizeOf<UserObjectFlags>(), out _))
        {
            return false;
        }

        return (flags.Flags & WindowStationVisibleFlag) != 0;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetProcessWindowStation();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(
        IntPtr handle,
        int index,
        ref UserObjectFlags userObjectInformation,
        int userObjectInformationLength,
        out int lengthNeeded);

    [StructLayout(LayoutKind.Sequential)]
    private struct UserObjectFlags
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool Inherit;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Reserved;

        public int Flags;
    }
}
