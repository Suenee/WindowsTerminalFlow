using System.Runtime.InteropServices;
using System.Text;

namespace WindowsTerminalFlow.Services;

public static class PathResolver
{
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnection(string localName, StringBuilder remoteName, ref int length);

    public static string ForElevation(string path)
    {
        path = Path.GetFullPath(path);
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root) || root.Length < 2 || root[1] != ':') return path;

        var drive = root[..2];
        var bufferLength = 2048;
        var buffer = new StringBuilder(bufferLength);
        var result = WNetGetConnection(drive, buffer, ref bufferLength);
        if (result != 0 || buffer.Length == 0) return path;

        var relative = path[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrEmpty(relative) ? buffer.ToString() : Path.Combine(buffer.ToString(), relative);
    }
}
