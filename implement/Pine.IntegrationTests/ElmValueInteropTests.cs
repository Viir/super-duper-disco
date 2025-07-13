using AwesomeAssertions;
using Pine.Core;
using Pine.Core.PopularEncodings;
using Pine.Core.Elm;
using System.Collections.Generic;
using Xunit;

namespace Pine.IntegrationTests;

public class ElmValueInteropTests
{
    [Fact]
    public void Pine_value_encoded_as_in_elm_compiler_roundtrips()
    {
        var testCases = (IReadOnlyList<PineValue>)[
            PineValue.EmptyList,
            PineValue.EmptyBlob,

            StringEncoding.ValueFromString("Hello, world!"),
        ];

        foreach (var testCase in testCases)
        {
            var encodedInCompiler =
                ElmValueInterop.PineValueEncodedAsInElmCompiler(testCase);

            var roundtrip =
                ElmValueInterop.ElmValueDecodedAsInElmCompiler(encodedInCompiler, null, null)
                .Extract(err => throw new System.Exception(err));

            roundtrip.Should().Be(testCase);
        }
    }
}
