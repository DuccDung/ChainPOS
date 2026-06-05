/*
    SaaS Store Management + POS + Inventory + Staff + Reporting schema
    Target: Microsoft SQL Server

    Business rules supported by this schema:
    - ADMIN manages the SaaS platform, owners, subscription plans, and system revenue.
    - OWNER owns one tenant / chain and manages stores, staff, products, inventory, POS, and reports.
    - STAFF does not self-register. STAFF users are created by OWNER and assigned to stores through UserStores.
    - Tenant = one owner business / store chain.
    - Store belongs to Tenant.
    - Products, inventory, orders, payments, shifts, and reports are tenant-scoped.
    - Store-level data includes StoreId.

    Usage:
    1. Create/select your application database first.
    2. Run this script in SSMS, Azure Data Studio, or sqlcmd.

    Optional:
    -- CREATE DATABASE StoreSaasDb;
    -- GO
    -- USE StoreSaasDb;
    -- GO
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 50000, 'Please select an application database before running this script.', 1;
END;
GO

/* ============================================================
   ASP.NET Core Identity base tables
   ============================================================ */

IF OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetRoles] (
        [Id] NVARCHAR(450) NOT NULL,
        [Name] NVARCHAR(256) NULL,
        [NormalizedName] NVARCHAR(256) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUsers] (
        [Id] NVARCHAR(450) NOT NULL,
        [UserName] NVARCHAR(256) NULL,
        [NormalizedUserName] NVARCHAR(256) NULL,
        [Email] NVARCHAR(256) NULL,
        [NormalizedEmail] NVARCHAR(256) NULL,
        [EmailConfirmed] BIT NOT NULL CONSTRAINT [DF_AspNetUsers_EmailConfirmed] DEFAULT (0),
        [PasswordHash] NVARCHAR(MAX) NULL,
        [SecurityStamp] NVARCHAR(MAX) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        [PhoneNumber] NVARCHAR(50) NULL,
        [PhoneNumberConfirmed] BIT NOT NULL CONSTRAINT [DF_AspNetUsers_PhoneNumberConfirmed] DEFAULT (0),
        [TwoFactorEnabled] BIT NOT NULL CONSTRAINT [DF_AspNetUsers_TwoFactorEnabled] DEFAULT (0),
        [LockoutEnd] DATETIMEOFFSET(7) NULL,
        [LockoutEnabled] BIT NOT NULL CONSTRAINT [DF_AspNetUsers_LockoutEnabled] DEFAULT (1),
        [AccessFailedCount] INT NOT NULL CONSTRAINT [DF_AspNetUsers_AccessFailedCount] DEFAULT (0),

        [FullName] NVARCHAR(200) NULL,
        [AvatarUrl] NVARCHAR(500) NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_AspNetUsers_Status] DEFAULT (N'Active'),
        [TenantId] UNIQUEIDENTIFIER NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_AspNetUsers_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        [LastLoginAt] DATETIME2(7) NULL,

        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AspNetUsers_Status]
            CHECK ([Status] IN (N'Active', N'Inactive', N'Locked', N'Pending'))
    );
END;
GO

IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RoleId] NVARCHAR(450) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId]
            FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[AspNetUserClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[AspNetUserLogins]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins] (
        [LoginProvider] NVARCHAR(128) NOT NULL,
        [ProviderKey] NVARCHAR(128) NOT NULL,
        [ProviderDisplayName] NVARCHAR(MAX) NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[AspNetUserRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles] (
        [UserId] NVARCHAR(450) NOT NULL,
        [RoleId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId]
            FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[AspNetUserTokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens] (
        [UserId] NVARCHAR(450) NOT NULL,
        [LoginProvider] NVARCHAR(128) NOT NULL,
        [Name] NVARCHAR(128) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

/* ============================================================
   Tenants, stores, and staff-store assignment
   ============================================================ */

IF OBJECT_ID(N'[dbo].[Tenants]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Tenants] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Tenants_Id] DEFAULT (NEWSEQUENTIALID()),
        [Name] NVARCHAR(200) NOT NULL,
        [OwnerUserId] NVARCHAR(450) NULL,
        [TaxCode] NVARCHAR(50) NULL,
        [Address] NVARCHAR(500) NULL,
        [Phone] NVARCHAR(50) NULL,
        [Email] NVARCHAR(256) NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Tenants_Status] DEFAULT (N'Active'),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Tenants_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Tenants_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tenants_AspNetUsers_OwnerUserId]
            FOREIGN KEY ([OwnerUserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [CK_Tenants_Status]
            CHECK ([Status] IN (N'Active', N'Suspended', N'Cancelled', N'Trial'))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_AspNetUsers_Tenants_TenantId'
      AND [parent_object_id] = OBJECT_ID(N'[dbo].[AspNetUsers]')
)
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD CONSTRAINT [FK_AspNetUsers_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]);
END;
GO

IF OBJECT_ID(N'[dbo].[Stores]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Stores] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Stores_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Code] NVARCHAR(50) NOT NULL,
        [Address] NVARCHAR(500) NULL,
        [Phone] NVARCHAR(50) NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Stores_Status] DEFAULT (N'Active'),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Stores_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Stores_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_Stores] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Stores_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_Stores_Status]
            CHECK ([Status] IN (N'Active', N'Inactive', N'Closed'))
    );
