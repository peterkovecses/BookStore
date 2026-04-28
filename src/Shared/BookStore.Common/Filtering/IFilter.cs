namespace BookStore.Common.Filtering;

public interface IFilter<T>
{
    ExpressionStarter<T> GetPredicate();
}
