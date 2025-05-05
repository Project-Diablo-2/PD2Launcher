
using PD2Shared.Models;

namespace PD2Shared.Interfaces
{
    public interface IFilterHelpers
    {
        Task<bool> CheckAndUpdateFilterAsync(SelectedAuthorAndFilter selectedAuthorAndFilter);
    }
}
