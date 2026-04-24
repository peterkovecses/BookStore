namespace BookStore.Catalog.Persistence.Configurations;

internal class PriceOfferConfiguration : IEntityTypeConfiguration<PriceOffer>
{
    public void Configure(EntityTypeBuilder<PriceOffer> builder)
    {
        builder.HasKey(po => po.Id);

        builder.Property(po => po.NewPrice)
            .HasPrecision(18, 2);

        builder.Property(po => po.Text)
            .HasMaxLength(200)
            .IsRequired();
    }
}
