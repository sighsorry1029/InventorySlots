using System;
using System.IO;

namespace InventoryPersistence;

internal static class LinkCompatibleFileWriter
{
    private const int ErrorSymlinkNotSupported = 1464;

    internal static Exception? WriteAllText(string destinationPath, string contents)
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
            File.WriteAllText(temporaryPath, contents);
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
                File.WriteAllText(destinationPath, contents);
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

    internal static bool IsLinkReplacementUnsupported(Exception exception)
    {
        return exception is NotSupportedException ||
               exception is IOException && (exception.HResult & 0xffff) == ErrorSymlinkNotSupported;
    }
}
