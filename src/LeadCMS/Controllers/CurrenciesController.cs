// <copyright file="CurrenciesController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Helpers;
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
        return Ok(CurrencyInfoHelper.GetAll());
    }
}
