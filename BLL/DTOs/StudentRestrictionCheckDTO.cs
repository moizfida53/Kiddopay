namespace Kiddopay.BLL.DTOs
{
    // ─── Allergy / Forbidden checks ────────────────────────────────────────────

    public class StudentRestrictionCheckDTO
    {
        public bool HasAllergyConflict { get; set; }
        public List<string> ConflictingAllergyNames { get; set; } = new();
        public bool IsCategoryForbidden { get; set; }
        public string ForbiddenCategoryName { get; set; }
    }
}
