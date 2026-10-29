CREATE OR ALTER PROCEDURE dbo.GetProductCodes
(
    @IntegrationCompany VARCHAR(100),
    @ID_MAGAZYNU INT = 6,
    @ID_MAGAZYNUGLOWNEGO INT = 1
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Same product filter as in DeleteNotSyncedProducts
    SELECT DISTINCT Indeks_katalogowy
    FROM dbo.Artykul WITH (NOLOCK)
    WHERE Pole1 LIKE @IntegrationCompany + '%'
      AND ID_MAGAZYNU IN (@ID_MAGAZYNU, @ID_MAGAZYNUGLOWNEGO);
END
