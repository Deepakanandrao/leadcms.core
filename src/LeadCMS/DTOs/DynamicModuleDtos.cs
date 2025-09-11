// <copyright file="DynamicModuleDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.DTOs;

public class DynamicModuleDto
{
    [Required]
    public string ModuleName { get; set; } = string.Empty;

    [Required]
    public string ModulePath { get; set; } = string.Empty;

    public string? AddButtonContent { get; set; }

    public DynamicSchemasDto? Schemas { get; set; }

    public DynamicFormFnsDto? FormFns { get; set; }

    public DynamicTablePropsDto? TableProps { get; set; }

    public DynamicExtraActionsDto? ExtraActions { get; set; }
}

public class DynamicSchemasDto
{
    public DtoSchema? Details { get; set; }

    public DtoSchema? Update { get; set; }

    public DtoSchema? Create { get; set; }
}

public class DynamicFormFnsDto
{
    public DynamicApiFnDto? GetItemFn { get; set; }

    public DynamicApiFnDto? CreateItemFn { get; set; }

    public DynamicApiFnDto? UpdateItemFn { get; set; }

    public DynamicApiFnDto? DeleteItemFn { get; set; }
}

public class DynamicTablePropsDto
{
    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public DynamicApiFnDto? GetItemsFn { get; set; }

    public DtoSchema? Schema { get; set; }

    public List<string> InitiallyShownColumns { get; set; } = new();
}

public class DynamicExtraActionsDto
{
    public ExportActionDto? Export { get; set; }

    public ImportActionDto? Import { get; set; }

    public bool? ShowColumnsPanel { get; set; }

    public bool? ShowFiltersPanel { get; set; }
}

public class ExportActionDto
{
    [Required]
    public bool? ShowButton { get; set; }

    [Required]
    public DynamicApiFnDto? ExportItemsFn { get; set; }
}

public class ImportActionDto
{
    [Required]
    public bool? ShowButton { get; set; }

    [Required]
    public DtoSchema? ImportSchema { get; set; }

    [Required]
    public DynamicApiFnDto? ImportItemsFn { get; set; }
}

public class DynamicApiFnDto
{
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string Method { get; set; } = string.Empty;
}

public class DtoSchema
{
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    public Dictionary<string, object>? Properties { get; set; }

    [Required]
    public List<string>? Required { get; set; }
}