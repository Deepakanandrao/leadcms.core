// <copyright file="IContactService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Interfaces
{
    public interface IContactService : IEntityService<Contact>
    {
        Task Subscribe(Contact contact, string groupName);

        Task Unsubscribe(string email, string reason, string source, DateTime? createdAt = null);

        Task<Contact> FindOrCreateByIdentifiers(string? email = null, string? phone = null, string? ipAddress = null, string? userAgent = null);

        Task<Contact> FindOrCreate(string email, string? ipAddress = null, string? userAgent = null);

        Task<Contact> FindOrCreateByPhone(string phone, string? ipAddress = null, string? userAgent = null);

        Task<Contact> FindOrCreatePotential(string ipAddress, string userAgent);
    }
}