using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace ApiClientExample;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<ExternalApiOptions>(builder.Configuration.GetSection(ExternalApiOptions.SectionName));
        builder.Services.AddHttpClient<ExternalApiClient>();

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapPost("/submit", OnSubmit)
        .DisableAntiforgery();

        app.Run();
    }

    static async Task<IResult> OnSubmit(
        [FromForm] string text,
        [FromForm] IFormFile? file,
        ExternalApiClient externalApiClient,
        CancellationToken cancellationToken)
    {
        ExternalApiRequest externalApiRequest;
        if (file == null)
        {
            externalApiRequest = new(text);
        }
        else
        {
            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            externalApiRequest = new ExternalApiRequest(
                text,
                file.FileName,
                file.ContentType,
                Convert.ToBase64String(memoryStream.ToArray()));
        }

        try
        {
            var result = await externalApiClient.SendAsync(
                externalApiRequest,
                cancellationToken);

            var responseObject = result.IsJson && TryParseJson(result.ResponseBody, out object? jsonObj) ? jsonObj : result.ResponseBody;

            return result.IsSuccessStatusCode
                ? Results.Ok(new
                {
                    message = "Submission forwarded successfully.",
                    externalStatusCode = (int)result.StatusCode,
                    externalResponse = responseObject
                })
                : Results.Json(
                    new
                    {
                        message = "The external API call failed.",
                        externalStatusCode = (int)result.StatusCode,
                        externalResponse = responseObject
                    },
                    statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static bool TryParseJson(string responseBody, [NotNullWhen(true)] out object? jsonObj)
    {
        try
        {
            jsonObj = System.Text.Json.JsonDocument.Parse(responseBody);
            return true;
        }
        catch //(Exception ex)
        {
            jsonObj = null;
            return false;
        }
    }
}
