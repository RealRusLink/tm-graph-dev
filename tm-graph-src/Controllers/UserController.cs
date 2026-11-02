using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tm_graph_src;

namespace tm_graph_src.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetUser(User user)
        {
            return Ok(new PublicUserDto(user.Id, user.Username, user.Email));
        }
    }
}
