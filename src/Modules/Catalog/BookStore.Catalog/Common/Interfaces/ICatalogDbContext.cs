namespace BookStore.Catalog.Common.Interfaces;

internal interface ICatalogDbContext
{
    DbSet<Book> Books { get; }
    DbSet<Author> Authors { get; }
    DbSet<PriceOffer> PriceOffers { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Tag> Tags { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
