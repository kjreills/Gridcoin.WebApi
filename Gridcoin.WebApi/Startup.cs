using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using Gridcoin.WebApi.Constants;
using Gridcoin.WebApi.Controllers;
using Gridcoin.WebApi.Models;
using Gridcoin.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Prometheus;

namespace Gridcoin.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gridcoin.WebApi", Version = "v1" });

                var oauth2 = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Scheme = "Bearer",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri("/oauth/token", UriKind.Relative)
                        }                        
                    }
                };

                c.AddSecurityDefinition("oauth2", oauth2);

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "oauth2"
                            },
                            Name = "oauth2"
                        },
                        new List<string>{ }
                    }
                });
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.Authority = Configuration.GetValue<string>("Authentication:Authority");
                options.Audience = Configuration.GetValue<string>("Authentication:Audience");
            });

            services.AddAuthorization(options =>
            {
                foreach (var permission in Permissions.All)
                {
                    options.AddPolicy(permission, policy =>
                                      policy.RequireClaim("permissions", permission));
                }
            });

            services.AddSingleton(x => Configuration.GetSection("Authentication").Get<OAuthSettings>());

            services.AddHttpClient(GridcoinController.HttpClientKey, x =>
            {
                var gridcoinSettings = Configuration.GetSection("Gridcoin").Get<GridcoinSettings>();
                x.BaseAddress = gridcoinSettings.Uri;
                var bytes = Encoding.ASCII.GetBytes($"{gridcoinSettings.Username}:{gridcoinSettings.Password}");
                x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
            });

            services.AddScoped<GridcoinService>();

            services.AddScoped(x => new GridcoinStatsService(x.GetService<ILogger<GridcoinStatsService>>(), TimeSpan.FromSeconds(90), x.GetService<GridcoinService>()));
            services.AddHostedService<BaseHostedService<GridcoinStatsService>>();

            services.AddHttpClient(OAuthController.HttpClientKey, x =>
            {
                x.BaseAddress = Configuration.GetValue<Uri>("Authentication:Authority");
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gridcoin.WebApi v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapMetrics();
            });
        }
    }
}
