CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026161004_InitialUserCreate') THEN
    CREATE TABLE "PasswordResetTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TokenHash" text NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "UsedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_PasswordResetTokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026161004_InitialUserCreate') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "FirstName" text NOT NULL,
        "LastName" text NOT NULL,
        "Email" character varying(256) NOT NULL,
        "NormalizedEmail" character varying(256) NOT NULL,
        "PasswordHash" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026161004_InitialUserCreate') THEN
    CREATE UNIQUE INDEX "IX_Users_NormalizedEmail" ON "Users" ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026161004_InitialUserCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261026161004_InitialUserCreate', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027181045_ConfigurePasswordResetToken') THEN
    ALTER TABLE "PasswordResetTokens" ALTER COLUMN "TokenHash" TYPE character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027181045_ConfigurePasswordResetToken') THEN
    CREATE UNIQUE INDEX "IX_PasswordResetTokens_TokenHash" ON "PasswordResetTokens" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027181045_ConfigurePasswordResetToken') THEN
    CREATE INDEX "IX_PasswordResetTokens_UserId" ON "PasswordResetTokens" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027181045_ConfigurePasswordResetToken') THEN
    ALTER TABLE "PasswordResetTokens" ADD CONSTRAINT "FK_PasswordResetTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027181045_ConfigurePasswordResetToken') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027181045_ConfigurePasswordResetToken', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028052325_AddAppUserEntity') THEN
    ALTER TABLE "Users" ALTER COLUMN "LastName" TYPE character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028052325_AddAppUserEntity') THEN
    ALTER TABLE "Users" ALTER COLUMN "FirstName" TYPE character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028052325_AddAppUserEntity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261028052325_AddAppUserEntity', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112052848_AddExternalLogin') THEN
    ALTER TABLE "Users" ALTER COLUMN "LastName" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112052848_AddExternalLogin') THEN
    ALTER TABLE "Users" ALTER COLUMN "FirstName" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112052848_AddExternalLogin') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112052848_AddExternalLogin', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261128120000_AddTwoFactorAuthColumns') THEN
    ALTER TABLE "Users" ADD "TwoFactorEnabled" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261128120000_AddTwoFactorAuthColumns') THEN
    ALTER TABLE "Users" ADD "TwoFactorSecret" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261128120000_AddTwoFactorAuthColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261128120000_AddTwoFactorAuthColumns', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261128130000_AddOtpTokenTable') THEN
    CREATE TABLE "OtpTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "OtpCode" text NOT NULL,
        "Attempts" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "VerifiedAtUtc" timestamp with time zone,
        "IsVerified" boolean NOT NULL,
        CONSTRAINT "PK_OtpTokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OtpTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261128130000_AddOtpTokenTable') THEN
    CREATE INDEX "IX_OtpTokens_UserId" ON "OtpTokens" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261128130000_AddOtpTokenTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261128130000_AddOtpTokenTable', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261201120000_AddExternalLoginTable') THEN
    CREATE TABLE "ExternalLogins" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Provider" character varying(50) NOT NULL,
        "ProviderSubject" character varying(256) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ExternalLogins" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExternalLogins_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261201120000_AddExternalLoginTable') THEN
    CREATE UNIQUE INDEX "IX_ExternalLogins_Provider_ProviderSubject" ON "ExternalLogins" ("Provider", "ProviderSubject");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261201120000_AddExternalLoginTable') THEN
    CREATE INDEX "IX_ExternalLogins_UserId" ON "ExternalLogins" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261201120000_AddExternalLoginTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261201120000_AddExternalLoginTable', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261202120000_MoveOtpChallengesToRedis') THEN
    DROP TABLE IF EXISTS "OtpTokens";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261202120000_MoveOtpChallengesToRedis') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261202120000_MoveOtpChallengesToRedis', '8.0.11');
    END IF;
END $EF$;
COMMIT;

