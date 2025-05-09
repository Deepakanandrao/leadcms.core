// <copyright file="DiscountService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Interfaces;

namespace LeadCMS.Services;

public class DiscountService : IDiscountService
{
    private PgDbContext pgDbContext;

    public DiscountService(PgDbContext pgDbContext)
    {
        this.pgDbContext = pgDbContext;
    }

    public async Task SaveAsync(Discount discount)
    {
        if (discount.Id > 0)
        {
            pgDbContext.Discounts!.Update(discount);
        }
        else
        {
            await pgDbContext.Discounts!.AddAsync(discount);
        }
    }

    public Task SaveRangeAsync(List<Discount> discounts)
    {
        throw new NotImplementedException();
    }

    public void SetDBContext(PgDbContext pgDbContext)
    {
        this.pgDbContext = pgDbContext;
    }
}