CREATE OR ALTER PROCEDURE dbo.DeleteNotSyncedProducts
(
    @IntegrationCompany VARCHAR(100),
    @AllowedCodes dbo.StringList READONLY,
    @ID_MAGAZYNU INT = 6,
    @ID_MAGAZYNUGLOWNEGO INT = 1
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @DeletedCount INT = 0;
    DECLARE @BatchSize INT = 500;
    DECLARE @Size INT;
    DECLARE @Position INT = 1;
    DECLARE @Count INT;
    DECLARE @OneByOneUntil INT = 0;

    CREATE TABLE #Allowed (Code VARCHAR(100) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);

    INSERT INTO #Allowed (Code)
    SELECT DISTINCT Code
    FROM @AllowedCodes
    WHERE Code IS NOT NULL;

    -- Products of this supplier to delete, numbered so they can be deleted in batches.
    -- Only rows created by the service (POLE1 and sync warehouses) - articles of other suppliers
    -- or created by hand with the same code are never touched.
    CREATE TABLE #ToDelete
    (
        RowNo INT IDENTITY(1,1) PRIMARY KEY,
        Indeks_katalogowy VARCHAR(100) COLLATE DATABASE_DEFAULT NOT NULL
    );

    INSERT INTO #ToDelete (Indeks_katalogowy)
    SELECT DISTINCT a.Indeks_katalogowy
    FROM dbo.Artykul a WITH (NOLOCK)
    WHERE a.Pole1 LIKE @IntegrationCompany + '%'
      AND a.ID_MAGAZYNU IN (@ID_MAGAZYNU, @ID_MAGAZYNUGLOWNEGO)
      AND NOT EXISTS (SELECT 1 FROM #Allowed al WHERE al.Code = a.Indeks_katalogowy);

    SET @Count = @@ROWCOUNT;

    CREATE TABLE #Batch (Indeks_katalogowy VARCHAR(100) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
    CREATE TABLE #Ids (ID_ARTYKULU INT NOT NULL PRIMARY KEY);

    WHILE @Position <= @Count
    BEGIN
        -- Products of a failed batch are deleted one by one, so one product that cannot be deleted does not stop the rest
        SET @Size = CASE WHEN @Position <= @OneByOneUntil THEN 1 ELSE @BatchSize END;

        TRUNCATE TABLE #Batch;
        INSERT INTO #Batch (Indeks_katalogowy)
        SELECT Indeks_katalogowy
        FROM #ToDelete
        WHERE RowNo BETWEEN @Position AND @Position + @Size - 1;

        BEGIN TRY
            BEGIN TRANSACTION;

            DELETE FROM #Ids;
            INSERT INTO #Ids (ID_ARTYKULU)
            SELECT a.ID_ARTYKULU
            FROM dbo.Artykul a
            JOIN #Batch b ON b.Indeks_katalogowy = a.Indeks_katalogowy
            WHERE a.Pole1 LIKE @IntegrationCompany + '%'
              AND a.ID_MAGAZYNU IN (@ID_MAGAZYNU, @ID_MAGAZYNUGLOWNEGO);

            -- 1. Delete prices and barcodes of the products
            DELETE c
            FROM dbo.CENA_ARTYKULU c
            JOIN #Ids i ON i.ID_ARTYKULU = c.ID_ARTYKULU;

            DELETE k
            FROM dbo.KOD_KRESKOWY k
            JOIN #Ids i ON i.ID_ARTYKULU = k.ID_ARTYKULU;

            -- 2. Delete products
            DELETE a
            FROM dbo.Artykul a
            JOIN #Ids i ON i.ID_ARTYKULU = a.ID_ARTYKULU;

            -- 3. Delete images, unless another article with the same code still uses them
            DELETE bl
            FROM dbo.ARTYKUL_BLOB bl
            JOIN #Batch b ON b.Indeks_katalogowy = bl.Indeks_katalogowy
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Artykul a WHERE a.Indeks_katalogowy = bl.Indeks_katalogowy);

            -- Commit only if all deletes succeed
            COMMIT TRANSACTION;

            SET @DeletedCount += (SELECT COUNT(*) FROM #Batch);
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;

            IF @Size > 1
            BEGIN
                -- Try the same products again, one by one
                SET @OneByOneUntil = @Position + @Size - 1;
                CONTINUE;
            END

            -- Ignore error of single product, continue with next one
        END CATCH;

        SET @Position += @Size;
    END

    -- Return number of successfully deleted products
    SELECT @DeletedCount AS DeletedRows;
END
