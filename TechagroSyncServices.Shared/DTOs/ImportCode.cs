namespace TechagroSyncServices.Shared.DTOs
{
    // Single code from the import file, which can be written with or without supplier affix
    public class ImportCode
    {
        // Code without affix, used in supplier API requests
        public string SupplierCode { get; set; }

        // Code with affix, saved in database (Indeks_katalogowy)
        public string DatabaseCode { get; set; }
    }
}
