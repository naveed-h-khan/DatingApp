using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using API.Data;
using API.DTOs;
using API.entities;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace API.Controllers
{
    public class AccountController : BaseAPIController
    {
        private readonly DataContext _context;
        private readonly ITokenService _token;

        public AccountController(DataContext context, ITokenService token)
        {
            _token = token;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDTO>> Register(RegisterDTO registerDTO)
        {
            if(await UserExists(registerDTO.Username)) return BadRequest("user name is taken");

            using var hmac = new HMACSHA512();

            var user = new AppUser
            {
                UserName=registerDTO.Username,
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDTO.Password)),
                PasswordSalt = hmac.Key
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new UserDTO
            {
                Username=user.UserName,
                Token=_token.CreateToken(user),
                PhotoUrl = user.Photos.FirstOrDefault(x=>x.IsMain)?.Url
            };
        }

        private Task<bool> UserExists(string userName)
        {
            return _context.Users.AnyAsync(x=>x.UserName==userName);
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDTO>> Login(LoginDTO loginDTO)
        {
            var user = await _context.Users
                .Include(p=>p.Photos)
                .SingleOrDefaultAsync(x=>x.UserName==loginDTO.Username);

            if (user == null)
            {
                return Unauthorized("Invalid user!");
            }

            using var hmac = new HMACSHA512(user.PasswordSalt);
            
            var ComputedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDTO.Password));

            for (int i = 0; i < ComputedHash.Length; i++)
            {   
                if (ComputedHash[i] != user.PasswordHash[i]) return Unauthorized("Invalid password!");
            }

            return new UserDTO
            {
                Username=user.UserName,
                Token=_token.CreateToken(user),
                PhotoUrl = user.Photos.FirstOrDefault(x=>x.IsMain)?.Url
            };
        }
    }
}