END;
GO

IF OBJECT_ID(N'[dbo].[UserStores]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserStores] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_UserStores_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [StoreId] UNIQUEIDENTIFIER NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_UserStores_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_UserStores_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        CONSTRAINT [PK_UserStores] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserStores_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [FK_UserStores_Stores_StoreId]
            FOREIGN KEY ([StoreId]) REFERENCES [dbo].[Stores] ([Id]),
        CONSTRAINT [FK_UserStores_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
    );
END;
GO

/* ============================================================
   Product catalog
   ============================================================ */

IF OBJECT_ID(N'[dbo].[Categories]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Categories] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Categories_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Categories_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Categories_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Categories_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Products]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Products] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Products_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [CategoryId] UNIQUEIDENTIFIER NULL,
        [Name] NVARCHAR(250) NOT NULL,
        [Sku] NVARCHAR(64) NULL,
        [Barcode] NVARCHAR(128) NULL,
        [Description] NVARCHAR(2000) NULL,
        [Price] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Products_Price] DEFAULT (0),
        [CostPrice] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Products_CostPrice] DEFAULT (0),
        [ImageUrl] NVARCHAR(500) NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Products_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Products_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Products_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Categories_CategoryId]
            FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories] ([Id]),
        CONSTRAINT [FK_Products_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_Products_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Products_CostPrice] CHECK ([CostPrice] >= 0)
    );
END;
GO

IF OBJECT_ID(N'[dbo].[StoreProducts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StoreProducts] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_StoreProducts_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [StoreId] UNIQUEIDENTIFIER NOT NULL,
        [ProductId] UNIQUEIDENTIFIER NOT NULL,
        [SellingPrice] DECIMAL(18,2) NULL,
        [IsAvailable] BIT NOT NULL CONSTRAINT [DF_StoreProducts_IsAvailable] DEFAULT (1),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_StoreProducts_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        CONSTRAINT [PK_StoreProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StoreProducts_Products_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_StoreProducts_Stores_StoreId]
            FOREIGN KEY ([StoreId]) REFERENCES [dbo].[Stores] ([Id]),
        CONSTRAINT [FK_StoreProducts_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_StoreProducts_SellingPrice]
            CHECK ([SellingPrice] IS NULL OR [SellingPrice] >= 0)
    );
END;
GO

/* ============================================================
   Inventory
   ============================================================ */

IF OBJECT_ID(N'[dbo].[Inventories]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Inventories] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Inventories_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [StoreId] UNIQUEIDENTIFIER NOT NULL,
        [ProductId] UNIQUEIDENTIFIER NOT NULL,
        [Quantity] DECIMAL(18,3) NOT NULL CONSTRAINT [DF_Inventories_Quantity] DEFAULT (0),
        [MinQuantity] DECIMAL(18,3) NOT NULL CONSTRAINT [DF_Inventories_MinQuantity] DEFAULT (0),
        [UpdatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Inventories_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedBy] NVARCHAR(450) NULL,
        CONSTRAINT [PK_Inventories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Inventories_Products_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_Inventories_Stores_StoreId]
            FOREIGN KEY ([StoreId]) REFERENCES [dbo].[Stores] ([Id]),
        CONSTRAINT [FK_Inventories_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_Inventories_Quantity] CHECK ([Quantity] >= 0),
        CONSTRAINT [CK_Inventories_MinQuantity] CHECK ([MinQuantity] >= 0)
    );
END;
GO

