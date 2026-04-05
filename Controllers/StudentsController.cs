using Kiddopay.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Kiddopay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudentsController(IStudentService studentService) : ControllerBase
    {
        private readonly IStudentService _students = studentService;

        /// <summary>
        /// Called when the NFC reader picks up a student's bracelet.
        /// Returns the full student profile including pre-order flag and wallet balance.
        /// </summary>
        /// <param name="nfcUid">Raw NFC UID from the hardware reader</param>
        [HttpGet]
        public IActionResult ScanBracelet([FromQuery] string nfcUid)
        {
            try
            {
                var profile = _students.GetStudentByNfcUid(nfcUid);
                if (profile == null)
                    return NotFound(new { message = "Student not found for this bracelet." });

                return Ok(profile);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Returns a student profile by ID.
        /// Used after the initial NFC scan to refresh the profile mid-session.
        /// </summary>
        [HttpGet("{studentId:guid}")]
        public IActionResult GetStudent(Guid studentId)
        {
            try
            {
                var profile = _students.GetStudentById(studentId);
                if (profile == null)
                    return NotFound(new { message = "Student not found." });

                return Ok(profile);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
