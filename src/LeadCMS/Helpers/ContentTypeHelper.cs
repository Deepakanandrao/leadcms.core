// <copyright file="ContentTypeHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.StaticFiles;

namespace LeadCMS.Helpers
{
    public static class ContentTypeHelper
    {
        public static FileExtensionContentTypeProvider CreateCustomizedProvider()
        {
            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".avif"] = "image/avif";
            return provider;
        }

        public static string GetMimeTypeOrThrow(string fileName, ModelStateDictionary modelState)
        {
            var provider = CreateCustomizedProvider();

            if (!provider.TryGetContentType(fileName, out var mimeType))
            {
                modelState.AddModelError("FileName", "Unsupported MIME type");
                throw new InvalidModelStateException(modelState);
            }

            return mimeType!;
        }
    }
}