IF OBJECT_ID(N'[dbo].[InventoryTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InventoryTransactions] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_InventoryTransactions_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [StoreId] UNIQUEIDENTIFIER NOT NULL,
        [ProductId] UNIQUEIDENTIFIER NOT NULL,
        [Type] NVARCHAR(30) NOT NULL,
        [Quantity] DECIMAL(18,3) NOT NULL,
        [BeforeQuantity] DECIMAL(18,3) NOT NULL,
        [AfterQuantity] DECIMAL(18,3) NOT NULL,
        [Reason] NVARCHAR(500) NULL,
        [ReferenceType] NVARCHAR(50) NULL,
        [ReferenceId] NVARCHAR(100) NULL,
        [CreatedBy] NVARCHAR(450) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_InventoryTransactions_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryTransactions_AspNetUsers_CreatedBy]
            FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [FK_InventoryTransactions_Products_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_InventoryTransactions_Stores_StoreId]
            FOREIGN KEY ([StoreId]) REFERENCES [dbo].[Stores] ([Id]),
        CONSTRAINT [FK_InventoryTransactions_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_InventoryTransactions_Type]
            CHECK ([Type] IN (N'Import', N'Export', N'Sale', N'Adjust', N'Return', N'TransferIn', N'TransferOut')),
        CONSTRAINT [CK_InventoryTransactions_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_InventoryTransactions_BeforeQuantity] CHECK ([BeforeQuantity] >= 0),
        CONSTRAINT [CK_InventoryTransactions_AfterQuantity] CHECK ([AfterQuantity] >= 0)
    );
END;
GO

/* ============================================================
   Shifts and POS
   ============================================================ */

IF OBJECT_ID(N'[dbo].[Shifts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Shifts] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Shifts_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [StoreId] UNIQUEIDENTIFIER NOT NULL,
        [OpenedBy] NVARCHAR(450) NOT NULL,
        [OpenedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Shifts_OpenedAt] DEFAULT (SYSUTCDATETIME()),
        [ClosedBy] NVARCHAR(450) NULL,
        [ClosedAt] DATETIME2(7) NULL,
        [OpeningCash] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Shifts_OpeningCash] DEFAULT (0),
        [ClosingCash] DECIMAL(18,2) NULL,
        [ExpectedCash] DECIMAL(18,2) NULL,
        [DifferenceAmount] DECIMAL(18,2) NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Shifts_Status] DEFAULT (N'Open'),
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shifts_AspNetUsers_ClosedBy]
            FOREIGN KEY ([ClosedBy]) REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [FK_Shifts_AspNetUsers_OpenedBy]
            FOREIGN KEY ([OpenedBy]) REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [FK_Shifts_Stores_StoreId]
            FOREIGN KEY ([StoreId]) REFERENCES [dbo].[Stores] ([Id]),
        CONSTRAINT [FK_Shifts_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_Shifts_Status] CHECK ([Status] IN (N'Open', N'Closed')),
        CONSTRAINT [CK_Shifts_OpeningCash] CHECK ([OpeningCash] >= 0),
        CONSTRAINT [CK_Shifts_ClosingCash] CHECK ([ClosingCash] IS NULL OR [ClosingCash] >= 0),
        CONSTRAINT [CK_Shifts_ExpectedCash] CHECK ([ExpectedCash] IS NULL OR [ExpectedCash] >= 0),
        CONSTRAINT [CK_Shifts_ClosedAt] CHECK ([ClosedAt] IS NULL OR [ClosedAt] >= [OpenedAt])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Orders]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Orders] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Orders_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [StoreId] UNIQUEIDENTIFIER NOT NULL,
        [OrderCode] NVARCHAR(50) NOT NULL,
        [StaffUserId] NVARCHAR(450) NULL,
        [ShiftId] UNIQUEIDENTIFIER NULL,
        [SubTotal] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Orders_SubTotal] DEFAULT (0),
        [DiscountAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Orders_DiscountAmount] DEFAULT (0),
        [TaxAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Orders_TaxAmount] DEFAULT (0),
        [TotalAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Orders_TotalAmount] DEFAULT (0),
        [PaymentStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Orders_PaymentStatus] DEFAULT (N'Unpaid'),
        [OrderStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Orders_OrderStatus] DEFAULT (N'New'),
        [Note] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Orders_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        [CancelledAt] DATETIME2(7) NULL,
        [CancelledBy] NVARCHAR(450) NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_AspNetUsers_CancelledBy]
            FOREIGN KEY ([CancelledBy]) REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [FK_Orders_AspNetUsers_CreatedBy]
            FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [FK_Orders_AspNetUsers_StaffUserId]
            FOREIGN KEY ([StaffUserId]) REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [FK_Orders_Shifts_ShiftId]
            FOREIGN KEY ([ShiftId]) REFERENCES [dbo].[Shifts] ([Id]),
        CONSTRAINT [FK_Orders_Stores_StoreId]
            FOREIGN KEY ([StoreId]) REFERENCES [dbo].[Stores] ([Id]),
        CONSTRAINT [FK_Orders_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_Orders_PaymentStatus]
            CHECK ([PaymentStatus] IN (N'Unpaid', N'Partial', N'Paid', N'Refunded', N'Cancelled')),
        CONSTRAINT [CK_Orders_OrderStatus]
            CHECK ([OrderStatus] IN (N'New', N'Completed', N'Cancelled')),
        CONSTRAINT [CK_Orders_Amounts]
            CHECK ([SubTotal] >= 0 AND [DiscountAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0),
        CONSTRAINT [CK_Orders_CancelledAt] CHECK ([CancelledAt] IS NULL OR [CancelledAt] >= [CreatedAt])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[OrderItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderItems] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_OrderItems_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [OrderId] UNIQUEIDENTIFIER NOT NULL,
        [ProductId] UNIQUEIDENTIFIER NOT NULL,
        [ProductName] NVARCHAR(250) NOT NULL,
        [Sku] NVARCHAR(64) NULL,
        [Quantity] DECIMAL(18,3) NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [DiscountAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_OrderItems_DiscountAmount] DEFAULT (0),
        [LineTotal] DECIMAL(18,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId]
            FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]),
        CONSTRAINT [FK_OrderItems_Products_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_OrderItems_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_OrderItems_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_OrderItems_Amounts]
            CHECK ([UnitPrice] >= 0 AND [DiscountAmount] >= 0 AND [LineTotal] >= 0)
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Payments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Payments] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Payments_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [OrderId] UNIQUEIDENTIFIER NOT NULL,
        [Method] NVARCHAR(30) NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [TransactionCode] NVARCHAR(100) NULL,
        [PaidAt] DATETIME2(7) NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Payments_Status] DEFAULT (N'Pending'),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Payments_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Orders_OrderId]
            FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]),
        CONSTRAINT [FK_Payments_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_Payments_Method]
            CHECK ([Method] IN (N'Cash', N'BankTransfer', N'Card', N'Momo', N'ZaloPay', N'Other')),
        CONSTRAINT [CK_Payments_Status]
            CHECK ([Status] IN (N'Pending', N'Paid', N'Failed', N'Refunded', N'Cancelled')),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0)
    );
