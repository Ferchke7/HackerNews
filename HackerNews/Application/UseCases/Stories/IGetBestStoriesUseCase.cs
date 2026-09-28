using FluentResults;
using HackerNews.Application.Models;

namespace HackerNews.Application.UseCases.Stories;

public interface IGetBestStoriesUseCase
{
    Task<Result<IReadOnlyList<StoryResponse>>> HandleAsync(int count, CancellationToken cancellationToken = default);
}
