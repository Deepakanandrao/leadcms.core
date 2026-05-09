// <copyright file="TestRedirect.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Enums;

namespace LeadCMS.Tests.TestEntities;

public class TestRedirect : RedirectCreateDto
{
    public TestRedirect(string uid = "")
    {
        SourceType = RedirectSourceType.InternalPath;
        FromPath = $"/old-path/{uid}";
        Kind = RedirectKind.Permanent;
        TargetType = RedirectTargetType.InternalPath;
        ToPath = $"/new-path/{uid}";
    }
}