END;
GO

/* ============================================================
   SaaS subscriptions
   ============================================================ */

IF OBJECT_ID(N'[dbo].[SubscriptionPlans]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SubscriptionPlans] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_SubscriptionPlans_Id] DEFAULT (NEWSEQUENTIALID()),
        [Name] NVARCHAR(200) NOT NULL,
        [Price] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_SubscriptionPlans_Price] DEFAULT (0),
        [BillingCycle] NVARCHAR(30) NOT NULL,
        [MaxStores] INT NULL,
        [MaxStaff] INT NULL,
        [MaxProducts] INT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_SubscriptionPlans_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_SubscriptionPlans_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_SubscriptionPlans_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SubscriptionPlans_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_SubscriptionPlans_BillingCycle]
            CHECK ([BillingCycle] IN (N'Monthly', N'Quarterly', N'Yearly')),
        CONSTRAINT [CK_SubscriptionPlans_Limits]
            CHECK (
                ([MaxStores] IS NULL OR [MaxStores] >= 0)
                AND ([MaxStaff] IS NULL OR [MaxStaff] >= 0)
                AND ([MaxProducts] IS NULL OR [MaxProducts] >= 0)
            )
    );
END;
GO

IF OBJECT_ID(N'[dbo].[TenantSubscriptions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TenantSubscriptions] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_TenantSubscriptions_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [PlanId] UNIQUEIDENTIFIER NOT NULL,
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_TenantSubscriptions_Status] DEFAULT (N'Active'),
        [AutoRenew] BIT NOT NULL CONSTRAINT [DF_TenantSubscriptions_AutoRenew] DEFAULT (1),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_TenantSubscriptions_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] NVARCHAR(450) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [UpdatedBy] NVARCHAR(450) NULL,
        CONSTRAINT [PK_TenantSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantSubscriptions_SubscriptionPlans_PlanId]
            FOREIGN KEY ([PlanId]) REFERENCES [dbo].[SubscriptionPlans] ([Id]),
        CONSTRAINT [FK_TenantSubscriptions_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_TenantSubscriptions_Status]
            CHECK ([Status] IN (N'Active', N'Trial', N'Expired', N'Cancelled', N'Suspended')),
        CONSTRAINT [CK_TenantSubscriptions_DateRange]
            CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[SystemPayments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SystemPayments] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_SystemPayments_Id] DEFAULT (NEWSEQUENTIALID()),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [SubscriptionId] UNIQUEIDENTIFIER NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [Method] NVARCHAR(30) NOT NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_SystemPayments_Status] DEFAULT (N'Pending'),
        [TransactionCode] NVARCHAR(100) NULL,
        [ProviderTransactionId] NVARCHAR(100) NULL,
        [BankCode] NVARCHAR(50) NULL,
        [BankAccountNo] NVARCHAR(50) NULL,
        [BankAccountName] NVARCHAR(255) NULL,
        [QrContent] NVARCHAR(1000) NULL,
        [TransferContent] NVARCHAR(255) NULL,
        [PaidAt] DATETIME2(7) NULL,
        [PaidAmount] DECIMAL(18,2) NULL,
        [RawResponse] NVARCHAR(MAX) NULL,
        [ExpiredAt] DATETIME2(7) NULL,
        [InvoiceUrl] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_SystemPayments_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2(7) NULL,
        CONSTRAINT [PK_SystemPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SystemPayments_TenantSubscriptions_SubscriptionId]
            FOREIGN KEY ([SubscriptionId]) REFERENCES [dbo].[TenantSubscriptions] ([Id]),
        CONSTRAINT [FK_SystemPayments_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
        CONSTRAINT [CK_SystemPayments_Method]
            CHECK ([Method] IN (N'Cash', N'BankTransfer', N'SePay', N'Card', N'Momo', N'ZaloPay', N'Other')),
        CONSTRAINT [CK_SystemPayments_Status]
            CHECK ([Status] IN (N'Pending', N'Paid', N'Failed', N'Refunded', N'Cancelled')),
        CONSTRAINT [CK_SystemPayments_Amount] CHECK ([Amount] > 0)
    );
