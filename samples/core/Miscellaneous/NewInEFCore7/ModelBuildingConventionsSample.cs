using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NewInEfCore7;

public static class ModelBuildingConventionsSample
{
    public static Task No_foreign_key_index_convention()
    {
        PrintSampleName();
        return ConventionsTest<NoForeignKeyIndexBlogsContext>();
    }

    public static Task Index_for_discriminator_convention()
    {
        PrintSampleName();
        return ConventionsTest<NoForeignKeyIndexBlogsContext>();
    }

    public static Task No_cascade_delete_convention()
    {
        PrintSampleName();
        return ConventionsTest<NoCascadeDeleteBlogsContext>();
    }

    public static async Task Map_members_explicitly_by_attribute_convention()
    {
        PrintSampleName();

        await using var context = new LaundryContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine(context.Model.ToDebugString());
    }

    private static async Task ConventionsTest<TContext>()
        where TContext : BlogsContext, new()
    {
        await using var context = new TContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await context.Seed();
        context.ChangeTracker.Clear();

        Console.WriteLine(context.Model.ToDebugString());

        var blogs = context.Blogs
            .Include(blog => blog.Posts).ThenInclude(post => post.Author)
            .Include(blog => blog.Posts).ThenInclude(post => post.Tags)
            .ToList();

        blogs[0].Name += "Changed";
        blogs[1].Posts[2].Content += "Changed";
        blogs[2].Posts[0].Author!.Contact.Address.Country = "United Kingdon";
        blogs[3].Posts[1].Tags.Add(new Tag("New Tag"));
        blogs[2].Posts[1].Tags.Remove(blogs[2].Posts[1].Tags[0]);

        await context.SaveChangesAsync();
    }

    private static void PrintSampleName([CallerMemberName] string? methodName = null)
    {
        Console.WriteLine($">>>> Sample: {methodName}");
        Console.WriteLine();
    }
}

public class NoForeignKeyIndexBlogsContext : ModelBuildingBlogsContextBase
{
    public NoForeignKeyIndexBlogsContext()
        : base(useSqlite: false)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>(entityTypeBuilder => entityTypeBuilder.HasIndex("BlogId"));

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));

        base.ConfigureConventions(configurationBuilder);
    }
}

public class IndexForDiscriminatorBlogsContext : ModelBuildingBlogsContextBase
{
    public IndexForDiscriminatorBlogsContext()
        : base(useSqlite: false)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Add(_ =>  new DiscriminatorIndexConvention());

        base.ConfigureConventions(configurationBuilder);
    }
}

public class NoCascadeDeleteBlogsContext : ModelBuildingBlogsContextBase
{
    public NoCascadeDeleteBlogsContext()
        : base(useSqlite: false)
    {
    }

    public override MappingStrategy MappingStrategy => MappingStrategy.Tph;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>().HasMany(author => author.Posts).WithOne(post => post.Author).OnDelete(DeleteBehavior.Cascade);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        //var cascadeDeleteConventions = configurationBuilder.Conventions
        // configurationBuilder.Conventions.Add(_ =>  new NoCascadeDeleteConvention());
        configurationBuilder.Conventions.Add(_ =>  new NoCascadeDeleteConvention2());
        configurationBuilder.Conventions.Add(_ =>  new DiscriminatorIndexConvention());
        configurationBuilder.Conventions.Add(_ =>  new MaxLengthConvention());
        //configurationBuilder.Conventions.Remove(typeof(SqlServerOnDeleteConvention));

        base.ConfigureConventions(configurationBuilder);
    }
}

public class NoCascadeDeleteConvention : IForeignKeyAddedConvention, IForeignKeyOwnershipChangedConvention,
    IForeignKeyRequirednessChangedConvention, ISkipNavigationForeignKeyChangedConvention
{
    public void ProcessForeignKeyAdded(
        IConventionForeignKeyBuilder foreignKeyBuilder,
        IConventionContext<IConventionForeignKeyBuilder> context)
        => foreignKeyBuilder.OnDelete(DeleteBehavior.ClientSetNull);

    public void ProcessForeignKeyOwnershipChanged(IConventionForeignKeyBuilder relationshipBuilder, IConventionContext<bool?> context)
        => relationshipBuilder.OnDelete(DeleteBehavior.ClientSetNull);

    public void ProcessForeignKeyRequirednessChanged(IConventionForeignKeyBuilder relationshipBuilder, IConventionContext<bool?> context)
        => relationshipBuilder.OnDelete(DeleteBehavior.ClientSetNull);

    public void ProcessSkipNavigationForeignKeyChanged(
        IConventionSkipNavigationBuilder skipNavigationBuilder, IConventionForeignKey? foreignKey, IConventionForeignKey? oldForeignKey,
        IConventionContext<IConventionForeignKey> context)
        => foreignKey?.SetDeleteBehavior(DeleteBehavior.ClientSetNull);
}

public class NoCascadeDeleteConvention2 : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var foreignKey in entityType.GetDeclaredForeignKeys())
            {
                foreignKey.Builder.Metadata.SetDeleteBehavior(DeleteBehavior.ClientSetNull);

                //((IMutableForeignKey)foreignKey.Builder.Metadata).DeleteBehavior = DeleteBehavior.ClientSetNull;
            }
        }
    }
}

