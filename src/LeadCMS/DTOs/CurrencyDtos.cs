// <copyright file="CurrencyDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.DTOs;

public class CurrencyInfoDto
{
    public string Code { get; set; } = string.Empty;

    public string EnglishName { get; set; } = string.Empty;

    public string NativeName { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public int DecimalDigits { get; set; }

    public string DecimalSeparator { get; set; } = string.Empty;

    public string GroupSeparator { get; set; } = string.Empty;

    public int PositivePattern { get; set; }

    public int NegativePattern { get; set; }

    public string CultureName { get; set; } = string.Empty;
}
