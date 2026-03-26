using FMC.Application.Interfaces;
using FMC.Shared.Auth;
using FMC.Shared.DTOs.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public UsersController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        return Ok(await _identityService.GetAllUsersAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var user = await _identityService.GetUserByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto request)
    {
        var result = await _identityService.CreateUserAsync(request);
        if (!result) return BadRequest("Failed to create user.");
        return Ok();
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateUserDto request)
    {
        var result = await _identityService.UpdateUserAsync(request);
        if (!result) return BadRequest("Failed to update user.");
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _identityService.DeleteUserAsync(id);
        if (!result) return BadRequest("Failed to delete user.");
        return Ok();
    }
}
