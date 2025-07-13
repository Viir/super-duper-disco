using AwesomeAssertions;
using Pine.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using Xunit;

namespace Pine.IntegrationTests;

public class LoadFromGitHubTests
{
    [Fact]
    public void Test_LoadFromGitHub_Tree()
    {
        var expectedFilesNamesAndHashes = new[]
        {
            ("elm-fullstack.json", "64c2c48a13c28a92366e6db67a6204084919d906ff109644f4237b22b87e952e"),

            ("elm-app/elm.json", "f6d1d18ccceb520cf43f27e5bc30060553c580e44151dbb0a32e3ded0763b209"),

            ("elm-app/src/Backend/Main.elm", "61ff36d96ea01dd1572c2f35c1c085dd23f1225fbebfbd4b3c71a69f3daa204a"),
            ("elm-app/src/Backend/InterfaceToHost.elm", "7c263cc27f29148a0ca2db1cdef5f7a17a5c0357839dec02f04c45cf8a491116"),

            ("elm-app/src/FrontendWeb/Main.elm", "6e82dcde8a9dc45ef65b27724903770d1bed74da458571811840687b4c790705"),
        };

        var loadFromGithubResult =
            LoadFromGitHubOrGitLab.LoadFromUrl(
                "https://github.com/pine-vm/pine/tree/30c482748f531899aac2b2d4895e5f0e52258be7/implement/PersistentProcess/example-elm-apps/default-full-stack-app")
            .Extract(error => throw new Exception("Failed to load from GitHub: " + error));

        var loadedFilesNamesAndContents =
            loadFromGithubResult.tree.EnumerateBlobsTransitive()
            .Select(blobPathAndContent => (
                fileName: string.Join("/", blobPathAndContent.path),
                fileContent: blobPathAndContent.blobContent))
            .ToImmutableList();

        var loadedFilesNamesAndHashes =
            loadedFilesNamesAndContents
            .Select(fileNameAndContent =>
                (fileNameAndContent.fileName,
                    Convert.ToHexStringLower(
                        SHA256.HashData(fileNameAndContent.fileContent.Span))))
            .ToImmutableList();

        loadedFilesNamesAndHashes.Should().BeEquivalentTo(
            expectedFilesNamesAndHashes,
            "Loaded files equal expected files.");
    }

    [Fact]
    public void Test_LoadFromGitHub_Tree_at_root()
    {
        var expectedFilesNamesAndHashes = new[]
        {
            (fileName: "README.md", fileHash: "e80817b2aa00350dff8f00207083b3b21b0726166dd695475be512ce86507238"),
        };

        var loadFromGithubResult =
            LoadFromGitHubOrGitLab.LoadFromUrl(
                "https://github.com/pine-vm/pine/blob/30c482748f531899aac2b2d4895e5f0e52258be7/")
            .Extract(error => throw new Exception("Failed to load from GitHub: " + error));

        var loadedFilesNamesAndContents =
            loadFromGithubResult.tree.EnumerateBlobsTransitive()
            .Select(blobPathAndContent => (
                fileName: string.Join("/", blobPathAndContent.path),
                fileContent: blobPathAndContent.blobContent))
            .ToImmutableList();

        var loadedFilesNamesAndHashes =
            loadedFilesNamesAndContents
            .Select(fileNameAndContent =>
                (fileNameAndContent.fileName,
                    fileHash:
                    Convert.ToHexStringLower(
                        SHA256.HashData(fileNameAndContent.fileContent.Span))))
            .ToImmutableList();

        foreach (var expectedFileNameAndHash in expectedFilesNamesAndHashes)
        {
            loadedFilesNamesAndHashes.Should().Contain(expectedFileNameAndHash,
                "Collection of loaded files contains a file named '" + expectedFileNameAndHash.fileName +
                "' with hash " + expectedFileNameAndHash.fileHash + ".");
        }
    }

    [Fact]
    public void Test_LoadFromGitHub_Object()
    {
        var expectedFileHash = "e80817b2aa00350dff8f00207083b3b21b0726166dd695475be512ce86507238";

        var loadFromGithubResult =
            LoadFromGitHubOrGitLab.LoadFromUrl(
                "https://github.com/pine-vm/pine/blob/30c482748f531899aac2b2d4895e5f0e52258be7/README.md")
            .Extract(error => throw new Exception("Failed to load from GitHub: " + error));

        var blobContent =
            loadFromGithubResult.tree
            .Map(fromBlob: blob => blob, fromTree: _ => throw new Exception("Unexpected tree"));

        blobContent.Should().NotBeNull("Found blobContent.");

        Convert.ToHexStringLower(SHA256.HashData(blobContent.Span))
            .ToLowerInvariant()
            .Should().Be(expectedFileHash, "Loaded blob content hash equals expected hash.");
    }

