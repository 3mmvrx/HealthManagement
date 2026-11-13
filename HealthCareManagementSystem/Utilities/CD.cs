namespace HealthCareManagementSystem.Utilities
{
    /// <summary>Application-wide constants (roles and areas).</summary>
    public class CD
    {
        // Roles
        public const string SUPER_ADMIN_ROLE = "SuperAdmin";
        public const string ADMIN_ROLE = "Admin";
        public const string EMPLOYEE_ROLE = "Employee";
        public const string PATIENT_ROLE = "Patient";
        public const string DOCTOR_ROLE = "Doctor";

        // Role groups, for use in [Authorize(Roles = ...)]
        public const string ADMIN_ROLES = SUPER_ADMIN_ROLE + "," + ADMIN_ROLE;
        public const string STAFF_ROLES = SUPER_ADMIN_ROLE + "," + ADMIN_ROLE + "," + EMPLOYEE_ROLE;

        // Areas
        public const string ADMIN_AREA = "Admin";
        public const string IDENTITY_AREA = "Identity";
        public const string PATIENT_AREA = "Patient";
        public const string DOCTOR_AREA = "Doctor";
    }
}
