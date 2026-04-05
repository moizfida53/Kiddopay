using Kiddopay.BLL.DTOs;

namespace Kiddopay.BLL.Interfaces
{
    public interface IPreOrderService
    {
        /// <summary>
        /// Retrieves the active pre-order for a student on today's date, if one exists.
        /// Returns null when no active pre-order is found.
        /// </summary>
        Task<PreOrderDTO> GetActivePreOrderForStudent(Guid studentId);

        /// <summary>
        /// Retrieves a pre-order by its GUID.
        /// </summary>
        Task<PreOrderDTO> GetPreOrderById(Guid preOrderId);
    }
}
