using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace LawyerPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LawyerPlatformDbContext))]
sealed partial class LawyerPlatformDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.10")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("LawyerPlatform.Domain.Catalog.CatalogItem", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uniqueidentifier");
            entity.Property<DateTime>("CreatedOnUtc").HasColumnType("datetime2");
            entity.Property<DateTime?>("DeletedOnUtc").HasColumnType("datetime2");
            entity.Property<string>("Description").HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            entity.Property<bool>("IsDeleted").HasColumnType("bit");
            entity.Property<DateTime?>("ModifiedOnUtc").HasColumnType("datetime2");
            entity.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<decimal>("Price").HasPrecision(18, 2).HasColumnType("decimal(18,2)");
            entity.Property<DateTime?>("RestoredOnUtc").HasColumnType("datetime2");
            entity.Property<byte[]>("RowVersion").IsConcurrencyToken().IsRequired().ValueGeneratedOnAddOrUpdate().HasColumnType("rowversion");
            entity.HasKey("Id");
            entity.HasIndex("Name");
            entity.ToTable("CatalogItems");
        });
#pragma warning restore 612, 618
    }
}
