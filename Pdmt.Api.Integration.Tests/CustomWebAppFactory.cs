using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Data;
using System.Text;

namespace Pdmt.Api.Integration.Tests
{
    public class CustomWebAppFactory : WebApplicationFactory<Program>
    {
        private const string TestJwtSecret = "test-super-secret-key-min-32-chars!!";

        public CustomWebAppFactory()
        {
            Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
            Environment.SetEnvironmentVariable("Jwt__Issuer", "pdmt-test");
            Environment.SetEnvironmentVariable("Jwt__Audience", "pdmt-test");
            Environment.SetEnvironmentVariable("Jwt__TokenLifetimeMinutes", "60");
            Environment.SetEnvironmentVariable("Jwt__RefreshTokenLifetimeDays", "1");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // --- Remove the real DbContext registrations ---
                RemoveService<DbContextOptions<AppDbContext>>(services);
                RemoveService<AppDbContext>(services);

                // Register in-memory database
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("IntegrationTestDb"));

                // --- Replace authentication ---
                // Add a policy scheme that forwards to either TestScheme or JwtBearer depending on the Authorization header.
                // This allows tests to exercise both the test auth handler (when using "TestScheme") and real JWT flow (when using "Bearer").
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestOrJwt";
                    options.DefaultChallengeScheme = "TestOrJwt";
                })
                .AddPolicyScheme("TestOrJwt", "Test or Jwt", options =>
                {
                    options.ForwardDefaultSelector = context =>
                    {
                        var auth = context.Request.Headers["Authorization"].ToString();
                        if (!string.IsNullOrEmpty(auth) && auth.StartsWith(TestAuthHandler.SchemeName, StringComparison.OrdinalIgnoreCase))
                            return TestAuthHandler.SchemeName;
                        // Forward to the JwtBearer scheme that Program.cs already registered.
                        return JwtBearerDefaults.AuthenticationScheme;
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, options => { });

                // Configure the existing JwtBearer options (avoid re-registering the "Bearer" scheme).
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret))
                    }
                    ;
                });
            });
        }

        private static void RemoveService<T>(IServiceCollection services)
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
            if (descriptor != null)
                services.Remove(descriptor);
        }
    }
}
