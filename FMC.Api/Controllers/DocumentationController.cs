using FMC.Shared.Auth;
using FMC.Shared.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FMC.Api.Controllers;

[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/[controller]")]
public class DocumentationController : ControllerBase
{
    private readonly string _docsPath;

    public DocumentationController(IConfiguration config)
    {
        // Use an absolute path from the start for consistency
        var rawPath = config["DocumentationPath"] ?? "../Documentation";
        _docsPath = Path.GetFullPath(rawPath);
    }

    [HttpGet("list")]
    public IActionResult ListFiles()
    {
        if (!Directory.Exists(_docsPath))
            return NotFound("Documentation directory not found on server.");

        var files = Directory.GetFiles(_docsPath, "*.md")
            .Select(f => {
                var name = Path.GetFileName(f);
                return new DocumentationDto
                {
                    FileName = name,
                    DisplayName = name.Replace("_", " ").Replace(".md", "").ToUpperFirst()
                };
            })
            .ToList();

        return Ok(files);
    }

    [HttpGet("{*fileName}")] // Catch-all to handle dots in filenames
    public async Task<IActionResult> GetFile(string fileName)
    {
        // Clean the filename
        var cleanName = Path.GetFileName(fileName); 
        var fullPath = Path.GetFullPath(Path.Combine(_docsPath, cleanName));
        
        // Final security check: ensure the resulting path is still inside the docs folder
        if (!System.IO.File.Exists(fullPath) || !fullPath.StartsWith(_docsPath, System.StringComparison.OrdinalIgnoreCase))
            return NotFound($"File '{cleanName}' not found or secured.");

        var content = await System.IO.File.ReadAllTextAsync(fullPath);
        return Ok(new DocumentationDto
        {
            FileName = cleanName,
            Content = content
        });
    }
}

public static class StringExtensions
{
    public static string ToUpperFirst(this string input)
    {
        return string.IsNullOrEmpty(input) ? input : char.ToUpper(input[0]) + input.Substring(1);
    }
}
