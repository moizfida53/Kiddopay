using Kiddopay.BLL.DTOs;

namespace Kiddopay.BLL.Interfaces
{
    public interface IStudentService
    {
        /// <summary>
        /// Resolves the student profile from an NFC bracelet UID.
        /// Returns null when the UID is not registered.
        /// </summary>
        StudentProfileDTO GetStudentByNfcUid(string nfcUid);

        /// <summary>
        /// Returns the student profile directly by student GUID.
        /// </summary>
        StudentProfileDTO GetStudentById(Guid studentId);
    }
}
