using AwesomeAssertions;
using Pine.Core.Bundle;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Pine.UnitTests.Bundle;

public class TarBrotliArchiveTests
{
    [Fact]
    public void Roundtrip_single_file()
    {
        var files = new Dictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>
        {
            { new[] { "test.txt" }, new byte[] { 72, 101, 108, 108, 111 } } // "Hello"
        };

        var archive = TarBrotliArchive.CreateArchive(files);
        var extracted = TarBrotliArchive.ExtractArchive(archive);

        extracted.Count.Should().Be(1);

        // Check by converting paths to strings for comparison
        var extractedFile = extracted.Single(kvp => string.Join("/", kvp.Key) == "test.txt");
        var extractedBytes = extractedFile.Value.ToArray();
        var expectedBytes = new byte[] { 72, 101, 108, 108, 111 };
        extractedBytes.Length.Should().Be(expectedBytes.Length);
        for (int i = 0; i < expectedBytes.Length; i++)
        {
            extractedBytes[i].Should().Be(expectedBytes[i]);
        }
    }

    [Fact]
    public void Roundtrip_multiple_files()
    {
        var files = new Dictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>
        {
            { new[] { "dir1", "file1.txt" }, new byte[] { 1, 2, 3 } },
            { new[] { "dir1", "file2.txt" }, new byte[] { 4, 5, 6, 7 } },
            { new[] { "dir2", "file3.txt" }, new byte[] { 8, 9 } },
            { new[] { "root.txt" }, new byte[] { 10 } }
        };

        var archive = TarBrotliArchive.CreateArchive(files);
        var extracted = TarBrotliArchive.ExtractArchive(archive);

        extracted.Count.Should().Be(4);

        // Convert to string-keyed dictionaries for easier comparison
        var extractedByPath = extracted.ToDictionary(
            kvp => string.Join("/", kvp.Key),
            kvp => kvp.Value);

        foreach (var (key, value) in files)
        {
            var pathString = string.Join("/", key);
            extractedByPath.Should().ContainKey(pathString);

            var extractedBytes = extractedByPath[pathString].ToArray();
            var expectedBytes = value.ToArray();
            extractedBytes.Length.Should().Be(expectedBytes.Length);
            for (int i = 0; i < expectedBytes.Length; i++)
            {
                extractedBytes[i].Should().Be(expectedBytes[i]);
            }
        }
    }

    [Fact]
    public void Roundtrip_empty_file()
    {
        var files = new Dictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>
        {
            { new[] { "empty.txt" }, Array.Empty<byte>() }
        };

        var archive = TarBrotliArchive.CreateArchive(files);
        var extracted = TarBrotliArchive.ExtractArchive(archive);

        extracted.Count.Should().Be(1);
        var extractedFile = extracted.Single(kvp => string.Join("/", kvp.Key) == "empty.txt");
        extractedFile.Value.ToArray().Length.Should().Be(0);
    }

    [Fact]
    public void Compressed_archive_is_smaller_than_uncompressed()
    {
        // Create files with repetitive content that compresses well
        var repetitiveContent = new byte[10000];
        for (int i = 0; i < repetitiveContent.Length; i++)
        {
            repetitiveContent[i] = (byte)(i % 10);
        }

        var files = new Dictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>
        {
            { new[] { "file1.dat" }, new ReadOnlyMemory<byte>(repetitiveContent) },
            { new[] { "file2.dat" }, new ReadOnlyMemory<byte>(repetitiveContent) },
            { new[] { "file3.dat" }, new ReadOnlyMemory<byte>(repetitiveContent) }
        };

        var archive = TarBrotliArchive.CreateArchive(files);

        var originalSize = files.Values.Sum(v => v.Length);
        var compressedSize = archive.Length;

        // The compressed size should be significantly smaller than the original
        // (At least 50% reduction given the highly repetitive content)
        compressedSize.Should().BeLessThan(originalSize / 2);
    }

    [Fact]
    public void Roundtrip_with_special_characters_in_paths()
    {
        var files = new Dictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>
        {
            { new[] { "dir with spaces", "file-with-dashes.txt" }, new byte[] { 1, 2, 3 } },
            { new[] { "dir_underscore", "file.extension.multiple.dots" }, new byte[] { 4, 5 } }
        };

        var archive = TarBrotliArchive.CreateArchive(files);
        var extracted = TarBrotliArchive.ExtractArchive(archive);

        extracted.Count.Should().Be(2);

        // Convert to string-keyed dictionaries for easier comparison
        var extractedByPath = extracted.ToDictionary(
            kvp => string.Join("/", kvp.Key),
            kvp => kvp.Value);

        foreach (var (key, value) in files)
        {
            var pathString = string.Join("/", key);
            extractedByPath.Should().ContainKey(pathString);

            var extractedBytes = extractedByPath[pathString].ToArray();
            var expectedBytes = value.ToArray();
            extractedBytes.Length.Should().Be(expectedBytes.Length);
            for (int i = 0; i < expectedBytes.Length; i++)
            {
                extractedBytes[i].Should().Be(expectedBytes[i]);
            }
        }
    }

    [Fact]
    public void Roundtrip_large_file()
    {
        // Create a file with 1MB of data
        var largeContent = new byte[1024 * 1024];
        new Random(42).NextBytes(largeContent);

        var files = new Dictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>
        {
            { new[] { "large.bin" }, new ReadOnlyMemory<byte>(largeContent) }
        };

        var archive = TarBrotliArchive.CreateArchive(files);
        var extracted = TarBrotliArchive.ExtractArchive(archive);

        extracted.Count.Should().Be(1);
        var extractedFile = extracted.Single(kvp => string.Join("/", kvp.Key) == "large.bin");
        var extractedBytes = extractedFile.Value.ToArray();
        extractedBytes.Length.Should().Be(largeContent.Length);
        // For large files, just check a few samples to avoid performance issues
        extractedBytes[0].Should().Be(largeContent[0]);
        extractedBytes[extractedBytes.Length / 2].Should().Be(largeContent[largeContent.Length / 2]);
        extractedBytes[extractedBytes.Length - 1].Should().Be(largeContent[largeContent.Length - 1]);
    }
}
