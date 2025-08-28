// <copyright file="DynamicModuleDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.DTOs;

public class DynamicModuleDto
{
    public string ModuleName { get; set; } = string.Empty;

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
    public string Key { get; set; } = string.Empty;

    public DynamicApiFnDto? GetItemsFn { get; set; }

    public DtoSchema? Schema { get; set; }

    public List<string>? InitiallyShownColumns { get; set; }
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
    public bool? ShowButton { get; set; }

    public DynamicApiFnDto? ExportItemsFn { get; set; }
}

public class ImportActionDto
{
    public bool? ShowButton { get; set; }

    public DtoSchema? ImportSchema { get; set; }

    public DynamicApiFnDto? ImportItemsFn { get; set; }
}

public class DynamicApiFnDto
{
    public string Endpoint { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;
}

public class DtoSchema
{
    public string Type { get; set; } = string.Empty;

    public Dictionary<string, object>? Properties { get; set; }

    public List<string>? Required { get; set; }
}