    [Fact]
    public void LoadFromGitHub_Commits_Contents_And_Lineage()
    {
        var loadFromGithubResult =
            LoadFromGitHubOrGitLab.LoadFromUrl(
                "https://github.com/Viir/bots/tree/6c5442434768625a4df9d0dfd2f54d61d9d1f61e/implement/applications")
            .Extract(error => throw new Exception("Failed to load from GitHub: " + error));

        loadFromGithubResult.urlInCommit.Should().Be(
            "https://github.com/Viir/bots/tree/6c5442434768625a4df9d0dfd2f54d61d9d1f61e/implement/applications");

        loadFromGithubResult.urlInFirstParentCommitWithSameValueAtThisPath.Should().Be(
            "https://github.com/Viir/bots/tree/1f915f4583cde98e0491e66bc73d7df0e92d1aac/implement/applications");

        loadFromGithubResult.rootCommit.hash.Should().Be("6c5442434768625a4df9d0dfd2f54d61d9d1f61e");
        loadFromGithubResult.rootCommit.content.message.Should().Be("Support finding development guides\n");
        loadFromGithubResult.rootCommit.content.author.name.Should().Be("Michael Rätzel");
        loadFromGithubResult.rootCommit.content.author.email.Should().Be("viir@viir.de");

        loadFromGithubResult.firstParentCommitWithSameTree.hash.Should().Be("1f915f4583cde98e0491e66bc73d7df0e92d1aac");
        loadFromGithubResult.firstParentCommitWithSameTree.content.message.Should().Be("Guide users\n\nClarify the bot uses drones if available.\n");
        loadFromGithubResult.firstParentCommitWithSameTree.content.author.name.Should().Be("John");
        loadFromGithubResult.firstParentCommitWithSameTree.content.author.email.Should().Be("john-dev@botengine.email");
    }


    [Fact]
    public void LoadFromGitHub_URL_points_only_to_repository()
    {
        var loadFromGithubResult =
            LoadFromGitHubOrGitLab.LoadFromUrl(
                "https://github.com/pine-vm/pine")
            .Extract(error => throw new Exception("Failed to load from GitHub: " + error));

        var loadedFilesPathsAndContents =
            loadFromGithubResult.tree.EnumerateBlobsTransitive()
            .Select(blobPathAndContent => (
                filePath: string.Join("/", blobPathAndContent.path),
                fileContent: blobPathAndContent.blobContent))
            .ToImmutableList();

        var readmeFile =
            loadedFilesPathsAndContents
            .FirstOrDefault(c => c.filePath.Equals("README.md", StringComparison.InvariantCultureIgnoreCase));

        readmeFile.fileContent.Should().NotBeNull("Loaded files contain README.md");
    }

    [Fact]
    public void LoadFromGitHub_Partial_Cache()
    {
        var tempWorkingDirectory = Filesystem.CreateRandomDirectoryInTempDirectory();

        try
        {
            var serverUrl = "http://localhost:16789";

            var server = GitPartialForCommitServer.Run(
                urls: [serverUrl],
                gitCloneUrlPrefixes: ["https://github.com/pine-vm/"],
                fileCacheDirectory: System.IO.Path.Combine(tempWorkingDirectory, "server-cache"));

            IImmutableDictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>> ConsultServer(
                LoadFromGitHubOrGitLab.GetRepositoryFilesPartialForCommitRequest request)
            {
                using var httpClient = new HttpClient();

                var httpRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    requestUri: serverUrl.TrimEnd('/') + GitPartialForCommitServer.ZipArchivePathFromCommit(request.commit))
                {
                    Content = new StringContent(string.Join("\n", request.cloneUrlCandidates))
                };

                var response = httpClient.SendAsync(httpRequest).Result;

                var responseContentBytes = response.Content.ReadAsByteArrayAsync().Result;

                return
                    PineValueComposition.ToFlatDictionaryWithPathComparer(
                        PineValueComposition.SortedTreeFromSetOfBlobsWithCommonFilePath(
                            ZipArchive.EntriesFromZipArchive(responseContentBytes))
                        .EnumerateBlobsTransitive());
            }

            {
                var loadFromGitHubResult =
                    LoadFromGitHubOrGitLab.LoadFromUrl(
                        sourceUrl: "https://github.com/pine-vm/pine/blob/30c482748f531899aac2b2d4895e5f0e52258be7/README.md",
                        getRepositoryFilesPartialForCommit:
                        request => Result<string, IImmutableDictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>>.ok(ConsultServer(request)))
                    .Extract(error => throw new Exception("Failed to load from GitHub: " + error));

                var blobContent =
                    loadFromGitHubResult.tree
                    .Map(fromBlob: blob => blob, fromTree: _ => throw new Exception("Unexpected tree"));

                blobContent.Should().NotBeNull("Found blobContent.");

                Convert.ToHexStringLower(SHA256.HashData(blobContent.Span))
                    .ToLowerInvariant()
                    .Should().Be("e80817b2aa00350dff8f00207083b3b21b0726166dd695475be512ce86507238",
                    "Loaded blob content hash equals expected hash.");
            }

            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var loadFromGitHubResult =
                    LoadFromGitHubOrGitLab.LoadFromUrl(
                        sourceUrl: "https://github.com/pine-vm/pine/blob/30c482748f531899aac2b2d4895e5f0e52258be7/azure-pipelines.yml",
                        getRepositoryFilesPartialForCommit:
                        request => Result<string, IImmutableDictionary<IReadOnlyList<string>, ReadOnlyMemory<byte>>>.ok(ConsultServer(request)))
                    .Extract(error => throw new Exception("Failed to load from GitHub: " + error));

                var blobContent =
                    loadFromGitHubResult.tree
                    .Map(fromBlob: blob => blob, fromTree: _ => throw new Exception("Unexpected tree"));

                blobContent.Should().NotBeNull("Found blobContent.");

                Convert.ToHexStringLower(SHA256.HashData(blobContent.Span))
                    .ToLowerInvariant()
                    .Should().Be("a328195ad75edf2bcc8df48b3d59db93ecc19b95b6115597c282900e1cf18cbc",
                    "Loaded blob content hash equals expected hash.");

                stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(3, "Reading another blob from an already cached commit should complete fast.");
            }
        }
        finally
        {
            Filesystem.DeleteLocalDirectoryRecursive(tempWorkingDirectory);
        }
    }
}
