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

        app.MapPost("/submit", async Task<IResult> (
            [FromForm] string text,
            [FromForm] IFormFile file,
            ExternalApiClient externalApiClient,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Results.BadRequest(new { message = "Text is required." });
            }

            if (file.Length == 0)
            {
                return Results.BadRequest(new { message = "A non-empty file is required." });
            }

            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);

            try
            {
                var result = await externalApiClient.SendAsync(
                    new ExternalApiRequest(
                        text,
                        file.FileName,
                        file.ContentType,
                        Convert.ToBase64String(memoryStream.ToArray())),
                    cancellationToken);

                return result.IsSuccessStatusCode
                    ? Results.Ok(new
                    {
                        message = "Submission forwarded successfully.",
                        externalStatusCode = (int)result.StatusCode,
                        externalResponse = result.ResponseBody
                    })
                    : Results.Json(
                        new
                        {
                            message = "The external API call failed.",
                            externalStatusCode = (int)result.StatusCode,
                            externalResponse = result.ResponseBody
                        },
                        statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .DisableAntiforgery();

        app.Run();
    }
}
