namespace BookStore.Common;

public class QueryCachingBehavior<TRequest, TResponse>(HybridCache cache)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICachedQuery cachedQuery)
        {
            return await next();
        }

        TResponse? result;
        try
        {
            HybridCacheEntryOptions? options = null;
            if (cachedQuery.Expiration is not null)
            {
                options = new HybridCacheEntryOptions
                {
                    Expiration = cachedQuery.Expiration.Value
                };
            }

            result = await cache.GetOrCreateAsync(
                cachedQuery.CacheKey,
                async token =>
                {
                    var response = await next();
                    if (!response.IsSuccess)
                    {
                        throw new CachingException(response);
                    }

                    return response;
                },
                options,
                cancellationToken: cancellationToken);
        }
        catch (CachingException ex)
        {
            return (TResponse)ex.Response;
        }

        return result!;
    }

    private sealed class CachingException(Result response) : Exception
    {
        public Result Response { get; } = response;
    }
}
