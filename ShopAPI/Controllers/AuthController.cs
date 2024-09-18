using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ShopAPI.Data;
using ShopAPI.Models;
using ShopAPI.Models.Dto;
using ShopAPI.Utility;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ShopAPI.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDBContext _db;
        private readonly ApiResponse _apiresponse;
        private string secretKey;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AuthController(AppDBContext db, IConfiguration configuration,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            this._db = db;
            _apiresponse = new ApiResponse();
            secretKey = configuration.GetValue<string>("ApiSettings:Secret");
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO loginRequestDTO)
        {
            try
            {
                ApplicationUser userFromDB = _db.ApplicationUsers.FirstOrDefault(x => x.UserName == loginRequestDTO.UserName);
                bool isValid = await _userManager.CheckPasswordAsync(userFromDB, loginRequestDTO.Password);

                if (!isValid)
                {
                    _apiresponse.Result = new LoginRequestDTO();
                    _apiresponse.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    _apiresponse.IsSuccess = false;
                    _apiresponse.ErrorMessages.Add("Invalid Username or Password.");
                    return BadRequest(_apiresponse);
                }

                // Generate JWT token
                var roles = await _userManager.GetRolesAsync(userFromDB);
                JwtSecurityTokenHandler tokenHandler = new();
                byte[] key = Encoding.ASCII.GetBytes(secretKey);

                SecurityTokenDescriptor tokenDescriptor = new()
                {
                    Subject = new ClaimsIdentity(new Claim[]
                    {
                    new Claim("fullName", userFromDB.Name),
                    new Claim("id", userFromDB.Id.ToString()),
                    new Claim(ClaimTypes.Email, userFromDB.UserName.ToString()),
                    new Claim(ClaimTypes.Role, roles.FirstOrDefault()),
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);

                LoginResponseDTO loginResponseDTO = new LoginResponseDTO()
                {
                    Email = userFromDB.Email,
                    Token = tokenHandler.WriteToken(token)
                };

                if(loginResponseDTO.Email == null || string.IsNullOrEmpty(loginResponseDTO.Token)){
                    _apiresponse.Result = new LoginRequestDTO();
                    _apiresponse.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    _apiresponse.IsSuccess = false;
                    _apiresponse.ErrorMessages.Add("Invalid Username or Password.");
                    return BadRequest(_apiresponse);
                }

                _apiresponse.Result = loginResponseDTO;
                _apiresponse.StatusCode = System.Net.HttpStatusCode.OK;
                _apiresponse.IsSuccess = true;
                return Ok(_apiresponse);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDTO registerRequestDTO)
        {
            try
            {
                ApplicationUser userFromDB = _db.ApplicationUsers.FirstOrDefault(x => x.UserName == registerRequestDTO.UserName);
                if (userFromDB != null)
                {
                    _apiresponse.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    _apiresponse.IsSuccess = false;
                    _apiresponse.ErrorMessages.Add("User Already Exists!");
                    return BadRequest(_apiresponse);
                }

                ApplicationUser newUser = new()
                {
                    UserName = registerRequestDTO.UserName,
                    Email = registerRequestDTO.UserName,
                    NormalizedEmail = registerRequestDTO.UserName.ToUpper(),
                    Name = registerRequestDTO.Name
                };

                var result = await _userManager.CreateAsync(newUser, registerRequestDTO.Password);
                if (result.Succeeded)
                {
                    if (!_roleManager.RoleExistsAsync(SD.Role_Admin).GetAwaiter().GetResult())
                    {
                        // if admin role does not exist then create in db
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_Admin));
                    }
                    if (!_roleManager.RoleExistsAsync(SD.Role_Customer).GetAwaiter().GetResult())
                    {
                        // if admin role does not exist then create in db
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_Customer));
                    }
                    if (registerRequestDTO.Role.ToLower() == SD.Role_Admin)
                    {
                        await _userManager.AddToRoleAsync(newUser, SD.Role_Admin);
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(newUser, SD.Role_Customer);
                    }

                    _apiresponse.StatusCode = System.Net.HttpStatusCode.OK;
                    _apiresponse.IsSuccess = true;
                    return Ok(_apiresponse);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            _apiresponse.StatusCode = System.Net.HttpStatusCode.BadRequest;
            _apiresponse.IsSuccess = false;
            _apiresponse.ErrorMessages.Add("Error when registering user.");
            return BadRequest(_apiresponse);
        }
    }
}
