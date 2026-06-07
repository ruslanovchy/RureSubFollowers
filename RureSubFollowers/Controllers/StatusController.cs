using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RureSubFollowers.Model;
using RureSubFollowers.Models;
using RureSubFollowers.Services;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace RureSubFollowers.Controllers;

[ApiController]
[Route("/status")]
public class StatusController : Controller
{
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        return Ok("Hello world!");
    }
}
