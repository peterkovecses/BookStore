using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using BookStore.Catalog.Infrastructure.Persistence.Interceptors;
using BookStore.Catalog.Domain.Common;

namespace BookStore.Catalog.UnitTests.Infrastructure.Persistence.Interceptors;

public class AuditableEntityInterceptorTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly AuditableEntityInterceptor _sut;

    public AuditableEntityInterceptorTests()
    {
        _timeProvider = new FakeTimeProvider();
        _sut = new AuditableEntityInterceptor(_timeProvider);
    }

    private class TestAuditableEntity : IAuditableEntity
    {
        public int Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? LastModifiedUtc { get; set; }
        public string? LastModifiedBy { get; set; }
    }

    private class TestDbContext : DbContext
    {
        private readonly AuditableEntityInterceptor _interceptor;

        public TestDbContext(DbContextOptions<TestDbContext> options, AuditableEntityInterceptor interceptor)
            : base(options)
        {
            _interceptor = interceptor;
        }

        public DbSet<TestAuditableEntity> AuditableEntities => Set<TestAuditableEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_interceptor);
        }
    }

    [Fact]
    public async Task SavingChanges_WhenEntityIsAdded_ShouldSetCreatedAndLastModified()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(utcNow);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options, _sut);
        var entity = new TestAuditableEntity();

        // Act
        context.AuditableEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.CreatedUtc.Should().Be(utcNow.DateTime);
        entity.CreatedBy.Should().Be("System");
        entity.LastModifiedUtc.Should().Be(utcNow.DateTime);
        entity.LastModifiedBy.Should().Be("System");
    }

    [Fact]
    public async Task SavingChanges_WhenEntityIsModified_ShouldOnlySetLastModified()
    {
        // Arrange
        var initialUtc = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);
        var modifiedUtc = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(initialUtc);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using (var context = new TestDbContext(options, _sut))
        {
            var entity = new TestAuditableEntity();
            context.AuditableEntities.Add(entity);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _timeProvider.SetUtcNow(modifiedUtc);

        using var actContext = new TestDbContext(options, _sut);
        var entityToModify = await actContext.AuditableEntities.SingleAsync(TestContext.Current.CancellationToken);

        // Act
        entityToModify.CreatedUtc.Should().Be(initialUtc.DateTime);
        entityToModify.CreatedBy.Should().Be("System");

        entityToModify.Id = entityToModify.Id; 
        actContext.Entry(entityToModify).State = EntityState.Modified;
        await actContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var actual = await actContext.AuditableEntities.SingleAsync(TestContext.Current.CancellationToken);
        actual.CreatedUtc.Should().Be(initialUtc.DateTime);
        actual.CreatedBy.Should().Be("System");
        actual.LastModifiedUtc.Should().Be(modifiedUtc.DateTime);
        actual.LastModifiedBy.Should().Be("System");
    }

    [Fact]
    public void SavingChanges_WhenContextIsNull_ShouldReturnWithoutThrowing()
    {
        // Arrange
        var context = (DbContext?)null;
        
        // Act & Assert
        var act = () =>
        {
            var method = typeof(AuditableEntityInterceptor)
                .GetMethod("UpdateEntities", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            method!.Invoke(_sut, [context]);
        };

        act.Should().NotThrow();
    }
}
