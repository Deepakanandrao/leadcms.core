// <copyright file="IContactService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Interfaces
{
    public interface IContactService : IEntityService<Contact>
    {
        Task Subscribe(Contact contact, string groupName);

        Task Unsubscribe(string email, string reason, string source, DateTime createdAt, string? ip);

        Task<Contact> FindOrCreate(string email, string? language = null, int? timezone = null);

        Task<Contact> FindOrCreateByPhone(string phone, string? language = null, int? timezone = null);
    }
}