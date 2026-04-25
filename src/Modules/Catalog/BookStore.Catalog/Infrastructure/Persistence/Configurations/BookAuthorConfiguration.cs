namespace BookStore.Catalog.Infrastructure.Persistence.Configurations;

internal class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthor>
{
    public void Configure(EntityTypeBuilder<BookAuthor> builder)
    {
        builder.HasKey(ba => new { ba.BookId, ba.AuthorId });

        builder.Property(ba => ba.Order)
            .IsRequired();
    }
}
