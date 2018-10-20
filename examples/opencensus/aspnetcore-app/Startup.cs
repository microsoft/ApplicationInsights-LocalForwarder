using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace W3CService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<HttpClient>();
            services.AddApplicationInsightsTelemetry(o => o.RequestCollectionOptions.EnableW3CDistributedTracing = true);

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            var initializers = app.ApplicationServices.GetServices<ITelemetryInitializer>().ToList();
            Console.WriteLine(string.Join(",\r\n", initializers));

            var azwebapp = initializers.OfType<AzureWebAppRoleEnvironmentTelemetryInitializer>().Single();

            var updateEnvVarsFI = typeof(AzureWebAppRoleEnvironmentTelemetryInitializer).GetField("updateEnvVars",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var getNodeNameMI = typeof(AzureWebAppRoleEnvironmentTelemetryInitializer).GetMethod("GetNodeName",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var getRoleNameMi = typeof(AzureWebAppRoleEnvironmentTelemetryInitializer).GetMethod("GetRoleName",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Console.WriteLine($"updateEnvVarsFI {updateEnvVarsFI.GetValue(azwebapp)}");
            Console.WriteLine($"getNodeNameMI {getNodeNameMI.Invoke(azwebapp, new object[0])}");
            Console.WriteLine($"getRoleNameMi {getRoleNameMi.Invoke(azwebapp, new object[0])}");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
