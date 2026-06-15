#if NET35
using System;
using Xunit;
using VaxDrive.VaxAgent.Loaders;

namespace VaxDrive.VaxAgent.Tests.Loaders;

public class LegacyDefinitionLoaderTests
{
    [Fact]
    public void Parse_ValidJson_ReturnsCorrectCveCountAndFields()
    {
        string json = @"
        [
            {
                ""cve_id"": ""CVE-2023-1234"",
                ""min_version"": ""1.0"",
                ""max_version"": ""2.0"",
                ""severity"": 8.5
            },
            {
                ""cve_id"": ""CVE-2021-9999"",
                ""min_version"": ""1.2.3"",
                ""max_version"": ""1.2.4"",
                ""severity"": 9.8
            }
        ]";

        var result = LegacyDefinitionLoader.Parse(json);

        Assert.NotNull(result);
        Assert.NotNull(result.Cves);
        Assert.Equal(2, result.Cves.Count);

        var first = result.Cves[0];
        Assert.Equal("CVE-2023-1234", first.CveId);
        Assert.Equal("1.0", first.MinVersion);
        Assert.Equal("2.0", first.MaxVersion);
        Assert.Equal(8.5, first.Severity);

        var second = result.Cves[1];
        Assert.Equal("CVE-2021-9999", second.CveId);
        Assert.Equal("1.2.3", second.MinVersion);
        Assert.Equal("1.2.4", second.MaxVersion);
        Assert.Equal(9.8, second.Severity);
    }

    [Fact]
    public void Parse_MissingField_ThrowsFormatException()
    {
        string json = @"
        [
            {
                ""cve_id"": ""CVE-2023-1234"",
                ""min_version"": ""1.0"",
                ""max_version"": ""2.0""
            }
        ]";

        Assert.Throws<FormatException>(() => LegacyDefinitionLoader.Parse(json));
    }
}
#endif
