namespace BookStore.Catalog.Infrastructure.Persistence.Configurations;

internal class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(b => b.Publisher)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.Price)
            .HasPrecision(18, 2);

        builder.Property(b => b.ImageName)
            .HasMaxLength(255);

        builder.HasOne(book => book.Promotion)
            .WithOne(promotion => promotion.Book)
            .HasForeignKey<PriceOffer>(promotion => promotion.BookId);

        builder.HasMany(book => book.Reviews)
            .WithOne(review => review.Book)
            .HasForeignKey(review => review.BookId);

        builder.HasMany(book => book.Tags)
            .WithMany(tag => tag.Books);

        builder.HasMany(book => book.AuthorsLink)
            .WithOne(al => al.Book)
            .HasForeignKey(al => al.BookId);
    }
}
