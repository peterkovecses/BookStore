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

        var key = (Type: typeof(T), PropertyName: sorting.SortBy);
        
        var lambda = Cache.GetOrAdd(key, k =>
        {
            var parameter = Expression.Parameter(k.Type, "entity");
            var property = k.Type.GetProperty(k.PropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property is null)
            {
                throw new ArgumentException($"Property '{k.PropertyName}' not found on type '{k.Type.Name}'");
            }

            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            
            return Expression.Lambda(propertyAccess, parameter);
        });

        var methodName = sorting.IsDescending ? "OrderByDescending" : "OrderBy";
        
        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(T), lambda.ReturnType],
            source.Expression,
            Expression.Quote(lambda));

        return source.Provider.CreateQuery<T>(resultExpression);
    }
}
