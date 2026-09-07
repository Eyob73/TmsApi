namespace TmsApi.Domain.Entities
{
    public class Course
    {
        public int Id { get; set; }
        public required string CourseCode { get; set; }
        public required string CourseName { get; set; }
        public string? Description { get; set; }
        public required int Credits { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? ProgramId { get; set; }
        public string? Level { get; set; }
        public string? Semester { get; set; }
        public required string CourseType { get; set; }
        public Guid? PrerequisiteCourseId { get; set; }
        public int? DurationHours { get; set; }
        public required string Status { get; set; }
        public required bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public required bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        
        // Navigation properties
        public Department? Department { get; set; }
        public Program? Program { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        
        // Legacy fields for backward compatibility
        [Obsolete("Use CourseCode instead")]
        public string? Code { get; set; }
        [Obsolete("Use CourseName instead")]
        public string? Title { get; set; }
        [Obsolete("Use maxCapacity from enrollment logic if needed")]
        public int? MaxCapacity { get; set; }
        public string? InstructorId { get; set; }
    }
}