END;
GO

IF OBJECT_ID(N'[dbo].[SystemPayments]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.SystemPayments', N'TransactionCode') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [TransactionCode] NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'ProviderTransactionId') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [ProviderTransactionId] NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'BankCode') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [BankCode] NVARCHAR(50) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'BankAccountNo') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [BankAccountNo] NVARCHAR(50) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'BankAccountName') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [BankAccountName] NVARCHAR(255) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'QrContent') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [QrContent] NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'TransferContent') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [TransferContent] NVARCHAR(255) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'PaidAmount') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [PaidAmount] DECIMAL(18,2) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'RawResponse') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [RawResponse] NVARCHAR(MAX) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'ExpiredAt') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [ExpiredAt] DATETIME2(7) NULL;
    IF COL_LENGTH(N'dbo.SystemPayments', N'UpdatedAt') IS NULL
        ALTER TABLE [dbo].[SystemPayments] ADD [UpdatedAt] DATETIME2(7) NULL;

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_SystemPayments_Method' AND [parent_object_id] = OBJECT_ID(N'[dbo].[SystemPayments]'))
        ALTER TABLE [dbo].[SystemPayments] DROP CONSTRAINT [CK_SystemPayments_Method];

    ALTER TABLE [dbo].[SystemPayments] WITH CHECK ADD CONSTRAINT [CK_SystemPayments_Method]
        CHECK ([Method] IN (N'Cash', N'BankTransfer', N'SePay', N'Card', N'Momo', N'ZaloPay', N'Other'));
END;
GO

IF OBJECT_ID(N'[dbo].[SystemPaymentWebhooks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SystemPaymentWebhooks] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_SystemPaymentWebhooks_Id] DEFAULT (NEWSEQUENTIALID()),
        [SystemPaymentId] UNIQUEIDENTIFIER NULL,
        [Gateway] NVARCHAR(30) NOT NULL CONSTRAINT [DF_SystemPaymentWebhooks_Gateway] DEFAULT (N'sepay'),
        [EventType] NVARCHAR(50) NULL,
        [ReferenceCode] NVARCHAR(100) NULL,
        [ContentTransfer] NVARCHAR(1000) NULL,
        [Amount] DECIMAL(18,2) NULL,
        [RawPayload] NVARCHAR(MAX) NOT NULL,
        [IsProcessed] BIT NOT NULL CONSTRAINT [DF_SystemPaymentWebhooks_IsProcessed] DEFAULT (0),
        [ProcessedAt] DATETIME2(7) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_SystemPaymentWebhooks_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_SystemPaymentWebhooks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SystemPaymentWebhooks_SystemPayments_SystemPaymentId]
            FOREIGN KEY ([SystemPaymentId]) REFERENCES [dbo].[SystemPayments] ([Id])
    );
