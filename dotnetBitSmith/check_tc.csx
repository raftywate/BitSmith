using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseSqlite("Data Source=c:\\Users\\abhig\\OneDrive\\Desktop\\Personal\\.NET\\BitSmithApp\\dotnetBitSmith\\app.db");

using var context = new ApplicationDbContext(optionsBuilder.Options);

var problem = context.Problems.FirstOrDefault(p => p.ProblemNumber == 226);
if (problem != null) {
    var testCases = context.TestCases.Where(t => t.ProblemId == problem.Id).ToList();
    foreach(var tc in testCases) {
        Console.WriteLine($"ID: {tc.Id}, Expected: {tc.ExpectedOutput}");
    }
} else {
    Console.WriteLine("Problem not found");
}
