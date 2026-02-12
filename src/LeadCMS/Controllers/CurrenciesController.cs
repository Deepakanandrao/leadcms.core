// <copyright file="CurrenciesController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Globalization;
using LeadCMS.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[Route("api/[controller]")]
public class CurrenciesController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public ActionResult<List<CurrencyInfoDto>> GetAll()
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
        var byCode = new Dictionary<string, CurrencyInfoDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in cultures)
        {
            if (string.IsNullOrWhiteSpace(culture.Name))
            {
                continue;
            }

            RegionInfo region;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var code = region.ISOCurrencySymbol;
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (!byCode.ContainsKey(code))
            {
                var format = culture.NumberFormat;
                byCode[code] = new CurrencyInfoDto
                {
                    Code = code,
                    EnglishName = region.CurrencyEnglishName,
                    NativeName = region.CurrencyNativeName,
                    Symbol = region.CurrencySymbol,
                    DecimalDigits = format.CurrencyDecimalDigits,
                    DecimalSeparator = format.CurrencyDecimalSeparator,
                    GroupSeparator = format.CurrencyGroupSeparator,
                    PositivePattern = format.CurrencyPositivePattern,
                    NegativePattern = format.CurrencyNegativePattern,
                    CultureName = culture.Name,
                };
            }
        }

        var result = byCode.Values
            .OrderBy(dto => dto.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(result);
    }
}
