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
                StatusCodes.Status422UnprocessableEntity,
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
                StatusCodes.Status422UnprocessableEntity,
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