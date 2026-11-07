using System.Globalization;

namespace BookStore.Catalog.Features.Books.GetBookDetails;

internal sealed class GetBookDetailsValidator : AbstractValidator<GetBookDetailsQuery>
{
    public GetBookDetailsValidator()
    {
        RuleFor(query => query.Id)
            .Must(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            .WithMessage("Id must be a positive integer.");
    }
}
