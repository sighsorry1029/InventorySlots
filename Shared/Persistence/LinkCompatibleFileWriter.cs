using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InventoryPersistence;

internal static class LinkCompatibleFileWriter
{
    private const int ErrorSymlinkNotSupported = 1464;

    internal static Exception? WriteAllText(string destinationPath, string contents)
    {
        return Write(destinationPath, path => File.WriteAllText(path, contents));
    }

    internal static Exception? WriteAllLines(string destinationPath, IEnumerable<string> lines)
    {
        string[] materializedLines = lines.ToArray();
        return Write(destinationPath, path => File.WriteAllLines(path, materializedLines));
    }

    internal static bool IsLinkReplacementUnsupported(Exception exception)
    {
        return exception is NotSupportedException ||
               exception is IOException && (exception.HResult & 0xffff) == ErrorSymlinkNotSupported;
    }

    private static Exception? Write(string destinationPath, Action<string> writeContents)
    {
        string? directoryPath = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(directoryPath))
        {
            throw new ArgumentException("The destination must include a directory.", nameof(destinationPath));
        }

        Directory.CreateDirectory(directoryPath);
        string? temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            writeContents(temporaryPath);
            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                temporaryPath = null;
                return null;
            }

            try
            {
                File.Replace(temporaryPath, destinationPath, null);
                temporaryPath = null;
                return null;
            }
            catch (Exception exception) when (IsLinkReplacementUnsupported(exception))
            {
                // File.Replace cannot target a symbolic link on Windows. Writing through the
                // destination keeps the link intact, unlike deleting it and moving the temp file.
                writeContents(destinationPath);
                return exception;
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
