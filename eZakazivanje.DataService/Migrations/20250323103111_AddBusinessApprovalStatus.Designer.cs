using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using eZakazivanje.DataService.Data;

#nullable disable

namespace eZakazivanje.DataService.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250323103111_AddBusinessApprovalStatus")]
    partial class AddBusinessApprovalStatus
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("eZakazivanje.Entity.DbSet.Bussiness", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<int>("ApprovalStatus")
                    .HasColumnType("integer");

                b.Property<string>("Description")
                    .HasColumnType("text");

                b.Property<TimeSpan?>("EndWorkHours")
                    .HasColumnType("interval");

                b.Property<bool?>("IsActive")
                    .HasColumnType("boolean");

                b.Property<List<string>>("ImageUrls")
                    .HasColumnType("text[]");

                b.Property<string>("Name")
                    .HasColumnType("text");

                b.Property<string>("PIB")
                    .HasColumnType("text");

                b.Property<string>("PhoneNumber")
                    .HasColumnType("text");

                b.Property<TimeSpan?>("StartWorkHours")
                    .HasColumnType("interval");

                b.Property<TimeSpan?>("Tick")
                    .HasColumnType("interval");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTime>("UpdatedAt")
                    .HasColumnType("timestamp with time zone");

                b.HasKey("Id");

                b.ToTable("Businesses");
            });
#pragma warning restore 612, 618
        }
    }
} 