END;
GO

/* ============================================================
   Audit log
   ============================================================ */

IF OBJECT_ID(N'[dbo].[AuditLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [TenantId] UNIQUEIDENTIFIER NULL,
        [StoreId] UNIQUEIDENTIFIER NULL,
        [UserId] NVARCHAR(450) NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [EntityName] NVARCHAR(128) NULL,
        [EntityId] NVARCHAR(100) NULL,
        [OldValue] NVARCHAR(MAX) NULL,
        [NewValue] NVARCHAR(MAX) NULL,
        [IpAddress] NVARCHAR(64) NULL,
        [UserAgent] NVARCHAR(512) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_AuditLogs_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [FK_AuditLogs_Stores_StoreId]
            FOREIGN KEY ([StoreId]) REFERENCES [dbo].[Stores] ([Id]),
        CONSTRAINT [FK_AuditLogs_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
    );
END;
GO

/* ============================================================
   Indexes
   ============================================================ */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'RoleNameIndex' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetRoles]'))
    CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'EmailIndex' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetUsers]'))
    CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UserNameIndex' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetUsers]'))
    CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AspNetRoleClaims_RoleId' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetRoleClaims]'))
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AspNetUserClaims_UserId' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetUserClaims]'))
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AspNetUserLogins_UserId' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetUserLogins]'))
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AspNetUserRoles_RoleId' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetUserRoles]'))
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AspNetUsers_TenantId' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetUsers]'))
    CREATE INDEX [IX_AspNetUsers_TenantId] ON [dbo].[AspNetUsers] ([TenantId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_AspNetUsers_PhoneNumber' AND [object_id] = OBJECT_ID(N'[dbo].[AspNetUsers]'))
    CREATE UNIQUE INDEX [UX_AspNetUsers_PhoneNumber] ON [dbo].[AspNetUsers] ([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Tenants_OwnerUserId' AND [object_id] = OBJECT_ID(N'[dbo].[Tenants]'))
    CREATE INDEX [IX_Tenants_OwnerUserId] ON [dbo].[Tenants] ([OwnerUserId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Stores_TenantId' AND [object_id] = OBJECT_ID(N'[dbo].[Stores]'))
    CREATE INDEX [IX_Stores_TenantId] ON [dbo].[Stores] ([TenantId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_Stores_TenantId_Code' AND [object_id] = OBJECT_ID(N'[dbo].[Stores]'))
    CREATE UNIQUE INDEX [UX_Stores_TenantId_Code] ON [dbo].[Stores] ([TenantId], [Code]) WHERE [IsDeleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_UserStores_TenantId_UserId_StoreId' AND [object_id] = OBJECT_ID(N'[dbo].[UserStores]'))
    CREATE UNIQUE INDEX [UX_UserStores_TenantId_UserId_StoreId] ON [dbo].[UserStores] ([TenantId], [UserId], [StoreId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_UserStores_StoreId' AND [object_id] = OBJECT_ID(N'[dbo].[UserStores]'))
    CREATE INDEX [IX_UserStores_StoreId] ON [dbo].[UserStores] ([StoreId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Categories_TenantId' AND [object_id] = OBJECT_ID(N'[dbo].[Categories]'))
    CREATE INDEX [IX_Categories_TenantId] ON [dbo].[Categories] ([TenantId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_Categories_TenantId_Name' AND [object_id] = OBJECT_ID(N'[dbo].[Categories]'))
    CREATE UNIQUE INDEX [UX_Categories_TenantId_Name] ON [dbo].[Categories] ([TenantId], [Name]) WHERE [IsDeleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Products_TenantId_Sku' AND [object_id] = OBJECT_ID(N'[dbo].[Products]'))
    CREATE INDEX [IX_Products_TenantId_Sku] ON [dbo].[Products] ([TenantId], [Sku]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_Products_TenantId_Sku' AND [object_id] = OBJECT_ID(N'[dbo].[Products]'))
    CREATE UNIQUE INDEX [UX_Products_TenantId_Sku] ON [dbo].[Products] ([TenantId], [Sku]) WHERE [Sku] IS NOT NULL AND [IsDeleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Products_TenantId_Barcode' AND [object_id] = OBJECT_ID(N'[dbo].[Products]'))
    CREATE INDEX [IX_Products_TenantId_Barcode] ON [dbo].[Products] ([TenantId], [Barcode]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_Products_TenantId_Barcode' AND [object_id] = OBJECT_ID(N'[dbo].[Products]'))
    CREATE UNIQUE INDEX [UX_Products_TenantId_Barcode] ON [dbo].[Products] ([TenantId], [Barcode]) WHERE [Barcode] IS NOT NULL AND [IsDeleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Products_CategoryId' AND [object_id] = OBJECT_ID(N'[dbo].[Products]'))
    CREATE INDEX [IX_Products_CategoryId] ON [dbo].[Products] ([CategoryId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_StoreProducts_TenantId_StoreId_ProductId' AND [object_id] = OBJECT_ID(N'[dbo].[StoreProducts]'))
    CREATE UNIQUE INDEX [UX_StoreProducts_TenantId_StoreId_ProductId] ON [dbo].[StoreProducts] ([TenantId], [StoreId], [ProductId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_Inventories_TenantId_StoreId_ProductId' AND [object_id] = OBJECT_ID(N'[dbo].[Inventories]'))
    CREATE UNIQUE INDEX [UX_Inventories_TenantId_StoreId_ProductId] ON [dbo].[Inventories] ([TenantId], [StoreId], [ProductId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_InventoryTransactions_TenantId_StoreId_ProductId_CreatedAt' AND [object_id] = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
    CREATE INDEX [IX_InventoryTransactions_TenantId_StoreId_ProductId_CreatedAt]
        ON [dbo].[InventoryTransactions] ([TenantId], [StoreId], [ProductId], [CreatedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Shifts_TenantId_StoreId_OpenedAt' AND [object_id] = OBJECT_ID(N'[dbo].[Shifts]'))
    CREATE INDEX [IX_Shifts_TenantId_StoreId_OpenedAt] ON [dbo].[Shifts] ([TenantId], [StoreId], [OpenedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Orders_TenantId_StoreId_CreatedAt' AND [object_id] = OBJECT_ID(N'[dbo].[Orders]'))
    CREATE INDEX [IX_Orders_TenantId_StoreId_CreatedAt] ON [dbo].[Orders] ([TenantId], [StoreId], [CreatedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_Orders_TenantId_OrderCode' AND [object_id] = OBJECT_ID(N'[dbo].[Orders]'))
    CREATE UNIQUE INDEX [UX_Orders_TenantId_OrderCode] ON [dbo].[Orders] ([TenantId], [OrderCode]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OrderItems_OrderId' AND [object_id] = OBJECT_ID(N'[dbo].[OrderItems]'))
    CREATE INDEX [IX_OrderItems_OrderId] ON [dbo].[OrderItems] ([OrderId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OrderItems_ProductId' AND [object_id] = OBJECT_ID(N'[dbo].[OrderItems]'))
    CREATE INDEX [IX_OrderItems_ProductId] ON [dbo].[OrderItems] ([ProductId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Payments_OrderId' AND [object_id] = OBJECT_ID(N'[dbo].[Payments]'))
    CREATE INDEX [IX_Payments_OrderId] ON [dbo].[Payments] ([OrderId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_TenantSubscriptions_TenantId_Status' AND [object_id] = OBJECT_ID(N'[dbo].[TenantSubscriptions]'))
    CREATE INDEX [IX_TenantSubscriptions_TenantId_Status] ON [dbo].[TenantSubscriptions] ([TenantId], [Status]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPayments_TenantId_PaidAt' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPayments]'))
    CREATE INDEX [IX_SystemPayments_TenantId_PaidAt] ON [dbo].[SystemPayments] ([TenantId], [PaidAt]);
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPayments_TransactionCode' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPayments]'))
    CREATE UNIQUE INDEX [IX_SystemPayments_TransactionCode] ON [dbo].[SystemPayments] ([TransactionCode]) WHERE [TransactionCode] IS NOT NULL;
GO

IF OBJECT_ID(N'[dbo].[SystemPaymentWebhooks]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPaymentWebhooks_SystemPaymentId' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPaymentWebhooks]'))
        CREATE INDEX [IX_SystemPaymentWebhooks_SystemPaymentId] ON [dbo].[SystemPaymentWebhooks] ([SystemPaymentId]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPaymentWebhooks_IsProcessed' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPaymentWebhooks]'))
        CREATE INDEX [IX_SystemPaymentWebhooks_IsProcessed] ON [dbo].[SystemPaymentWebhooks] ([IsProcessed]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPaymentWebhooks_ReferenceCode' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPaymentWebhooks]'))
        CREATE INDEX [IX_SystemPaymentWebhooks_ReferenceCode] ON [dbo].[SystemPaymentWebhooks] ([ReferenceCode]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_TenantId_UserId_CreatedAt' AND [object_id] = OBJECT_ID(N'[dbo].[AuditLogs]'))
    CREATE INDEX [IX_AuditLogs_TenantId_UserId_CreatedAt] ON [dbo].[AuditLogs] ([TenantId], [UserId], [CreatedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_TenantId_StoreId_CreatedAt' AND [object_id] = OBJECT_ID(N'[dbo].[AuditLogs]'))
    CREATE INDEX [IX_AuditLogs_TenantId_StoreId_CreatedAt] ON [dbo].[AuditLogs] ([TenantId], [StoreId], [CreatedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_TenantId_Action_CreatedAt' AND [object_id] = OBJECT_ID(N'[dbo].[AuditLogs]'))
    CREATE INDEX [IX_AuditLogs_TenantId_Action_CreatedAt] ON [dbo].[AuditLogs] ([TenantId], [Action], [CreatedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_TenantId_CreatedAt' AND [object_id] = OBJECT_ID(N'[dbo].[AuditLogs]'))
    CREATE INDEX [IX_AuditLogs_TenantId_CreatedAt] ON [dbo].[AuditLogs] ([TenantId], [CreatedAt]);
GO

/* ============================================================
   Seed core roles
   ============================================================ */

IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'ADMIN')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (N'ADMIN', N'ADMIN', N'ADMIN', NULL);
END;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'OWNER')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (N'OWNER', N'OWNER', N'OWNER', NULL);
END;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'STAFF')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (N'STAFF', N'STAFF', N'STAFF', NULL);
END;
GO

/* ============================================================
   Reporting views
   ============================================================ */

CREATE OR ALTER VIEW [dbo].[vw_DailySalesReport]
AS
SELECT
    o.[TenantId],
    o.[StoreId],
    CAST(o.[CreatedAt] AS DATE) AS [ReportDate],
    COUNT_BIG(*) AS [OrderCount],
    SUM(o.[SubTotal]) AS [SubTotal],
    SUM(o.[DiscountAmount]) AS [DiscountAmount],
    SUM(o.[TaxAmount]) AS [TaxAmount],
    SUM(o.[TotalAmount]) AS [TotalAmount]
FROM [dbo].[Orders] o
WHERE o.[OrderStatus] <> N'Cancelled'
GROUP BY
    o.[TenantId],
    o.[StoreId],
    CAST(o.[CreatedAt] AS DATE);
GO

CREATE OR ALTER VIEW [dbo].[vw_StaffSalesReport]
AS
SELECT
    o.[TenantId],
    o.[StoreId],
    o.[StaffUserId],
    CAST(o.[CreatedAt] AS DATE) AS [ReportDate],
    COUNT_BIG(*) AS [OrderCount],
    SUM(o.[TotalAmount]) AS [TotalSales]
FROM [dbo].[Orders] o
WHERE o.[OrderStatus] <> N'Cancelled'
GROUP BY
    o.[TenantId],
    o.[StoreId],
    o.[StaffUserId],
    CAST(o.[CreatedAt] AS DATE);
GO

CREATE OR ALTER VIEW [dbo].[vw_InventoryStatusReport]
AS
SELECT
    i.[TenantId],
    i.[StoreId],
    i.[ProductId],
    p.[Name] AS [ProductName],
    p.[Sku],
    p.[Barcode],
    i.[Quantity],
    i.[MinQuantity],
    CASE
        WHEN i.[Quantity] <= i.[MinQuantity] THEN CAST(1 AS BIT)
        ELSE CAST(0 AS BIT)
    END AS [IsLowStock],
    i.[UpdatedAt]
FROM [dbo].[Inventories] i
INNER JOIN [dbo].[Products] p ON p.[Id] = i.[ProductId]
WHERE p.[IsDeleted] = 0;
GO

CREATE OR ALTER VIEW [dbo].[vw_SystemRevenueReport]
AS
SELECT
    sp.[TenantId],
    CAST(sp.[PaidAt] AS DATE) AS [PaidDate],
    COUNT_BIG(*) AS [PaymentCount],
    SUM(sp.[Amount]) AS [TotalAmount]
FROM [dbo].[SystemPayments] sp
WHERE sp.[Status] = N'Paid'
GROUP BY
    sp.[TenantId],
    CAST(sp.[PaidAt] AS DATE);
GO
