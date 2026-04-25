namespace BookStore.Catalog.Infrastructure.Persistence.Configurations;

internal class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasMany(author => author.BooksLink)
            .WithOne(bl => bl.Author)
            .HasForeignKey(bl => bl.AuthorId);
    }
}
