BEGIN TRANSACTION;
ALTER TABLE [User] ADD [EmailVerificationToken] nvarchar(max) NULL;

ALTER TABLE [User] ADD [EmailVerified] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [User] ADD [LastLoginAt] datetime2 NULL;

ALTER TABLE [User] ADD [OAuthProvider] nvarchar(max) NULL;

ALTER TABLE [User] ADD [PasswordHash] nvarchar(max) NULL;

ALTER TABLE [Space] ADD [AllowAnonymousReading] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [Space] ADD [RequireEmailConfirmation] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [Hub] ADD [AllowAnonymousReading] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [Hub] ADD [RequireEmailConfirmation] bit NOT NULL DEFAULT CAST(1 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260126075726_AddAuthenticationAndAccessControl', N'10.0.1');

COMMIT;
GO

