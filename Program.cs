var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseExceptionHandler("/error");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
})).RequireAuthorization();

app.Run();
