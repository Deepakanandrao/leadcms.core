// <copyright file="TestEntity.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations.Schema;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;

namespace LeadCMS.Plugin.TestPlugin.Entities;

[Table("test_entity")]
[SupportsChangeLog]
public class TestEntity : BaseEntity
{
    public string StringField { get; set; } = string.Empty;
}