CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "Categories" (
    "Name" TEXT NOT NULL CONSTRAINT "PK_Categories" PRIMARY KEY,
    CONSTRAINT "CK_Category_Name" CHECK (length(Name) BETWEEN 1 AND 32 AND Name GLOB '[a-z]*' AND Name NOT GLOB '*[^a-z0-9_-]*')
);

CREATE TABLE "ImportReceipts" (
    "BatchId" TEXT NOT NULL CONSTRAINT "PK_ImportReceipts" PRIMARY KEY,
    "Fingerprint" TEXT NOT NULL,
    "Added" INTEGER NOT NULL,
    "Duplicates" INTEGER NOT NULL
);

CREATE TABLE "Expenses" (
    "Sequence" INTEGER NOT NULL CONSTRAINT "PK_Expenses" PRIMARY KEY AUTOINCREMENT,
    "ExternalId" TEXT NOT NULL,
    "Day" INTEGER NOT NULL,
    "CategoryName" TEXT NOT NULL,
    "AmountMinor" INTEGER NOT NULL,
    "Version" INTEGER NOT NULL,
    CONSTRAINT "CK_Expense_Amount" CHECK (typeof(AmountMinor) = 'integer' AND AmountMinor BETWEEN 1 AND 99999999),
    CONSTRAINT "CK_Expense_Day" CHECK (typeof(Day) = 'integer' AND Day BETWEEN 0 AND 3652058),
    CONSTRAINT "CK_Expense_Id" CHECK (length(ExternalId) = 36),
    CONSTRAINT "CK_Expense_Version" CHECK (typeof(Version) = 'integer' AND Version >= 0),
    CONSTRAINT "FK_Expenses_Categories_CategoryName" FOREIGN KEY ("CategoryName") REFERENCES "Categories" ("Name") ON DELETE RESTRICT
);

CREATE INDEX "IX_Expenses_CategoryName" ON "Expenses" ("CategoryName");

CREATE INDEX "IX_Expenses_Day_Sequence" ON "Expenses" ("Day", "Sequence");

CREATE UNIQUE INDEX "IX_Expenses_ExternalId" ON "Expenses" ("ExternalId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261022174905_InitialCreate', '10.0.13');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Expenses" ADD "Note" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261022175014_AddExpenseNote', '10.0.13');

COMMIT;
