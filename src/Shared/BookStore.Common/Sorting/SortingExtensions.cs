namespace BookStore.Common.Sorting;

public static class SortingExtensions
{
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), LambdaExpression> Cache = new();

    public static IQueryable<T> ApplySorting<T>(this IQueryable<T> source, ISorting sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting.SortBy))
        {
            return source;
        }

        var lambda = GetOrCreatePropertyLambda<T>(sorting.SortBy);

        return source.ApplyOrder(sorting.IsDescending, lambda);
    }

    public static IQueryable<T> ApplySorting<T>(
        this IQueryable<T> source,
        ISorting sorting,
        IReadOnlyDictionary<string, LambdaExpression> customExpressions)
    {
        if (string.IsNullOrWhiteSpace(sorting.SortBy))
        {
            return source;
        }

        if (!customExpressions.TryGetValue(sorting.SortBy, out var lambda))
        {
            throw new ArgumentException($"No sort expression mapped for property '{sorting.SortBy}' on type '{typeof(T).Name}'");
        }

        return source.ApplyOrder(sorting.IsDescending, lambda);
    }

    private static IQueryable<T> ApplyOrder<T>(this IQueryable<T> source, bool isDescending, LambdaExpression lambda)
    {
        var methodName = isDescending ? "OrderByDescending" : "OrderBy";

        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(T), lambda.ReturnType],
            source.Expression,
            Expression.Quote(lambda));

        return source.Provider.CreateQuery<T>(resultExpression);
    }

    private static LambdaExpression GetOrCreatePropertyLambda<T>(string propertyName) =>
        Cache.GetOrAdd((typeof(T), propertyName), k =>
        {
            var parameter = Expression.Parameter(k.Type, "entity");
            var property = k.Type.GetProperty(k.PropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"Property '{k.PropertyName}' not found on type '{k.Type.Name}'");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);

            return Expression.Lambda(propertyAccess, parameter);
        });
}

