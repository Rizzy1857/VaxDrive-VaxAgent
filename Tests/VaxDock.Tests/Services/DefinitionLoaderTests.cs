using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using VaxDrive.Models;
using VaxDrive.VaxDock.Services;

namespace VaxDrive.VaxDock.Tests.Services;

public sealed class DefinitionLoaderTests
{
    private string CreateTempJson(DefinitionPack pack)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, JsonSerializer.Serialize(pack));
        return path;
    }

    [Fact]
    public void LoadAndValidate_ValidPack_ReturnsPack()
    {
        var pack = new DefinitionPack
        {
            SoftwareCveRules = new List<SoftwareCveRule>
            {
                new SoftwareCveRule { Id = "CVE-1", RemediationId = "REM-001" }
            },
            RemediationCards = new List<RemediationCard>
            {
                new RemediationCard { Id = "REM-001", Title = "Test Risk" }
            }
        };

        var path = CreateTempJson(pack);
        var loader = new DefinitionLoader();
        
        try
        {
            var result = loader.LoadAndValidate(path);
            Assert.NotNull(result);
            Assert.Single(result.SoftwareCveRules);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadAndValidate_OrphanedRemediationId_ThrowsException()
    {
        var pack = new DefinitionPack
        {
            SoftwareCveRules = new List<SoftwareCveRule>
            {
                new SoftwareCveRule { Id = "CVE-1", RemediationId = "REM-001" }
            },
            RemediationCards = new List<RemediationCard>() // Missing card
        };

        var path = CreateTempJson(pack);
        var loader = new DefinitionLoader();
        
        try
        {
            var ex = Assert.Throws<Exception>(() => loader.LoadAndValidate(path));
            Assert.Contains("Validation Failed: Orphaned RemediationId", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
