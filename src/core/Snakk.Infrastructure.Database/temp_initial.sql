IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE TABLE [Hub] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] nvarchar(450) NOT NULL,
        [Slug] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastModifiedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Hub] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE TABLE [User] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] nvarchar(max) NOT NULL,
        [DisplayName] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NULL,
        [OAuthProviderId] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastModifiedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [LastSeenAt] datetime2 NULL,
        [AnonymousUserId] nvarchar(max) NULL,
        CONSTRAINT [PK_User] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE TABLE [Space] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] nvarchar(450) NOT NULL,
        [Slug] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastModifiedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [HubId] int NOT NULL,
        CONSTRAINT [PK_Space] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Space_Hub_HubId] FOREIGN KEY ([HubId]) REFERENCES [Hub] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE TABLE [Discussion] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] nvarchar(450) NOT NULL,
        [Slug] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastModifiedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [LastActivityAt] datetime2 NULL,
        [IsPinned] bit NOT NULL,
        [IsLocked] bit NOT NULL,
        [SpaceId] int NOT NULL,
        [CreatedByUserId] int NOT NULL,
        CONSTRAINT [PK_Discussion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Discussion_Space_SpaceId] FOREIGN KEY ([SpaceId]) REFERENCES [Space] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Discussion_User_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE TABLE [Post] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastModifiedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [EditedAt] datetime2 NULL,
        [IsFirstPost] bit NOT NULL,
        [RevisionCount] int NOT NULL,
        [DiscussionId] int NOT NULL,
        [CreatedByUserId] int NOT NULL,
        [ReplyToPostId] int NULL,
        CONSTRAINT [PK_Post] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Post_Discussion_DiscussionId] FOREIGN KEY ([DiscussionId]) REFERENCES [Discussion] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Post_Post_ReplyToPostId] FOREIGN KEY ([ReplyToPostId]) REFERENCES [Post] ([Id]),
        CONSTRAINT [FK_Post_User_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE TABLE [PostRevision] (
        [Id] int NOT NULL IDENTITY,
        [PostId] int NOT NULL,
        [PostPublicId] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [EditedByUserId] int NOT NULL,
        [EditedByUserPublicId] nvarchar(max) NOT NULL,
        [RevisionNumber] int NOT NULL,
        CONSTRAINT [PK_PostRevision] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PostRevision_Post_PostId] FOREIGN KEY ([PostId]) REFERENCES [Post] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PostRevision_User_EditedByUserId] FOREIGN KEY ([EditedByUserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Discussion_CreatedByUserId] ON [Discussion] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Discussion_PublicId] ON [Discussion] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Discussion_Slug] ON [Discussion] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Discussion_SpaceId] ON [Discussion] ([SpaceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Hub_PublicId] ON [Hub] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Hub_Slug] ON [Hub] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Post_CreatedByUserId] ON [Post] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Post_DiscussionId] ON [Post] ([DiscussionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Post_ReplyToPostId] ON [Post] ([ReplyToPostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PostRevision_EditedByUserId] ON [PostRevision] ([EditedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PostRevision_PostId] ON [PostRevision] ([PostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Space_HubId] ON [Space] ([HubId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Space_PublicId] ON [Space] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Space_Slug] ON [Space] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126072538_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260126072538_InitialCreate', N'10.0.1');
END;

COMMIT;
GO

