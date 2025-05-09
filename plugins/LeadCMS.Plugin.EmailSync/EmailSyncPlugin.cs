// <copyright file="EmailSyncPlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.EmailSync.Tasks;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.EmailSync.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Plugin.EmailSync
{
    public class EmailSyncPlugin : IPlugin
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddAutoMapper(typeof(EmailSyncPlugin));

            services.AddScoped<ITask, EmailSyncTask>();
            services.AddScoped<PluginDbContextBase, EmailSyncDbContext>();
            services.AddScoped<EmailSyncDbContext, EmailSyncDbContext>();
        }
    }
}