// <copyright file="ContactService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services
{
    public class ContactService : IContactService
    {
        private readonly IDomainService domainService;
        private readonly IEmailSchedulingService emailSchedulingService;
        private readonly IPhoneNormalizationService phoneNormalizationService;
        private readonly IConfiguration configuration;
        private PgDbContext pgDbContext;

        public ContactService(PgDbContext pgDbContext, IDomainService domainService, IEmailSchedulingService emailSchedulingService, IPhoneNormalizationService phoneNormalizationService, IConfiguration configuration)
        {
            this.pgDbContext = pgDbContext;
            this.domainService = domainService;
            this.emailSchedulingService = emailSchedulingService;
            this.phoneNormalizationService = phoneNormalizationService;
            this.configuration = configuration;
        }

        public async Task SaveAsync(Contact contact)
        {
            NormalizePhone(contact);
            await EnrichWithDomainId(contact);

            // Only enrich AccountId for new contacts (not updates)
            if (contact.Id == 0)
            {
                EnrichWithAccountId(contact);
            }

            if (contact.Id > 0)
            {
                pgDbContext.Contacts!.Update(contact);
            }
            else
            {
                await pgDbContext.Contacts!.AddAsync(contact);
            }
        }

        public async Task SaveRangeAsync(List<Contact> contacts)
        {
            foreach (var contact in contacts)
            {
                NormalizePhone(contact);
            }

            await EnrichWithDomainIdAsync(contacts);

            // Only enrich AccountId for new contacts (not updates)
            var newContacts = contacts.Where(c => c.Id == 0).ToList();
            if (newContacts.Count > 0)
            {
                EnrichWithAccountId(newContacts);
            }

            var sortedContacts = contacts.GroupBy(c => c.Id > 0);

            foreach (var group in sortedContacts)
            {
                if (group.Key)
                {
                    pgDbContext.UpdateRange(group.ToList());
                }
                else
                {
                    await pgDbContext.AddRangeAsync(group.ToList());
                }
            }
        }

        public async Task<Contact> FindOrCreate(string email, string? ipAddress = null, string? userAgent = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(email);

            return await FindOrCreateByIdentifiers(email, null, ipAddress, userAgent);
        }

        public async Task<Contact> FindOrCreateByIdentifiers(string? email = null, string? phone = null, string? ipAddress = null, string? userAgent = null)
        {
            email = NormalizeEmail(email);

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone) && (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent)))
            {
                throw new ArgumentException("At least one identifier must be provided.");
            }

            var contact = FindExistingContactByPriority(email, phone, ipAddress, userAgent);

            if (contact == null)
            {
                contact = new Contact();
            }

            if (!string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(contact.Email))
            {
                contact.Email = email;
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                contact.PhoneRaw = phone;

                var normalized = phoneNormalizationService.Normalize(phone);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    contact.Phone = normalized;
                }
            }

            await SaveAsync(contact);

            return contact;
        }

        public async Task Subscribe(Contact contact, string groupName)
        {
            var language = contact.Language ?? LanguageHelper.GetDefaultLanguage(configuration);

            var emailSchedule = await emailSchedulingService.FindByGroupAndLanguage(groupName, language);

            if (emailSchedule == null)
            {
                throw new EntityNotFoundException(typeof(EmailSchedule).Name, groupName);
            }

            await pgDbContext.ContactEmailSchedules!.AddAsync(new ContactEmailSchedule
            {
                Contact = contact,
                Schedule = emailSchedule,
            });
        }

        public async Task Unsubscribe(string email, string reason, string source, DateTime? createdAt = null)
        {
            email = NormalizeEmail(email) ?? email;

            var contact = (from u in pgDbContext.Contacts
                           where u.Email == email
                           select u).FirstOrDefault();

            if (contact != null)
            {
                var unsubscribe = new Unsubscribe
                {
                    ContactId = contact.Id,
                    Reason = reason,
                    Source = source,
                };

                if (createdAt.HasValue)
                {
                    unsubscribe.CreatedAt = createdAt.Value;
                }

                await pgDbContext.Unsubscribes!.AddAsync(unsubscribe);

                contact.Unsubscribe = unsubscribe;

                var schedules = pgDbContext.ContactEmailSchedules!
                    .Include(c => c.Schedule)
                    .Include(c => c.Contact)
                    .Where(s => s.Status == ScheduleStatus.Pending && s.ContactId == contact.Id)
                    .ToList();

                foreach (var schedule in schedules)
                {
                    schedule.Status = ScheduleStatus.Unsubscribed;
                }
            }
        }

        public void SetDBContext(PgDbContext pgDbContext)
        {
            this.pgDbContext = pgDbContext;
            domainService.SetDBContext(pgDbContext);
            emailSchedulingService.SetDBContext(pgDbContext);
        }

        public async Task<Contact> FindOrCreateByPhone(string phone, string? ipAddress = null, string? userAgent = null)
        {
            return await FindOrCreateByIdentifiers(null, phone, ipAddress, userAgent);
        }

        public async Task<Contact> FindOrCreatePotential(string ipAddress, string userAgent)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
            ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

            var contact = FindPotentialContact(ipAddress, userAgent);

            contact ??= new Contact();
            await SaveAsync(contact);

            return contact;
        }

        private static string? NormalizeEmail(string? email)
        {
            return string.IsNullOrWhiteSpace(email)
                ? null
                : email.Trim().ToLowerInvariant();
        }

        private Contact? FindPotentialContact(string? ipAddress, string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent))
            {
                return null;
            }

            return pgDbContext.Contacts!
                .Where(c =>
                    string.IsNullOrWhiteSpace(c.Email)
                    && string.IsNullOrWhiteSpace(c.Phone)
                    && string.IsNullOrWhiteSpace(c.PhoneRaw)
                    && c.CreatedByIp == ipAddress
                    && c.CreatedByUserAgent == userAgent)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .FirstOrDefault();
        }

        private Contact? FindExistingContactByPriority(string? email, string? phone, string? ipAddress, string? userAgent)
        {
            var normalizedEmail = NormalizeEmail(email);

            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                var emailMatch = pgDbContext.Contacts!.FirstOrDefault(c => c.Email == normalizedEmail);
                if (emailMatch != null)
                {
                    return emailMatch;
                }
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var exactPhoneMatch = pgDbContext.Contacts!.FirstOrDefault(c => c.Phone == phone);
                if (exactPhoneMatch != null)
                {
                    return exactPhoneMatch;
                }

                var normalized = phoneNormalizationService.Normalize(phone);

                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    var normalizedPhoneMatch = pgDbContext.Contacts!.FirstOrDefault(c => c.Phone == normalized);
                    if (normalizedPhoneMatch != null)
                    {
                        return normalizedPhoneMatch;
                    }
                }

                var rawPhoneMatch = pgDbContext.Contacts!.FirstOrDefault(c => c.PhoneRaw == phone);
                if (rawPhoneMatch != null)
                {
                    return rawPhoneMatch;
                }
            }

            return FindPotentialContact(ipAddress, userAgent);
        }

        private async Task EnrichWithDomainId(Contact contact)
        {
            if (string.IsNullOrWhiteSpace(contact.Email))
            {
                return;
            }

            var domainName = domainService.GetDomainNameByEmail(contact.Email);

            var domainsQueryResult = await pgDbContext!.Domains!.FirstOrDefaultAsync(domain => domain.Name == domainName);

            if (domainsQueryResult != null)
            {
                contact.DomainId = domainsQueryResult.Id;
                contact.Domain = domainsQueryResult;
            }
            else
            {
                contact.Domain = new Domain()
                {
                    Name = domainName,
                    AccountStatus = AccountSyncStatus.NotInitialized,
                };

                await domainService.SaveAsync(contact.Domain);
            }
        }

        private async Task EnrichWithDomainIdAsync(List<Contact> contacts)
        {
            var newDomains = new Dictionary<string, Domain>();

            var contactsWithDomain = from contact in contacts
                                     where !string.IsNullOrWhiteSpace(contact.Email)
                                     select new
                                     {
                                         Contact = contact,
                                         DomainName = domainService.GetDomainNameByEmail(contact.Email!),
                                     };

            try
            {
                var contactsWithDomainInfo = (from contactWithDomain in contactsWithDomain
                                              join domain in pgDbContext.Domains! on contactWithDomain.DomainName equals domain.Name into domainTemp
                                              from domain in domainTemp.DefaultIfEmpty()
                                              select new
                                              {
                                                  Contact = contactWithDomain.Contact,
                                                  DomainName = contactWithDomain.DomainName,
                                                  Domain = domain,
                                                  DomainId = domain?.Id ?? 0,
                                              }).ToList();

                foreach (var contactWithDomainInfo in contactsWithDomainInfo)
                {
                    if (contactWithDomainInfo.DomainId != 0)
                    {
                        contactWithDomainInfo.Contact.DomainId = contactWithDomainInfo.DomainId;
                        contactWithDomainInfo.Contact.Domain = contactWithDomainInfo.Domain;
                    }
                    else
                    {
                        var existingDomain = from newDomain in newDomains
                                             where newDomain.Key == contactWithDomainInfo.DomainName
                                             select newDomain;

                        if (!existingDomain.Any())
                        {
                            var domain = new Domain()
                            {
                                Name = contactWithDomainInfo.DomainName,
                                Source = contactWithDomainInfo.Contact.Email,
                                AccountStatus = AccountSyncStatus.NotIntended,
                            };

                            newDomains.Add(domain.Name, domain);
                            await domainService.SaveAsync(domain);
                            contactWithDomainInfo.Contact.Domain = domain;
                        }
                        else
                        {
                            contactWithDomainInfo.Contact.Domain = existingDomain.FirstOrDefault().Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "error");
                throw;
            }
        }

        private void EnrichWithAccountId(List<Contact> contacts)
        {
            foreach (var contact in contacts)
            {
                var domain = contact.Domain;
                if (domain != null && contact.AccountId == null)
                {
                    contact.AccountId = domain.AccountId;
                }
            }
        }

        private void EnrichWithAccountId(Contact contact)
        {
            var domain = contact.Domain;
            if (domain != null && contact.AccountId == null)
            {
                contact.AccountId = domain.AccountId;
            }
        }

        private void NormalizePhone(Contact contact)
        {
            // Only normalize when Phone has a value that hasn't been normalized yet
            var rawPhone = contact.Phone;
            if (string.IsNullOrWhiteSpace(rawPhone))
            {
                return;
            }

            // Already in E.164 format — skip
            if (rawPhone.StartsWith('+') && rawPhone.Length >= 8)
            {
                // Preserve original input in PhoneRaw if not already set
                if (string.IsNullOrWhiteSpace(contact.PhoneRaw))
                {
                    contact.PhoneRaw = rawPhone;
                }

                return;
            }

            // Always preserve the original user input
            if (string.IsNullOrWhiteSpace(contact.PhoneRaw))
            {
                contact.PhoneRaw = rawPhone;
            }

            var normalized = phoneNormalizationService.Normalize(rawPhone, contact.CountryCode, contact.Language);

            if (normalized != null)
            {
                contact.Phone = normalized;
            }
            else
            {
                // Could not normalize — clear Phone, raw is already preserved
                contact.Phone = null;
            }
        }
    }
}