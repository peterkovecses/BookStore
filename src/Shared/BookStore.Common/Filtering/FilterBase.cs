namespace BookStore.Common.Filtering;

public abstract class FilterBase<T> : IFilter<T>
{
    public virtual ExpressionStarter<T> GetPredicate()
    {
        return PredicateBuilder.New<T>(true);
    }
}
