namespace Luma.Core;

public sealed record TransferResult(string Source, string Destination, bool Copied, bool Moved, string? Error);

public static class FileTransfer
{
    // No overwrite. Copy into a private sibling, verify the source version, then publish atomically.
    public static async Task<TransferResult> RunAsync(string source, string destination, bool move,
        Func<bool> sourceStillValid, CancellationToken ct = default)
    {
        string? temporary = null; bool copied = false;
        try
        {
            if (!sourceStillValid()) throw new IOException("Source changed or disconnected.");
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new IOException("Source and destination are the same file.");
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Destination already exists; original and destination were preserved.");
            temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".luma-{Guid.NewGuid():N}.tmp");
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await input.CopyToAsync(output, ct); await output.FlushAsync(ct); output.Flush(true);
            }
            ct.ThrowIfCancellationRequested();
            if (!sourceStillValid()) throw new IOException("Source changed while copying.");
            File.Move(temporary, destination, false); copied = true;
            if (move)
            {
                if (!sourceStillValid()) throw new IOException("Copy saved, but source changed before deletion; both files were kept.");
                File.Delete(source);
            }
            return new(source,destination,true,move,null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        { return new(source,destination,copied,false,ex is OperationCanceledException ? "Cancelled" : ex.Message); }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
    }
}
