using HackerNews.Api.Contracts;
using HackerNews.Api.Errors;
using HackerNews.Application.UseCases.Stories;
using Microsoft.AspNetCore.Http;

namespace HackerNews.Api.Endpoints.Stories;

public static class GetBestStoriesHandler
{
    public static async Task<IResult> Handle(
        [AsParameters] GetBestStoriesRequest request,
        IGetBestStoriesUseCase useCase,
        CancellationToken ct)
    {
        if (!request.N.HasValue)
        {
            return Results.BadRequest(ApiErrorResponse.From(RequestErrors.MissingStoryCount));
        }

        var result = await useCase.HandleAsync(request.N.Value, ct);

        if (result.IsFailed)
        {
            var error = result.Errors.FirstOrDefault() ?? ApiErrors.Unexpected;
            return Results.BadRequest(ApiErrorResponse.From(error));
        }

        return Results.Ok(result.Value);
    }
}
