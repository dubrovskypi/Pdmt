using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Data;
using System.Text;

namespace Pdmt.Api.Tests
{
    public class CustomWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // --- Remove the real DbContext registrations ---
                RemoveService<DbContextOptions<SqlServerAppDbContext>>(services);
                RemoveService<DbContextOptions<PostgresAppDbContext>>(services);
                RemoveService<DbContextOptions<AppDbContext>>(services);
                RemoveService<SqlServerAppDbContext>(services);
                RemoveService<PostgresAppDbContext>(services);
                RemoveService<AppDbContext>(services);

                // Register in-memory database
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("IntegrationTestDb"));

                // --- Replace authentication ---
                // Add a policy scheme that forwards to either TestScheme or JwtBearer depending on the Authorization header.
                // This allows tests to exercise both the test auth handler (when using "TestScheme") and real JWT flow (when using "Bearer").
                var testJwtSecret = "your-super-secret-test-key-123456";
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
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testJwtSecret))
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
