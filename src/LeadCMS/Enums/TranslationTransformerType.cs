// <copyright file="TranslationTransformerType.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Enums;

/// <summary>
/// Defines the transformation strategy when creating translation drafts.
/// </summary>
public enum TranslationTransformerType
{
    /// <summary>
    /// Create a new empty instance with only Language and TranslationKey set.
    /// </summary>
    EmptyCopy,

    /// <summary>
    /// Create a copy with all original attributes preserved, except Language.
    /// </summary>
    KeepOriginal,
}
