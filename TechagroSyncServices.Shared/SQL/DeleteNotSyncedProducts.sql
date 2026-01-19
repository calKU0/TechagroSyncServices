CREATE PROCEDURE dbo.DeleteNotSyncedProducts
(
    @IntegrationCompany VARCHAR(100),
    @AllowedCodes dbo.StringList READONLY
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DeletedCount INT = 0;

    -- Local table for IDs to delete
    DECLARE @ToDelete TABLE (Indeks_katalogowy VARCHAR(100));

    -- Fill table with products to delete
    INSERT INTO @ToDelete (Indeks_katalogowy)
    SELECT Indeks_katalogowy
    FROM dbo.Artykul WITH (NOLOCK)
    WHERE Pole1 LIKE @IntegrationCompany + '%'
      AND Indeks_katalogowy NOT IN (SELECT Code FROM @AllowedCodes);

    DECLARE @Index VARCHAR(100);

    -- Start loop
    WHILE EXISTS (SELECT 1 FROM @ToDelete)
    BEGIN
        -- Get one value
        SELECT TOP 1 @Index = Indeks_katalogowy
        FROM @ToDelete;

        BEGIN TRY
            -- Start transaction for this product
            BEGIN TRANSACTION;

            -- 1. Delete images
            DELETE FROM dbo.ARTYKUL_BLOB
            WHERE Indeks_katalogowy = @Index;

            -- 2. Delete product
            DELETE FROM dbo.Artykul
            WHERE Indeks_katalogowy = @Index;

            -- Commit only if both deletes succeed
            COMMIT TRANSACTION;

            SET @DeletedCount += 1;
        END TRY
        BEGIN CATCH
            -- Rollback transaction if anything fails
            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;

            -- Ignore error, continue with next product
        END CATCH;

        -- Remove processed row
        DELETE FROM @ToDelete
        WHERE Indeks_katalogowy = @Index;
    END

    -- Return number of successfully deleted products
    SELECT @DeletedCount AS DeletedRows;
END
