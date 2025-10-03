using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Pine.Core.Bundle;

/// <summary>
/// Helper class for creating and reading TAR archives compressed with Brotli (.tar.br)
/// </summary>
public static class TarBrotliArchive
{
    /// <summary>
    /// Creates a TAR archive compressed with Brotli from a dictionary of file paths and their contents.
    /// </summary>
    /// <param name="files">Dictionary where keys are file paths (list of path segments) and values are file contents</param>
    /// <returns>Compressed TAR archive as a byte array</returns>
    public static ReadOnlyMemory<byte> CreateArchive(
        IReadOnlyDictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>> files)
    {
        // Create TAR archive first
        using var tarStream = new MemoryStream();
        
        using (var tarWriter = SharpCompress.Writers.WriterFactory.Open(
            tarStream,
            SharpCompress.Common.ArchiveType.Tar,
            SharpCompress.Common.CompressionType.None))
        {
            foreach (var file in files.OrderBy(kvp => string.Join("/", kvp.Key)))
            {
                var filePath = string.Join("/", file.Key);
                var fileContent = file.Value;
                
                using var contentStream = new MemoryStream(fileContent.ToArray());
                tarWriter.Write(filePath, contentStream, modificationTime: null);
            }
        }
        
        tarStream.Position = 0;
        
        // Compress with Brotli
        var tarBytes = tarStream.ToArray();
        using var compressedStream = new MemoryStream();
        
        using (var brotliStream = new BrotliStream(
            compressedStream,
            CompressionLevel.Optimal,
            leaveOpen: true))
        {
            brotliStream.Write(tarBytes);
        }
        
        return compressedStream.ToArray();
    }

    /// <summary>
    /// Extracts files from a TAR archive compressed with Brotli.
    /// </summary>
    /// <param name="archiveBytes">The compressed TAR archive bytes</param>
    /// <returns>Dictionary where keys are file paths (list of path segments) and values are file contents</returns>
    public static IReadOnlyDictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>> ExtractArchive(
        ReadOnlyMemory<byte> archiveBytes)
    {
        // Decompress with Brotli
        using var compressedStream = new MemoryStream(archiveBytes.ToArray());
        using var decompressedStream = new MemoryStream();
        
        using (var brotliStream = new BrotliStream(
            compressedStream,
            CompressionMode.Decompress))
        {
            brotliStream.CopyTo(decompressedStream);
        }
        
        var tarBytes = decompressedStream.ToArray();
        
        // Extract files from TAR
        using var tarStream = new MemoryStream(tarBytes);
        using var tarArchive = SharpCompress.Archives.Tar.TarArchive.Open(tarStream);
        
        var files = new Dictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>();
        
        foreach (var entry in tarArchive.Entries.Where(e => !e.IsDirectory))
        {
            var pathSegments = entry.Key.Split('/').ToList();
            
            using var entryStream = entry.OpenEntryStream();
            using var contentStream = new MemoryStream();
            entryStream.CopyTo(contentStream);
            
            files[pathSegments] = contentStream.ToArray();
        }
        
        return files;
    }
}
