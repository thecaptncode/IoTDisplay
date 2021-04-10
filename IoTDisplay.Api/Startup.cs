#region Copyright
// --------------------------------------------------------------------------
// Copyright 2021 Greg Cannon
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

namespace IoTDisplay.Api
{
    #region Using

    using System;
    using System.ComponentModel;
    using System.IO;
    using IoTDisplay.Common.Helpers;
    using IoTDisplay.Common.Models;
    using IoTDisplay.Common.Services;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.OpenApi.Models;
    using SkiaSharp;

    #endregion Using

    public class Startup
    {
        #region Fields

        private readonly OpenApiInfo _openApiInfo = new ()
        {
            Title = "IoT Display API",
            Version = "v1",
            Description = "Internet of Things E-Paper Display Controller",
            Contact = new ()
            {
                Url = new ("https://github.com/thecaptncode")
            }
        };

        #endregion Fields

        #region Constructor

        public Startup(IConfiguration configuration)
        {
            TypeDescriptor.AddAttributes(typeof(SKColor), new TypeConverterAttribute(typeof(SKColorTypeConverter)));
            Configuration = configuration.GetSection("Api").Get<AppSettings.Api>();
        }

        #endregion Constructor

        #region Properties

        public AppSettings.Api Configuration { get; }

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
                o.SwaggerDoc(_openApiInfo.Version, _openApiInfo);
                DirectoryInfo dir = new (AppContext.BaseDirectory);
                foreach (FileInfo fi in dir.EnumerateFiles("*.xml"))
                {
                    o.IncludeXmlComments(fi.FullName, includeControllerXmlComments: true);
                }
            });

            IDisplayService ioTDisplayService = DisplayServiceHelper.GetService(Configuration);
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

            // app.UseHttpsRedirection();
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
                o.SwaggerEndpoint($"/swagger/{_openApiInfo.Version}/swagger.json",
                    _openApiInfo.Title);
            });
        }

        #endregion Methods (Public)
    }
}
