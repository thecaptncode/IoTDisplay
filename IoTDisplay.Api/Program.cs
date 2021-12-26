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

#region Using

#pragma warning disable SA1200 // Using directives should be placed correctly
using System.ComponentModel;
using IoTDisplay.Common.Helpers;
using IoTDisplay.Common.Models;
using Microsoft.OpenApi.Models;
using SkiaSharp;
#pragma warning restore SA1200 // Using directives should be placed correctly

#endregion

#region Configure Builder

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Host.UseSystemd();
builder.Host.UseWindowsService();
builder.Services.AddEndpointsApiExplorer();

OpenApiInfo openApiInfo = new ()
{
    Title = "IoT Display API",
    Version = "v1",
    Description = "Internet of Things E-Paper Display Controller",
    Contact = new ()
    {
        Url = new ("https://github.com/thecaptncode")
    }
};

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", openApiInfo);
    DirectoryInfo dir = new (AppContext.BaseDirectory);
    foreach (FileInfo fi in dir.EnumerateFiles("*.xml"))
    {
        c.IncludeXmlComments(fi.FullName, includeControllerXmlComments: true);
    }
});

TypeDescriptor.AddAttributes(typeof(SKColor), new TypeConverterAttribute(typeof(SKColorTypeConverter)));
AppSettings.Api settings = builder.Configuration.GetSection("Api").Get<AppSettings.Api>();
builder.Services.AddSingleton(
    DisplayServiceHelper.GetService(settings));
builder.Services.AddControllers();

#endregion Configure Builder

#region Configure Application

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
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

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint($"/swagger/{openApiInfo.Version}/swagger.json", openApiInfo.Title);
});

app.Run(settings.ListenerUrl);

#endregion Configure Application