// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DataIngestion;
using Microsoft.Shared.Diagnostics;

namespace DataIngestionPlayground.Readers;

/// <summary>
/// Reads Markdown content and converts it to an <see cref="IngestionDocument"/>.
/// </summary>
public sealed class MarkdownReader : IngestionDocumentReader
{
    /// <inheritdoc/>
    public override async Task<IngestionDocument> ReadAsync(Stream source, string identifier, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(source);
        _ = Throw.IfNullOrEmpty(identifier);

        string fileContent = await ReadToEndAsync(source, cancellationToken).ConfigureAwait(false);
        return MarkdownParser.Parse(fileContent, identifier);
    }

    private static async Task<string> ReadToEndAsync(Stream source, CancellationToken cancellationToken)
    {
        using StreamReader reader =
#if NET
            new(source, leaveOpen: true);
#else
            new(source, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
#endif

        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
