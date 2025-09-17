// <copyright file="ConfigTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using LeadCMS.Controllers;

namespace LeadCMS.Tests;

public class ConfigTests : BaseTest
{
    [Fact]
    public async Task GetConfig_ReturnsContentValidationSettings()
    {
        // Arrange
        // Act
        var configDto = await GetTest<ConfigDto>("/api/config", HttpStatusCode.OK);

        Assert.NotNull(configDto);
        Assert.NotNull(configDto.Settings);
        Assert.Equal("60", configDto.Settings[SettingKeys.MaxTitleLength]);
        Assert.Equal("155", configDto.Settings[SettingKeys.MaxDescriptionLength]);
    }
}