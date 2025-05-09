// <copyright file="MessagesController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions;
using LeadCMS.Plugin.Sms.Data;
using LeadCMS.Plugin.Sms.DTOs;
using LeadCMS.Plugin.Sms.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhoneNumbers;
using Serilog;

namespace LeadCMS.Plugin.Sms.Controllers;

[Route("api/messages")]
public class MessagesController : Controller
{
    private readonly ISmsService smsService;
    private readonly PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();
    private readonly SmsDbContext dbContext;

    public MessagesController(SmsDbContext dbContext, ISmsService smsService)
    {
        this.dbContext = dbContext;
        this.smsService = smsService;
    }

    [HttpPost]
    [Route("sms")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SendSms(
        [FromBody] SmsDetailsDto smsDetails,
        [FromHeader(Name = "Authentication")] string accessToken)
    {
        try
        {
            if (accessToken == null || accessToken.Replace("Bearer ", string.Empty) != SmsPlugin.Configuration.SmsAccessKey)
            {
                return new UnauthorizedResult();
            }

            var recipient = string.Empty;

            try
            {
                var phoneNumber = phoneNumberUtil.Parse(smsDetails.Recipient, string.Empty);

                recipient = phoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.E164);
            }
            catch (NumberParseException npex)
            {
                ModelState.AddModelError(npex.ErrorType.ToString(), npex.Message);
            }

            if (!ModelState.IsValid)
            {
                throw new InvalidModelStateException(ModelState);
            }

            var smsLog = new SmsLog
            {
                Sender = smsService.GetSender(recipient),
                Recipient = smsDetails.Recipient,
                Message = smsDetails.Message,
                Status = SmsLog.SmsStatus.NotSent,
            };

            dbContext.SmsLogs!.Add(smsLog);
            await dbContext.SaveChangesAsync();

            await smsService.SendAsync(recipient, smsDetails.Message);

            smsLog.Status = SmsLog.SmsStatus.Sent;

            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send SMS message to {0}: {1}", smsDetails.Recipient, smsDetails.Message);

            throw;
        }
        finally
        {
            await dbContext.SaveChangesAsync();
        }
    }
}