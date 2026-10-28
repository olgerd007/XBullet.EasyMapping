using System.Collections.Immutable;

namespace XBullet.EasyMapping.Generator;

internal static class ModelApprovalVerifier
{
    public static GeneratedMapper Verify(GeneratedMapper mapper, ImmutableArray<ModelApprovalFile> files)
    {
        if (mapper.Approval is not { } approval) { return mapper; }
        var matches = files.Where(file => file.FileName == approval.FileName).ToArray();
        var reason = matches.Length switch
        {
            0 => "is missing",
            > 1 => "has duplicate AdditionalFiles entries",
            _ => matches[0].Content is not { } content ? "cannot be read" :
                ModelApprovalSnapshot.Normalize(content) == approval.Content ? null : "has changed"
        };
        if (reason is null) { return mapper; }
        var error = approval.Diagnostic.WithArguments(
            $"Model approval '{approval.FileName}' {reason}. Review the .mapper.received.txt file written by the build integration and rename it to '{approval.FileName}' (replace the old approved file), then rebuild. The candidate is also available in '{approval.SourceHint}'");
        return new GeneratedMapper(mapper.HintName, mapper.Source, mapper.Diagnostics.Add(error), approval);
    }
}
