using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Repositories.Interfaces;

namespace AIStory.API.Authorization
{
    public sealed class AuthorMustResignPolicyHandler : AuthorizationHandler<AuthorMustResignPolicyRequirement>
    {
        private readonly IUserRepository _userRepository;

        public AuthorMustResignPolicyHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AuthorMustResignPolicyRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            if (context.User.IsInRole("ADMIN"))
            {
                context.Succeed(requirement);
                return;
            }

            if (!context.User.IsInRole("AUTHOR"))
            {
                context.Succeed(requirement);
                return;
            }

            var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)
                             ?? context.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return;
            }

            var user = await _userRepository.GetUserById(userId);
            if (user == null)
            {
                return;
            }

            if (user.must_resign_policy == true)
            {
                return;
            }

            context.Succeed(requirement);
        }
    }
}
