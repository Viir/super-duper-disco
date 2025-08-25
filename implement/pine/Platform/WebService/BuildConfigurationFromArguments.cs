using Pine;
using Pine.Core;
using System;

namespace ElmTime.Platform.WebService;

public static class BuildConfigurationFromArguments
{
    public static
        (BlobTreeWithStringPath sourceTree,
        string filteredSourceCompositionId,
        byte[] configZipArchive)
        BuildConfigurationZipArchiveFromPath(string sourcePath)
    {
        var loadCompositionResult =
            LoadComposition.LoadFromPathResolvingNetworkDependencies(sourcePath)
            .LogToActions(Console.WriteLine)
            .Extract(error => throw new Exception("Failed to load from path '" + sourcePath + "': " + error));

        var sourceTree = loadCompositionResult.tree;

        /*
        TODO: Provide a better way to avoid unnecessary files ending up in the config: Get the source files from git.
        */
        var filteredSourceTree =
            loadCompositionResult.origin is LoadCompositionOrigin.FromLocalFileSystem
            ?
            LoadFromLocalFilesystem.RemoveNoiseFromTree(sourceTree, discardGitDirectory: true)
            :
            sourceTree;

        var filteredSourceComposition = PineValueComposition.FromTreeWithStringPath(filteredSourceTree);

        var filteredSourceCompositionId =
            Convert.ToHexStringLower(PineValueHashTree.ComputeHash(filteredSourceComposition).Span);

        Console.WriteLine("Loaded source composition " + filteredSourceCompositionId + " from '" + sourcePath + "'.");

        var configZipArchive =
            BuildConfigurationZipArchive(sourceComposition: filteredSourceComposition);

        return (sourceTree, filteredSourceCompositionId, configZipArchive);
    }

    public static byte[] BuildConfigurationZipArchive(PineValue sourceComposition)
    {
        var parseSourceAsTree =
            PineValueComposition.ParseAsTreeWithStringPath(sourceComposition)
            .Extract(_ => throw new Exception("Failed to map source to tree."));

        var sourceFiles = PineValueComposition.TreeToFlatDictionaryWithPathComparer(parseSourceAsTree);

        return ZipArchive.ZipArchiveFromEntries(sourceFiles);
    }
}
