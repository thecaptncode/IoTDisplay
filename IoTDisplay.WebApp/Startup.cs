#region Copyright

// --------------------------------------------------------------------------
// Copyright 2020 Greg Cannon
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// --------------------------------------------------------------------------

#endregion Copyright

#region Using

using IoTDisplay.Common.Helpers;
using IoTDisplay.Common.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.IO;

#endregion Using

namespace IoTDisplay.WebApp
{
    public class Startup
    {
        #region Fields

        private readonly OpenApiInfo openApiInfo = new()
        {
            Title = "IoT Display API",
            Version = "v1",
            Description = "Internet of Things E-Paper Display Controller",
            Contact = new()
            {
                Url = new("https://github.com/thecaptncode")
            }
        };

        #endregion Fields

        #region Constructor

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        #endregion Constructor

        #region Properties

        public IConfiguration Configuration { get; }

        #endregion Properties

        #region Methods (Public)

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                .AddNewtonsoftJson();

            // Swagger generator
            services.AddSwaggerGen(o =>
            {
                o.SwaggerDoc(openApiInfo.Version, openApiInfo);
                DirectoryInfo dir = new(AppContext.BaseDirectory);
                foreach (FileInfo fi in dir.EnumerateFiles("*.xml"))
                {
                    o.IncludeXmlComments(fi.FullName, includeControllerXmlComments: true);
                }
            });

            // ioTDisplayService (driver mode is obtained from appsettings.json)
            string driver = Configuration["Driver"];
            if (!int.TryParse(Configuration["Rotation"], out int rotation) ||
                (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270))
                throw new ArgumentException("Rotation must be 0, 90, 180 or 270", "Rotation");
            if (!TimeSpan.TryParse(Configuration["RefreshTime"], out TimeSpan refreshtime))
                throw new ArgumentException("Unable to parse RefreshTime", "RefreshTime");
            if (refreshtime.Days != 0)
                throw new ArgumentException("RefreshTime must not have a day portion", "RefreshTime");
            string statefolder = Configuration["StateFolder"];
            if (string.IsNullOrWhiteSpace(statefolder))
                statefolder = Path.GetTempPath();
            if (!statefolder.EndsWith(Path.DirectorySeparatorChar))
                statefolder += Path.DirectorySeparatorChar;
            if (!Directory.Exists(statefolder))
                throw new ArgumentException("StateFolder does not point to an existing folder", "StateFolder");
            string background = Configuration["BackgroundColor"];
            string foreground = Configuration["ForegroundColor"];
            IIoTDisplayService ioTDisplayService = IoTDisplayServiceHelper.GetService(driver, rotation, statefolder, background, foreground, refreshtime);
            services.AddSingleton(ioTDisplayService);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // Swagger
            app.UseSwagger();
            app.UseSwaggerUI(o =>
            {
                o.SwaggerEndpoint($"/swagger/{openApiInfo.Version}/swagger.json",
                    openApiInfo.Title);
            });
        }

        #endregion Methods (Public)
    }
}
