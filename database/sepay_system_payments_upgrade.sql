IF OBJECT_ID(N'[dbo].[SystemPayments]', N'U') IS NULL
BEGIN
    THROW 50001, 'SystemPayments table does not exist.', 1;
END;
GO

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
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_SystemPayments_Method' AND [parent_object_id] = OBJECT_ID(N'[dbo].[SystemPayments]'))
    ALTER TABLE [dbo].[SystemPayments] DROP CONSTRAINT [CK_SystemPayments_Method];
GO

ALTER TABLE [dbo].[SystemPayments] WITH CHECK ADD CONSTRAINT [CK_SystemPayments_Method]
    CHECK ([Method] IN (N'Cash', N'BankTransfer', N'SePay', N'Card', N'Momo', N'ZaloPay', N'Other'));
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPayments_TransactionCode' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPayments]'))
    CREATE UNIQUE INDEX [IX_SystemPayments_TransactionCode] ON [dbo].[SystemPayments] ([TransactionCode]) WHERE [TransactionCode] IS NOT NULL;
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPaymentWebhooks_SystemPaymentId' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPaymentWebhooks]'))
    CREATE INDEX [IX_SystemPaymentWebhooks_SystemPaymentId] ON [dbo].[SystemPaymentWebhooks] ([SystemPaymentId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPaymentWebhooks_IsProcessed' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPaymentWebhooks]'))
    CREATE INDEX [IX_SystemPaymentWebhooks_IsProcessed] ON [dbo].[SystemPaymentWebhooks] ([IsProcessed]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemPaymentWebhooks_ReferenceCode' AND [object_id] = OBJECT_ID(N'[dbo].[SystemPaymentWebhooks]'))
    CREATE INDEX [IX_SystemPaymentWebhooks_ReferenceCode] ON [dbo].[SystemPaymentWebhooks] ([ReferenceCode]);
GO
