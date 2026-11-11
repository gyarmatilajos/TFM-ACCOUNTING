CREATE TABLE [CostCategories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_CostCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_CostCategories_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO


CREATE TABLE [InvoiceTypes] (
    [Id] int NOT NULL IDENTITY,
    [Code] varchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    [RequiresOriginalInvoice] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_InvoiceTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_InvoiceTypes_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
    CONSTRAINT [CK_InvoiceTypes_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO


CREATE TABLE [ObligationTypes] (
    [Id] int NOT NULL IDENTITY,
    [Code] varchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    [RequiresPeriod] bit NOT NULL,
    [HasInvoiceDetails] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ObligationTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ObligationTypes_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
    CONSTRAINT [CK_ObligationTypes_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO


CREATE TABLE [OwnCompanies] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [ShortName] nvarchar(50) NULL,
    [TaxNumber] nvarchar(20) NOT NULL,
    [PostalCode] nvarchar(10) NULL,
    [City] nvarchar(100) NULL,
    [AddressLine] nvarchar(200) NULL,
    [Notes] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_OwnCompanies] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OwnCompanies_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
    CONSTRAINT [CK_OwnCompanies_TaxNumber] CHECK (LEN(LTRIM(RTRIM([TaxNumber]))) > 0)
);
GO


CREATE TABLE [PartnerTypes] (
    [Id] int NOT NULL IDENTITY,
    [Code] varchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PartnerTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PartnerTypes_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
    CONSTRAINT [CK_PartnerTypes_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO


CREATE TABLE [PaymentMethods] (
    [Id] int NOT NULL IDENTITY,
    [Code] varchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PaymentMethods] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PaymentMethods_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
    CONSTRAINT [CK_PaymentMethods_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO


CREATE TABLE [SettlementTypes] (
    [Id] int NOT NULL IDENTITY,
    [Code] varchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    [IsCreditOffset] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_SettlementTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_SettlementTypes_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
    CONSTRAINT [CK_SettlementTypes_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO


CREATE TABLE [Partners] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [ShortName] nvarchar(50) NULL,
    [PartnerTypeId] int NOT NULL,
    [TaxNumber] nvarchar(20) NOT NULL,
    [PostalCode] nvarchar(10) NULL,
    [City] nvarchar(100) NULL,
    [AddressLine] nvarchar(200) NULL,
    [Notes] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Partners] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Partners_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
    CONSTRAINT [CK_Partners_TaxNumber] CHECK (LEN(LTRIM(RTRIM([TaxNumber]))) > 0),
    CONSTRAINT [FK_Partners_PartnerTypes_PartnerTypeId] FOREIGN KEY ([PartnerTypeId]) REFERENCES [PartnerTypes] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Obligations] (
    [Id] int NOT NULL IDENTITY,
    [OwnCompanyId] int NOT NULL,
    [PartnerId] int NOT NULL,
    [ObligationTypeId] int NOT NULL,
    [CategoryId] int NULL,
    [Description] nvarchar(200) NOT NULL,
    [PeriodYear] int NULL,
    [PeriodMonth] int NULL,
    [DueDate] date NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaymentMethodId] int NULL,
    [OriginalObligationId] int NULL,
    [Notes] nvarchar(2000) NULL,
    [IsArchived] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Obligations] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_Obligations_Id_OwnCompanyId_PartnerId] UNIQUE ([Id], [OwnCompanyId], [PartnerId]),
    CONSTRAINT [CK_Obligations_Amount] CHECK ([Amount] <> 0),
    CONSTRAINT [CK_Obligations_Description] CHECK (LEN(LTRIM(RTRIM([Description]))) > 0),
    CONSTRAINT [CK_Obligations_Original] CHECK ([OriginalObligationId] IS NULL OR [OriginalObligationId] <> [Id]),
    CONSTRAINT [CK_Obligations_Period] CHECK (([PeriodYear] IS NULL AND [PeriodMonth] IS NULL) OR ([PeriodYear] IS NOT NULL AND [PeriodMonth] IS NOT NULL AND [PeriodYear] BETWEEN 1 AND 9999 AND [PeriodMonth] BETWEEN 1 AND 12)),
    CONSTRAINT [FK_Obligations_CostCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [CostCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Obligations_ObligationTypes_ObligationTypeId] FOREIGN KEY ([ObligationTypeId]) REFERENCES [ObligationTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Obligations_Obligations_OriginalObligationId] FOREIGN KEY ([OriginalObligationId]) REFERENCES [Obligations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Obligations_OwnCompanies_OwnCompanyId] FOREIGN KEY ([OwnCompanyId]) REFERENCES [OwnCompanies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Obligations_Partners_PartnerId] FOREIGN KEY ([PartnerId]) REFERENCES [Partners] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Obligations_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Attachments] (
    [Id] int NOT NULL IDENTITY,
    [ObligationId] int NOT NULL,
    [OriginalFileName] nvarchar(255) NOT NULL,
    [RelativePath] varchar(1000) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [FileSizeBytes] bigint NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Attachments_ContentType] CHECK (LEN(LTRIM(RTRIM([ContentType]))) > 0),
    CONSTRAINT [CK_Attachments_Name] CHECK (LEN(LTRIM(RTRIM([OriginalFileName]))) > 0),
    CONSTRAINT [CK_Attachments_Path] CHECK (LEN(LTRIM(RTRIM([RelativePath]))) > 0),
    CONSTRAINT [CK_Attachments_Size] CHECK ([FileSizeBytes] > 0),
    CONSTRAINT [FK_Attachments_Obligations_ObligationId] FOREIGN KEY ([ObligationId]) REFERENCES [Obligations] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [InvoiceDetails] (
    [ObligationId] int NOT NULL,
    [OwnCompanyId] int NOT NULL,
    [PartnerId] int NOT NULL,
    [InvoiceNumber] nvarchar(100) NOT NULL,
    [InvoiceTypeId] int NOT NULL,
    [IssueDate] date NOT NULL,
    [PerformanceDate] date NOT NULL,
    [NetAmount] decimal(18,2) NOT NULL,
    [VatAmount] decimal(18,2) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_InvoiceDetails] PRIMARY KEY ([ObligationId]),
    CONSTRAINT [CK_InvoiceDetails_Number] CHECK (LEN(LTRIM(RTRIM([InvoiceNumber]))) > 0),
    CONSTRAINT [FK_InvoiceDetails_InvoiceTypes_InvoiceTypeId] FOREIGN KEY ([InvoiceTypeId]) REFERENCES [InvoiceTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InvoiceDetails_Obligations_ObligationId_OwnCompanyId_PartnerId] FOREIGN KEY ([ObligationId], [OwnCompanyId], [PartnerId]) REFERENCES [Obligations] ([Id], [OwnCompanyId], [PartnerId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [PaymentEntries] (
    [Id] int NOT NULL IDENTITY,
    [ObligationId] int NOT NULL,
    [SettlementTypeId] int NOT NULL,
    [SettlementDate] date NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaymentMethodId] int NULL,
    [Reference] nvarchar(200) NULL,
    [SettlementGroupId] uniqueidentifier NULL,
    [Notes] nvarchar(2000) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PaymentEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PaymentEntries_Amount] CHECK ([Amount] <> 0),
    CONSTRAINT [CK_PaymentEntries_Settlement] CHECK (([SettlementGroupId] IS NULL AND [PaymentMethodId] IS NOT NULL) OR ([SettlementGroupId] IS NOT NULL AND [PaymentMethodId] IS NULL)),
    CONSTRAINT [FK_PaymentEntries_Obligations_ObligationId] FOREIGN KEY ([ObligationId]) REFERENCES [Obligations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PaymentEntries_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PaymentEntries_SettlementTypes_SettlementTypeId] FOREIGN KEY ([SettlementTypeId]) REFERENCES [SettlementTypes] ([Id]) ON DELETE NO ACTION
);
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'IsActive', N'Name', N'Notes', N'RequiresOriginalInvoice', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[InvoiceTypes]'))
    SET IDENTITY_INSERT [InvoiceTypes] ON;
INSERT INTO [InvoiceTypes] ([Id], [Code], [CreatedAtUtc], [IsActive], [Name], [Notes], [RequiresOriginalInvoice], [UpdatedAtUtc])
VALUES (1, 'STANDARD', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Normál', NULL, CAST(0 AS bit), '2026-11-10T00:00:00.0000000Z'),
(2, 'AMENDMENT', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Módosító', NULL, CAST(1 AS bit), '2026-11-10T00:00:00.0000000Z'),
(3, 'CANCELLATION', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Érvénytelenítő', NULL, CAST(1 AS bit), '2026-11-10T00:00:00.0000000Z');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'IsActive', N'Name', N'Notes', N'RequiresOriginalInvoice', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[InvoiceTypes]'))
    SET IDENTITY_INSERT [InvoiceTypes] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'HasInvoiceDetails', N'IsActive', N'Name', N'Notes', N'RequiresPeriod', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[ObligationTypes]'))
    SET IDENTITY_INSERT [ObligationTypes] ON;
INSERT INTO [ObligationTypes] ([Id], [Code], [CreatedAtUtc], [HasInvoiceDetails], [IsActive], [Name], [Notes], [RequiresPeriod], [UpdatedAtUtc])
VALUES (1, 'INCOMING_INVOICE', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), CAST(1 AS bit), N'Bejövő számla', NULL, CAST(0 AS bit), '2026-11-10T00:00:00.0000000Z'),
(2, 'OTHER_EXPENSE', '2026-11-10T00:00:00.0000000Z', CAST(0 AS bit), CAST(1 AS bit), N'Egyéb költség', NULL, CAST(0 AS bit), '2026-11-10T00:00:00.0000000Z'),
(3, 'NET_SALARY', '2026-11-10T00:00:00.0000000Z', CAST(0 AS bit), CAST(1 AS bit), N'Nettó bér', NULL, CAST(1 AS bit), '2026-11-10T00:00:00.0000000Z'),
(4, 'TAX_OR_CONTRIBUTION', '2026-11-10T00:00:00.0000000Z', CAST(0 AS bit), CAST(1 AS bit), N'Járulék / adó', NULL, CAST(1 AS bit), '2026-11-10T00:00:00.0000000Z');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'HasInvoiceDetails', N'IsActive', N'Name', N'Notes', N'RequiresPeriod', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[ObligationTypes]'))
    SET IDENTITY_INSERT [ObligationTypes] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'IsActive', N'Name', N'Notes', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[PartnerTypes]'))
    SET IDENTITY_INSERT [PartnerTypes] ON;
INSERT INTO [PartnerTypes] ([Id], [Code], [CreatedAtUtc], [IsActive], [Name], [Notes], [UpdatedAtUtc])
VALUES (1, 'BUSINESS', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Vállalkozás', NULL, '2026-11-10T00:00:00.0000000Z'),
(2, 'INDIVIDUAL', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Magánszemély', NULL, '2026-11-10T00:00:00.0000000Z'),
(3, 'AUTHORITY', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Hatóság', NULL, '2026-11-10T00:00:00.0000000Z');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'IsActive', N'Name', N'Notes', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[PartnerTypes]'))
    SET IDENTITY_INSERT [PartnerTypes] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'IsActive', N'Name', N'Notes', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[PaymentMethods]'))
    SET IDENTITY_INSERT [PaymentMethods] ON;
INSERT INTO [PaymentMethods] ([Id], [Code], [CreatedAtUtc], [IsActive], [Name], [Notes], [UpdatedAtUtc])
VALUES (1, 'BANK_TRANSFER', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Átutalás', NULL, '2026-11-10T00:00:00.0000000Z'),
(2, 'CASH', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Készpénz', NULL, '2026-11-10T00:00:00.0000000Z'),
(3, 'BANK_CARD', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Bankkártya', NULL, '2026-11-10T00:00:00.0000000Z'),
(4, 'DIRECT_DEBIT', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Csoportos beszedés', NULL, '2026-11-10T00:00:00.0000000Z'),
(5, 'OTHER', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), N'Egyéb', NULL, '2026-11-10T00:00:00.0000000Z');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'IsActive', N'Name', N'Notes', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[PaymentMethods]'))
    SET IDENTITY_INSERT [PaymentMethods] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'IsActive', N'IsCreditOffset', N'Name', N'Notes', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[SettlementTypes]'))
    SET IDENTITY_INSERT [SettlementTypes] ON;
INSERT INTO [SettlementTypes] ([Id], [Code], [CreatedAtUtc], [IsActive], [IsCreditOffset], [Name], [Notes], [UpdatedAtUtc])
VALUES (1, 'MONEY_MOVEMENT', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), CAST(0 AS bit), N'Pénzmozgás', NULL, '2026-11-10T00:00:00.0000000Z'),
(2, 'CREDIT_OFFSET', '2026-11-10T00:00:00.0000000Z', CAST(1 AS bit), CAST(1 AS bit), N'Jóváírás beszámítása', NULL, '2026-11-10T00:00:00.0000000Z');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'IsActive', N'IsCreditOffset', N'Name', N'Notes', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[SettlementTypes]'))
    SET IDENTITY_INSERT [SettlementTypes] OFF;
GO


CREATE INDEX [IX_Attachments_ObligationId] ON [Attachments] ([ObligationId]);
GO


CREATE UNIQUE INDEX [IX_Attachments_RelativePath] ON [Attachments] ([RelativePath]);
GO


CREATE UNIQUE INDEX [IX_CostCategories_Name] ON [CostCategories] ([Name]);
GO


CREATE INDEX [IX_InvoiceDetails_InvoiceTypeId] ON [InvoiceDetails] ([InvoiceTypeId]);
GO


CREATE INDEX [IX_InvoiceDetails_IssueDate] ON [InvoiceDetails] ([IssueDate]);
GO


CREATE UNIQUE INDEX [IX_InvoiceDetails_ObligationId_OwnCompanyId_PartnerId] ON [InvoiceDetails] ([ObligationId], [OwnCompanyId], [PartnerId]);
GO


CREATE UNIQUE INDEX [IX_InvoiceDetails_OwnCompanyId_PartnerId_InvoiceNumber] ON [InvoiceDetails] ([OwnCompanyId], [PartnerId], [InvoiceNumber]);
GO


CREATE INDEX [IX_InvoiceDetails_PerformanceDate] ON [InvoiceDetails] ([PerformanceDate]);
GO


CREATE UNIQUE INDEX [IX_InvoiceTypes_Code] ON [InvoiceTypes] ([Code]);
GO


CREATE UNIQUE INDEX [IX_InvoiceTypes_Name] ON [InvoiceTypes] ([Name]);
GO


CREATE INDEX [IX_Obligations_CategoryId] ON [Obligations] ([CategoryId]);
GO


CREATE INDEX [IX_Obligations_ObligationTypeId] ON [Obligations] ([ObligationTypeId]);
GO


CREATE INDEX [IX_Obligations_OriginalObligationId] ON [Obligations] ([OriginalObligationId]);
GO


CREATE INDEX [IX_Obligations_OwnCompanyId_DueDate] ON [Obligations] ([OwnCompanyId], [DueDate]);
GO


CREATE INDEX [IX_Obligations_PartnerId] ON [Obligations] ([PartnerId]);
GO


CREATE INDEX [IX_Obligations_PaymentMethodId] ON [Obligations] ([PaymentMethodId]);
GO


CREATE INDEX [IX_Obligations_PeriodYear_PeriodMonth_ObligationTypeId] ON [Obligations] ([PeriodYear], [PeriodMonth], [ObligationTypeId]);
GO


CREATE UNIQUE INDEX [IX_ObligationTypes_Code] ON [ObligationTypes] ([Code]);
GO


CREATE UNIQUE INDEX [IX_ObligationTypes_Name] ON [ObligationTypes] ([Name]);
GO


CREATE INDEX [IX_OwnCompanies_Name] ON [OwnCompanies] ([Name]);
GO


CREATE UNIQUE INDEX [IX_OwnCompanies_TaxNumber] ON [OwnCompanies] ([TaxNumber]);
GO


CREATE INDEX [IX_Partners_Name] ON [Partners] ([Name]);
GO


CREATE INDEX [IX_Partners_PartnerTypeId] ON [Partners] ([PartnerTypeId]);
GO


CREATE UNIQUE INDEX [IX_Partners_TaxNumber] ON [Partners] ([TaxNumber]);
GO


CREATE UNIQUE INDEX [IX_PartnerTypes_Code] ON [PartnerTypes] ([Code]);
GO


CREATE UNIQUE INDEX [IX_PartnerTypes_Name] ON [PartnerTypes] ([Name]);
GO


CREATE INDEX [IX_PaymentEntries_ObligationId] ON [PaymentEntries] ([ObligationId]);
GO


CREATE INDEX [IX_PaymentEntries_PaymentMethodId] ON [PaymentEntries] ([PaymentMethodId]);
GO


CREATE INDEX [IX_PaymentEntries_SettlementDate] ON [PaymentEntries] ([SettlementDate]);
GO


CREATE UNIQUE INDEX [IX_PaymentEntries_SettlementGroupId_ObligationId] ON [PaymentEntries] ([SettlementGroupId], [ObligationId]) WHERE [SettlementGroupId] IS NOT NULL;
GO


CREATE INDEX [IX_PaymentEntries_SettlementTypeId] ON [PaymentEntries] ([SettlementTypeId]);
GO


CREATE UNIQUE INDEX [IX_PaymentMethods_Code] ON [PaymentMethods] ([Code]);
GO


CREATE UNIQUE INDEX [IX_PaymentMethods_Name] ON [PaymentMethods] ([Name]);
GO


CREATE UNIQUE INDEX [IX_SettlementTypes_Code] ON [SettlementTypes] ([Code]);
GO


CREATE UNIQUE INDEX [IX_SettlementTypes_Name] ON [SettlementTypes] ([Name]);
GO


