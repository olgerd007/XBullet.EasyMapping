using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public sealed class MapperWriteReceivedApprovals : Task
{
    [Required]
    public string GeneratedDirectory
    {
        get; set;
    }

    public string ApprovalDirectory
    {
        get; set;
    }

    [Required]
    public string ProjectDirectory
    {
        get; set;
    }

    public ITaskItem[] ApprovedFiles
    {
        get; set;
    }

    public override bool Execute()
    {
        try
        {
            var generatedDirectory = ResolvePath(GeneratedDirectory);
            if (!Directory.Exists(generatedDirectory))
            {
                return true;
            }

            foreach (var candidate in Directory.GetFiles(generatedDirectory, "*.ModelApproval.g.cs"))
            {
                var lines = File.ReadAllLines(candidate);
                var start = Array.IndexOf(lines, "// <MapperModelApproval>");
                var end = Array.IndexOf(lines, "// </MapperModelApproval>");
                if (start < 0 || end <= start + 1)
                {
                    throw new InvalidDataException("Invalid mapper approval candidate: " + candidate);
                }

                var content = new StringBuilder();
                for (var index = start + 1; index < end; index++)
                {
                    if (!lines[index].StartsWith("// ", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Invalid mapper approval comment: " + candidate);
                    }

                    content.Append(lines[index].Substring(3)).Append('\n');
                }

                var mapperName = Path.GetFileName(candidate);
                mapperName = mapperName.Substring(0, mapperName.Length - ".ModelApproval.g.cs".Length);
                var approvedName = mapperName + ".mapper.approved.txt";
                var approvals = (ApprovedFiles ?? new ITaskItem[0])
                    .Where(item => Path.GetFileName(item.ItemSpec) == approvedName).ToArray();
                var approvalDirectory = GetApprovalDirectory(lines, approvals);
                var receivedPath = Path.Combine(approvalDirectory, mapperName + ".mapper.received.txt");
                var text = content.ToString();
                if (approvals.Length == 1 && File.Exists(ResolvePath(approvals[0].ItemSpec)) &&
                    Normalize(File.ReadAllText(ResolvePath(approvals[0].ItemSpec))) == text)
                {
                    if (File.Exists(receivedPath))
                    {
                        File.Delete(receivedPath);
                    }

                    continue;
                }

                Directory.CreateDirectory(approvalDirectory);
                if (!File.Exists(receivedPath) || File.ReadAllText(receivedPath) != text)
                {
                    File.WriteAllText(receivedPath, text, new UTF8Encoding(false));
                }

                Log.LogMessage(MessageImportance.High,
                    "Mapper: review '{0}', then rename it to '{1}' (replace the old approved file) and rebuild.",
                    receivedPath, approvedName);
            }

            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception);
            return false;
        }
    }

    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd('\n') + "\n";
    }

    private string GetApprovalDirectory(string[] lines, ITaskItem[] approvals)
    {
        if (!string.IsNullOrWhiteSpace(ApprovalDirectory))
        {
            return ResolvePath(ApprovalDirectory);
        }

        if (approvals.Length == 1)
        {
            var approvedPath = ResolvePath(approvals[0].ItemSpec);
            if (IsInProject(approvedPath))
            {
                return Path.GetDirectoryName(approvedPath);
            }
        }

        const string marker = "// MapperSourcePath: ";
        var metadata = lines.SingleOrDefault(line => line.StartsWith(marker, StringComparison.Ordinal));
        if (metadata != null)
        {
            var sourcePath = Encoding.UTF8.GetString(Convert.FromBase64String(metadata.Substring(marker.Length)));
            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                var resolved = ResolvePath(sourcePath);
                if (IsInProject(resolved))
                {
                    return Path.GetDirectoryName(resolved);
                }
            }
        }

        // Linked sources outside the project must not cause writes into another checkout.
        return Path.GetFullPath(ProjectDirectory);
    }

    private string ResolvePath(string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(ProjectDirectory, path));
    }

    private bool IsInProject(string path)
    {
        var root = Path.GetFullPath(ProjectDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.StartsWith(root, comparison);
    }
}
