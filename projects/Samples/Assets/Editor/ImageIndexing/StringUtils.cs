
public static class StringUtils
{
    public static string SanitizePath(string path)
    {
        return path.Replace("\\", "/");
    }
}
