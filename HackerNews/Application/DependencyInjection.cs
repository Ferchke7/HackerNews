using HackerNews.Application.UseCases.Stories;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.Application;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IGetBestStoriesUseCase, GetBestStoriesUseCase>();
            return services;
        }
    }
}
