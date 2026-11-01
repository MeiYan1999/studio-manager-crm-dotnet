using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenantCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Studios",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    StudioCode = table.Column<string>(maxLength: 12, nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Slug = table.Column<string>(nullable: true),
                    CustomDomain = table.Column<string>(nullable: true),
                    CustomDomainVerified = table.Column<bool>(nullable: false),
                    DomainVerificationToken = table.Column<string>(nullable: false),
                    CurrentPlan = table.Column<int>(nullable: false),
                    BillingCycle = table.Column<int>(nullable: false),
                    SubscriptionExpiresAt = table.Column<DateTimeOffset>(nullable: true),
                    IsTrialGift = table.Column<bool>(nullable: false),
                    MaxUsers = table.Column<int>(nullable: false),
                    MonthlyPcFilterQuota = table.Column<int>(nullable: false),
                    CurrentMonthPcFilterUsed = table.Column<int>(nullable: false),
                    PcFilterMonth = table.Column<string>(nullable: false),
                    QuickLocationsJson = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Studios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Album",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    ShareToken = table.Column<string>(nullable: false),
                    Title = table.Column<string>(nullable: false),
                    ClientName = table.Column<string>(nullable: false),
                    CustomerId = table.Column<string>(nullable: false),
                    EventDate = table.Column<string>(nullable: false),
                    Location = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: false),
                    DriveFolderUrl = table.Column<string>(nullable: false),
                    DriveFolderId = table.Column<string>(nullable: false),
                    IncludeSubfolders = table.Column<bool>(nullable: false),
                    AllowFavorites = table.Column<bool>(nullable: false),
                    AllowDownloads = table.Column<bool>(nullable: false),
                    IsPublished = table.Column<bool>(nullable: false),
                    IsDemo = table.Column<bool>(nullable: false),
                    CoverPhotoId = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
                    FavoritesUpdatedAt = table.Column<DateTimeOffset>(nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(nullable: true),
                    ClientNote = table.Column<string>(nullable: false),
                    Photos = table.Column<string>(nullable: false),
                    Selections = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Album", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Album_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecord",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    StaffId = table.Column<string>(nullable: false),
                    Date = table.Column<string>(nullable: false),
                    In = table.Column<string>(nullable: false),
                    Out = table.Column<string>(nullable: false),
                    Note = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecord", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AttendanceRecord_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogEntry",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Module = table.Column<string>(nullable: false),
                    Action = table.Column<string>(nullable: false),
                    Summary = table.Column<string>(nullable: false),
                    ActorId = table.Column<string>(nullable: false),
                    ActorName = table.Column<string>(nullable: false),
                    ActorRole = table.Column<string>(nullable: false),
                    EntityId = table.Column<string>(nullable: false),
                    Details = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntry", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AuditLogEntry_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Booking",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Code = table.Column<string>(nullable: false),
                    CustomerId = table.Column<string>(nullable: false),
                    PackageId = table.Column<string>(nullable: false),
                    Date = table.Column<string>(nullable: false),
                    StartTime = table.Column<string>(nullable: false),
                    Hours = table.Column<decimal>(nullable: false),
                    Location = table.Column<string>(nullable: false),
                    PhotographerId = table.Column<string>(nullable: false),
                    Status = table.Column<string>(nullable: false),
                    Notes = table.Column<string>(nullable: false),
                    AlbumId = table.Column<string>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    StatusHistory = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Booking", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Booking_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contract",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Code = table.Column<string>(nullable: false),
                    CustomerId = table.Column<string>(nullable: false),
                    BookingId = table.Column<string>(nullable: false),
                    Service = table.Column<string>(nullable: false),
                    Total = table.Column<decimal>(nullable: false),
                    Schedule = table.Column<string>(nullable: false),
                    Terms = table.Column<string>(nullable: false),
                    Status = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contract", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Contract_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Customer",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Type = table.Column<string>(nullable: false),
                    Phone = table.Column<string>(nullable: false),
                    Email = table.Column<string>(nullable: false),
                    Address = table.Column<string>(nullable: false),
                    Source = table.Column<string>(nullable: false),
                    Notes = table.Column<string>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customer", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Customer_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseEntry",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Kind = table.Column<string>(nullable: false),
                    Date = table.Column<string>(nullable: false),
                    Category = table.Column<string>(nullable: false),
                    Amount = table.Column<decimal>(nullable: false),
                    Note = table.Column<string>(nullable: false),
                    Photos = table.Column<string>(nullable: false),
                    CreatedById = table.Column<string>(nullable: false),
                    CreatedByName = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseEntry", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ExpenseEntry_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationEntry",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Type = table.Column<string>(nullable: false),
                    Title = table.Column<string>(nullable: false),
                    Body = table.Column<string>(nullable: false),
                    Link = table.Column<string>(nullable: false),
                    IsRead = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEntry", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_NotificationEntry_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Order",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Code = table.Column<string>(nullable: false),
                    CustomerId = table.Column<string>(nullable: false),
                    BookingId = table.Column<string>(nullable: false),
                    Items = table.Column<string>(nullable: false),
                    Discount = table.Column<decimal>(nullable: false),
                    Status = table.Column<string>(nullable: false),
                    Notes = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Order", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Order_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Outfit",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Category = table.Column<string>(nullable: false),
                    Size = table.Column<string>(nullable: false),
                    Color = table.Column<string>(nullable: false),
                    Quantity = table.Column<int>(nullable: false),
                    Note = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outfit", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Outfit_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutfitLoan",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    OutfitId = table.Column<string>(nullable: false),
                    Quantity = table.Column<int>(nullable: false),
                    BorrowerType = table.Column<string>(nullable: false),
                    CustomerId = table.Column<string>(nullable: false),
                    StaffId = table.Column<string>(nullable: false),
                    BookingId = table.Column<string>(nullable: false),
                    BorrowDate = table.Column<string>(nullable: false),
                    DueDate = table.Column<string>(nullable: false),
                    ReturnDate = table.Column<string>(nullable: false),
                    Deposit = table.Column<decimal>(nullable: false),
                    Notes = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutfitLoan", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_OutfitLoan_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Code = table.Column<string>(nullable: false),
                    OrderId = table.Column<string>(nullable: false),
                    CustomerId = table.Column<string>(nullable: false),
                    Amount = table.Column<decimal>(nullable: false),
                    Method = table.Column<string>(nullable: false),
                    PaidAt = table.Column<string>(nullable: false),
                    Note = table.Column<string>(nullable: false),
                    ActorId = table.Column<string>(nullable: false),
                    ActorName = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Payment_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEntry",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Month = table.Column<string>(nullable: false),
                    StaffId = table.Column<string>(nullable: false),
                    Base = table.Column<decimal>(nullable: false),
                    Hours = table.Column<decimal>(nullable: false),
                    CommissionBase = table.Column<decimal>(nullable: false),
                    Commission = table.Column<decimal>(nullable: false),
                    Total = table.Column<decimal>(nullable: false),
                    Paid = table.Column<bool>(nullable: false),
                    PaidAt = table.Column<string>(nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEntry", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PayrollEntry_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PcFilterUsage",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Month = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcFilterUsage", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PcFilterUsage_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceGroup",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Title = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: false),
                    Items = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceGroup", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PriceGroup_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServicePackage",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Category = table.Column<string>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    Hours = table.Column<decimal>(nullable: false),
                    Includes = table.Column<string>(nullable: false),
                    Notes = table.Column<string>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePackage", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ServicePackage_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignment",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    StaffId = table.Column<string>(nullable: false),
                    Date = table.Column<string>(nullable: false),
                    Shift = table.Column<string>(nullable: false),
                    Note = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignment", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ShiftAssignment_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffMember",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Role = table.Column<string>(nullable: false),
                    Phone = table.Column<string>(nullable: false),
                    Email = table.Column<string>(nullable: false),
                    UserId = table.Column<string>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    BaseSalary = table.Column<decimal>(nullable: false),
                    CommissionRate = table.Column<decimal>(nullable: false),
                    PayMode = table.Column<string>(nullable: false),
                    HourlyRate = table.Column<decimal>(nullable: false),
                    Shift = table.Column<string>(nullable: false),
                    Notes = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMember", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_StaffMember_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudioConcept",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Category = table.Column<string>(nullable: false),
                    AlbumId = table.Column<string>(nullable: false),
                    IsPublished = table.Column<bool>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: false),
                    Fee = table.Column<decimal>(nullable: false),
                    Status = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioConcept", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_StudioConcept_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudioUser",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Username = table.Column<string>(nullable: false),
                    DisplayName = table.Column<string>(nullable: false),
                    Role = table.Column<string>(nullable: false),
                    PasswordHash = table.Column<string>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    MustChangePassword = table.Column<bool>(nullable: false),
                    StaffId = table.Column<string>(nullable: false),
                    PermissionsConfigured = table.Column<bool>(nullable: false),
                    Permissions = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioUser", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_StudioUser_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionTransaction",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Plan = table.Column<int>(nullable: false),
                    BillingCycle = table.Column<int>(nullable: false),
                    Amount = table.Column<decimal>(nullable: false),
                    PaymentCode = table.Column<string>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(nullable: true),
                    SePayReference = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionTransaction", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SubscriptionTransaction_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantWorkspace",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    MetadataJson = table.Column<string>(nullable: false),
                    Revision = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantWorkspace", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_TenantWorkspace_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Album_TenantId_ShareToken",
                table: "Album",
                columns: new[] { "TenantId", "ShareToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Studios_CustomDomain",
                table: "Studios",
                column: "CustomDomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Studios_Slug",
                table: "Studios",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Studios_StudioCode",
                table: "Studios",
                column: "StudioCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudioUser_TenantId_Username",
                table: "StudioUser",
                columns: new[] { "TenantId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTransaction_PaymentCode",
                table: "SubscriptionTransaction",
                column: "PaymentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTransaction_SePayReference",
                table: "SubscriptionTransaction",
                column: "SePayReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Album");

            migrationBuilder.DropTable(
                name: "AttendanceRecord");

            migrationBuilder.DropTable(
                name: "AuditLogEntry");

            migrationBuilder.DropTable(
                name: "Booking");

            migrationBuilder.DropTable(
                name: "Contract");

            migrationBuilder.DropTable(
                name: "Customer");

            migrationBuilder.DropTable(
                name: "ExpenseEntry");

            migrationBuilder.DropTable(
                name: "NotificationEntry");

            migrationBuilder.DropTable(
                name: "Order");

            migrationBuilder.DropTable(
                name: "Outfit");

            migrationBuilder.DropTable(
                name: "OutfitLoan");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "PayrollEntry");

            migrationBuilder.DropTable(
                name: "PcFilterUsage");

            migrationBuilder.DropTable(
                name: "PriceGroup");

            migrationBuilder.DropTable(
                name: "ServicePackage");

            migrationBuilder.DropTable(
                name: "ShiftAssignment");

            migrationBuilder.DropTable(
                name: "StaffMember");

            migrationBuilder.DropTable(
                name: "StudioConcept");

            migrationBuilder.DropTable(
                name: "StudioUser");

            migrationBuilder.DropTable(
                name: "SubscriptionTransaction");

            migrationBuilder.DropTable(
                name: "TenantWorkspace");

            migrationBuilder.DropTable(
                name: "Studios");
        }
    }
}
