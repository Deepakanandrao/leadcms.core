// <copyright file="CsvClassMapHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace LeadCMS.Formatters.Csv;

public static class CsvClassMapHelper
{
    public static void RegisterCamelCaseClassMap(this CsvContext csvContext, Type itemType)
    {
        var mapType = typeof(DefaultClassMap<>);
        var constructedMapType = mapType.MakeGenericType(itemType!);

        var map = (ClassMap)Activator.CreateInstance(constructedMapType)!;
        map.AutoMap(CultureInfo.InvariantCulture);

        // AutoMap skips array properties — add them explicitly so the registered TypeConverter is used
        var mappedMembers = new HashSet<string>(map.MemberMaps.Select(m => m.Data.Member!.Name));

        var arrayProperties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string[]) && !mappedMembers.Contains(p.Name));

        foreach (var prop in arrayProperties)
        {
            map.Map(itemType, prop).Data.IsOptional = true;
        }

        foreach (var memberMapData in map.MemberMaps.Select(m => m.Data))
        {
            memberMapData.Names.Add(JsonNamingPolicy.CamelCase.ConvertName(memberMapData.Member!.Name));
        }

        csvContext.RegisterClassMap(map);
    }
}