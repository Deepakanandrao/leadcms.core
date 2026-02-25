// <copyright file="TestSegment.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests.TestEntities;

public class TestSegment : SegmentCreateDto
{
    public TestSegment(string uid = "", SegmentType type = SegmentType.Dynamic, SegmentDefinition? definition = null, int[]? contactIds = null)
    {
        Name = $"Segment_{uid}";
        Description = $"Test segment {uid}";
        Type = type;
        Definition = definition;
        ContactIds = contactIds;
    }

    public static SegmentDefinition CreateSimpleDefinition(string fieldId, FieldOperator op, object? value)
    {
        return new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Id = Guid.NewGuid().ToString(),
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule
                    {
                        Id = Guid.NewGuid().ToString(),
                        FieldId = fieldId,
                        Operator = op,
                        Value = value,
                    },
                },
                Groups = new List<RuleGroup>(),
            },
        };
    }
}