#region DiscriminatorIndexConvention
public class DiscriminatorIndexConvention : IEntityTypeBaseTypeChangedConvention
{
    public void ProcessEntityTypeBaseTypeChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionEntityType? newBaseType,
        IConventionEntityType? oldBaseType,
        IConventionContext<IConventionEntityType> context)
    {
        var discriminatorProperty = entityTypeBuilder.Metadata.FindDiscriminatorProperty();
        if (discriminatorProperty != null)
        {
            discriminatorProperty.SetMaxLength(24);
            discriminatorProperty.DeclaringEntityType.Builder
                .HasIndex(new[] { discriminatorProperty }, "DiscriminatorIndex");
        }
    }
}
#endregion

public class AttributeBasedPropertyDiscoveryConvention : PropertyDiscoveryConvention
{
    public AttributeBasedPropertyDiscoveryConvention(ProviderConventionSetBuilderDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
        => Process(entityTypeBuilder);

    public override void ProcessEntityTypeBaseTypeChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionEntityType? newBaseType,
        IConventionEntityType? oldBaseType,
        IConventionContext<IConventionEntityType> context)
    {
        if ((newBaseType == null
             || oldBaseType != null)
            && entityTypeBuilder.Metadata.BaseType == newBaseType)
        {
            Process(entityTypeBuilder);
        }
    }

    private void Process(IConventionEntityTypeBuilder entityTypeBuilder)
    {
        foreach (var memberInfo in GetRuntimeMembers())
        {
            if (Attribute.IsDefined(memberInfo, typeof(PersistAttribute), inherit: true))
            {
                entityTypeBuilder.Property(memberInfo);
            }
        }

        IEnumerable<MemberInfo> GetRuntimeMembers()
        {
            var clrType = entityTypeBuilder.Metadata.ClrType;

            foreach (var property in clrType.GetRuntimeProperties()
                         .Where(p => p.GetMethod != null && !p.GetMethod.IsStatic))
            {
                yield return property;
            }

            foreach (var property in clrType.GetRuntimeFields())
            {
                yield return property;
            }
        }
    }
}

public class MaxLengthConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var property in modelBuilder.Metadata.GetEntityTypes()
                     .SelectMany(
                         entityType => entityType.GetDeclaredProperties()
                             .Where(
                                 property => property.ClrType == typeof(string)
                                             && property.GetMaxLength() == null)))
        {
            property.SetMaxLength(500);
        }
    }
}

public class ValidateTenantIdConvention : IModelFinalizedConvention
{
    public IModel ProcessModelFinalized(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            var tenantIdProperty = entityType.FindProperty("TenantId");
            if (tenantIdProperty == null
                || tenantIdProperty.ClrType != typeof(int))
            {
                throw new InvalidOperationException($"Entity type {entityType.DisplayName()} does not have an int TenantId property.");
            }
        }

        return model;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PersistAttribute : Attribute
{
}

public abstract class ModelBuildingBlogsContextBase : BlogsContext
{
    protected ModelBuildingBlogsContextBase(bool useSqlite)
        : base(useSqlite)
    {
    }

    public override MappingStrategy MappingStrategy => MappingStrategy.Tph;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>().OwnsOne(
            author => author.Contact, ownedNavigationBuilder =>
            {
                ownedNavigationBuilder.OwnsOne(contactDetails => contactDetails.Address);
            });

        modelBuilder.Entity<Post>().OwnsOne(
            post => post.Metadata, ownedNavigationBuilder =>
            {
                ownedNavigationBuilder.OwnsMany(metadata => metadata.TopSearches);
                ownedNavigationBuilder.OwnsMany(metadata => metadata.TopGeographies);
                ownedNavigationBuilder.OwnsMany(
                    metadata => metadata.Updates,
                    ownedOwnedNavigationBuilder => ownedOwnedNavigationBuilder.OwnsMany(update => update.Commits));
            });

        base.OnModelCreating(modelBuilder);
    }
}

public class LaundryContext : DbContext
{
    public DbSet<LaundryBasket> LaundryBaskets => Set<LaundryBasket>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .ReplaceService<IModelValidator, NoPropertyValidationModelValidator>()
            .UseSqlServer(@$"Server=(localdb)\mssqllocaldb;Database={GetType().Name}");

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Replace<PropertyDiscoveryConvention>(
            p => new AttributeBasedPropertyDiscoveryConvention(p.GetRequiredService<ProviderConventionSetBuilderDependencies>()));

        configurationBuilder.Conventions.Add(_ => new ValidateTenantIdConvention());
    }
}

public class NoPropertyValidationModelValidator : SqlServerModelValidator
{
    public NoPropertyValidationModelValidator(
        ModelValidatorDependencies dependencies, RelationalModelValidatorDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    protected override void ValidatePropertyMapping(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
    }
}

public class LaundryBasket
{
    [Persist]
    [Key]
    private readonly int _id;

    [Persist]
    public int TenantId { get; init; }

    public bool IsClean { get; set; }

    public List<Garment> Garments { get; } = new();
}

public class Garment
{
    public Garment(string name, string color)
    {
        Name = name;
        Color = color;
    }

    [Persist]
    [Key]
    private readonly int _id;

    [Persist]
    public int TenantId { get; init; }

    [Persist]
    public string Name { get; }

    [Persist]
    public string Color { get; }

    public bool IsClean { get; set; }

    public LaundryBasket? Basket { get; set; }
}
