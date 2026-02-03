using Microsoft.AspNetCore.Identity.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
    }
}
