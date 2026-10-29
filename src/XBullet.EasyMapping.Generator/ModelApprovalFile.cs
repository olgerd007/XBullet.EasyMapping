namespace XBullet.EasyMapping.Generator;

internal sealed class ModelApprovalFile
{
    public ModelApprovalFile(string path, string? content)
    {
        FileName = path.Replace('\\', '/').Split('/').Last();
        Content = content;
    }

    public string FileName
    {
        get;
    }
    public string? Content
    {
        get;
    }
}
