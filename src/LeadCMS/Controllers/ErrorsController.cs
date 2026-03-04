// <copyright file="ErrorsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.RegularExpressions;
using LeadCMS.Exceptions;
using LeadCMS.Exceptions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LeadCMS.Controllers;

[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorsController : Controller
{
    [Route("/error")]
    public IActionResult HandleError()
    {
        var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        var error = exceptionHandlerFeature!.Error;

        ProblemDetails problemDetails;

        Log.Error(error, "Exception caught by the error controller.");

        switch (error)
        {
            // Handle base HTTP exceptions first (this will catch plugin exceptions that extend these)
            case IHttpStatusException httpStatusException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    httpStatusException.StatusCode,
                    error.Message);

                // Add any additional extensions from the exception
                var extensions = httpStatusException.GetExtensions();
                foreach (var kvp in extensions)
                {
                    problemDetails.Extensions[kvp.Key] = kvp.Value;
                }

                break;

            case InvalidModelStateException exception:
                problemDetails = ProblemDetailsFactory.CreateValidationProblemDetails(
                    HttpContext,
                    exception.ModelState!,
                    StatusCodes.Status422UnprocessableEntity);

                break;

            case TaskNotFoundException taskNotFoundException:

                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status404NotFound);

                problemDetails.Extensions["taskName"] = taskNotFoundException.TaskName;

                break;

            case EntityNotFoundException entityNotFoundError:

                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status404NotFound);

                problemDetails.Extensions["entityType"] = entityNotFoundError.EntityType;
                problemDetails.Extensions["entityUid"] = entityNotFoundError.EntityUid;

                break;
            case QueryException queryException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                StatusCodes.Status400BadRequest);
                queryException.FailedCommands.ForEach(cmd =>
                {
                    problemDetails.Extensions[cmd.Key] = cmd.Value;
                });
                break;

            case DbUpdateException dbUpdateException:
                problemDetails = BuildDbUpdateProblemDetails(dbUpdateException);

                break;
            case PostgresException postgresException:
                problemDetails = BuildPostgresProblemDetails(postgresException);

                break;
            case IdentityException identityException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    identityException.ErrorMessage);
                break;
            case TooManyRequestsException tooManyRequestsException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    tooManyRequestsException.Message);
                break;
            case UnauthorizedException unauthorizedException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status401Unauthorized,
                    unauthorizedException.Message);
                break;
            case UnauthorizedAccessException unauthorizedAccessException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status401Unauthorized,
                    unauthorizedAccessException.Message);
                break;
            case TranslationConflictException translationConflictException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    translationConflictException.Message);

                problemDetails.Extensions["entityType"] = translationConflictException.EntityType;
                problemDetails.Extensions["entityId"] = translationConflictException.EntityId;
                problemDetails.Extensions["language"] = translationConflictException.Language;
                break;
            case NotTranslatableException notTranslatableException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    notTranslatableException.Message);

                problemDetails.Extensions["entityType"] = notTranslatableException.EntityType;
                break;
            case UnsupportedLanguageException unsupportedLanguageException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    unsupportedLanguageException.Message);

                problemDetails.Extensions["language"] = unsupportedLanguageException.Language;
                problemDetails.Extensions["supportedLanguages"] = unsupportedLanguageException.SupportedLanguages;
                break;
            case KeyNotFoundException keyNotFoundException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    keyNotFoundException.Message);
                break;
            case InvalidOperationException invalidOperationException:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    invalidOperationException.Message);
                break;
            default:
                problemDetails = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status500InternalServerError,
                    error.Message);

                break;
        }

        return new ObjectResult(problemDetails);
    }

    private ProblemDetails BuildDbUpdateProblemDetails(DbUpdateException dbUpdateException)
    {
        if (TryGetUniqueConstraintName(dbUpdateException, out var uniqueConstraintName))
        {
            return ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                StatusCodes.Status409Conflict,
                GetUniqueViolationMessage(uniqueConstraintName));
        }

        if (TryGetPostgresException(dbUpdateException, out var postgresException))
        {
            return BuildPostgresProblemDetails(postgresException);
        }

        return ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            StatusCodes.Status422UnprocessableEntity,
            "The request could not be completed because of invalid or conflicting data.");
    }

    private ProblemDetails BuildPostgresProblemDetails(PostgresException postgresException)
    {
        if (postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                StatusCodes.Status409Conflict,
                GetUniqueViolationMessage(postgresException.ConstraintName));
        }

        return ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            StatusCodes.Status422UnprocessableEntity,
            "The request could not be completed because of invalid or conflicting data.");
    }

    private string GetUniqueViolationMessage(string? constraintName)
    {
        return constraintName?.ToLowerInvariant() switch
        {
            "ix_content_slug_language" => "A content item with this slug already exists for the selected language.",
            "ix_account_name" => "An account with this name already exists.",
            "ix_campaign_name" => "A campaign with this name already exists.",
            "ix_campaign_recipient_campaign_id_contact_id" => "This contact is already a recipient in the campaign.",
            "ix_contact_email" => "A contact with this email already exists.",
            "ix_content_draft_object_id_object_type_created_by_id" => "A draft for this item already exists for the current user.",
            "ix_content_type_uid" => "A content type with this UID already exists.",
            "ix_discount_order_item_id" => "A discount is already assigned to this order item.",
            "ix_domain_name" => "A domain with this name already exists.",
            "ix_email_group_name_language" => "An email group with this name already exists for the selected language.",
            "ix_email_template_name_language" => "An email template with this name already exists for the selected language.",
            "ix_enrichment_quota_usage_provider_key_window_type_window_start" => "Quota usage for this provider and window already exists.",
            "ix_imap_account_host_user_name" => "An IMAP account with this host and username already exists.",
            "ix_ip_details_ip" => "IP details for this IP already exist.",
            "ix_link_uid" => "A link with this UID already exists.",
            "ix_mail_server_name" => "A mail server with this name already exists.",
            "ix_order_item_order_id_line_number" => "An order item with this line number already exists in the order.",
            "ix_order_ref_no" => "An order with this reference number already exists.",
            "ix_promotion_code" => "A promotion with this code already exists.",
            "ix_segment_name" => "A segment with this name already exists.",
            "ix_setting_key_user_id_language" => "A setting with this key, user, and language already exists.",
            "emailindex" => "A user with this email already exists.",
            "usernameindex" => "A user with this username already exists.",
            "rolenameindex" => "A role with this name already exists.",
            _ => "A record with the same unique value already exists.",
        };
    }

    private bool TryGetPostgresException(Exception exception, out PostgresException postgresException)
    {
        if (exception is PostgresException exceptionAsPostgres)
        {
            postgresException = exceptionAsPostgres;
            return true;
        }

        if (exception.InnerException == null)
        {
            postgresException = null!;
            return false;
        }

        return TryGetPostgresException(exception.InnerException, out postgresException);
    }

    private bool TryGetUniqueConstraintName(Exception exception, out string? constraintName)
    {
        constraintName = null;

        if (exception is PostgresException postgresException && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            constraintName = postgresException.ConstraintName;
            return true;
        }

        if (exception.Message.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(exception.Message, "\"(?<constraint>[^\"]+)\"");
            if (match.Success)
            {
                constraintName = match.Groups["constraint"].Value;
            }

            return true;
        }

        if (exception.InnerException == null)
        {
            return false;
        }

        return TryGetUniqueConstraintName(exception.InnerException, out constraintName);
    }
}