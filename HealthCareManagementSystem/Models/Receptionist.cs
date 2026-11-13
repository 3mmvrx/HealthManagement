namespace HealthCareManagementSystem.Models
{
    public class Receptionist
    {
        public int Id { get; set; }

        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
