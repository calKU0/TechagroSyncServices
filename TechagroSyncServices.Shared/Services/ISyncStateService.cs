namespace TechagroApiSync.Shared.Services
{
    public interface ISyncStateService
    {
        int GetLastProductsCount();

        void SetLastProductsCount(int count);
    }
}
