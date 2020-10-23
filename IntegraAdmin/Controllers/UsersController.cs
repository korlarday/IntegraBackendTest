﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using IntegraAdmin.Core.Constants;
using IntegraAdmin.Core.Interfaces;
using IntegraAdmin.Core.Models;
using IntegraAdmin.Core.Resources;
using IntegraAdmin.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IntegraAdmin.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private UserManager<ApplicationUser> _userManager;
        private SignInManager<ApplicationUser> _signInManager;
        private readonly ISponsorRepository _sponsorRepo;
        private readonly IMapper _mapper;
        private readonly ApplicationSettings _appSettings;

        public UsersController(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager, 
            IOptions<ApplicationSettings> appSettings,
            ISponsorRepository sponsorRepository,
            IMapper mapper)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _sponsorRepo = sponsorRepository;
            _mapper = mapper;
            _appSettings = appSettings.Value;
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> PostApplicationUser(ApplicationUserModel model)
        {
            model.Roles.Add("Admin");
            var applicationUser = new ApplicationUser()
            {
                UserName = model.UserName,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                SponsorId = model.SponsorId
            };

            try
            {
                var result = await _userManager.CreateAsync(applicationUser, model.Password);
                await _userManager.AddToRolesAsync(applicationUser, model.Roles);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return BadRequest((new { message = ex }));
                //return BadRequest((new { message = "Sorry, something went wrong." }));
            }
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);

            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                //Get role assigned to the user
                var roles = await _userManager.GetRolesAsync(user);
                IdentityOptions _options = new IdentityOptions();

                // add roles to claim
                var customClaims = new List<Claim>();
                foreach (var role in roles)
                {
                    var newClaim = new Claim(_options.ClaimsIdentity.RoleClaimType, role);
                    customClaims.Add(newClaim);
                }

                var claimIdentity = new ClaimsIdentity(customClaims);
                claimIdentity.AddClaim(new Claim("UserId", user.Id.ToString()));
                    var tokenDescriptor = new SecurityTokenDescriptor
                    {
                        Subject = claimIdentity,
                        Expires = DateTime.UtcNow.AddDays(1),
                        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.JWT_Secret)), SecurityAlgorithms.HmacSha256Signature)
                    };
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var securityToken = tokenHandler.CreateToken(tokenDescriptor);
                    var token = tokenHandler.WriteToken(securityToken);
                    return Ok(new { token });
            }
            else
                return BadRequest(new { message = "Username or password is incorrect." });
        }


        
        [Authorize(Roles = "Admin")]
        [Route("create-sponsor-user")]
        [HttpPost]
        public async Task<IActionResult> CreateSponsorUser(ApplicationUserModel model)
        {
            model.Roles.Add("Sponsor Read");
            model.Roles.Add("Sponsor Write");
            model.Roles.Add("Sponsor");
            var applicationUser = new ApplicationUser()
            {
                UserName = model.UserName,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                SponsorId = model.SponsorId
            };

            try
            {
                var result = await _userManager.CreateAsync(applicationUser, model.Password);
                await _userManager.AddToRolesAsync(applicationUser, model.Roles);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return BadRequest((new { message = ex }));
                //return BadRequest((new { message = "Sorry, something went wrong." }));
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("all-sponsors")]
        public async Task<IActionResult> GetAllSponsorUsers()
        {
            var sponsorUsers = await _userManager.GetUsersInRoleAsync(RolesConst.Sponsor);
            var sponsors = await _sponsorRepo.AllSponsors();
            var result = _mapper.Map<IList<ApplicationUser>, List<ReadUserResource>>(sponsorUsers);
            var model = new SponsorsViewModel { SponsorUsers = result };
            return Ok(result);
        }
    }
}
