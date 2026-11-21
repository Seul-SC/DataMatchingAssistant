namespace StudentScoreTool;

public static class StickerCatalog
{
    // 占位列表。定稿表情包到位后再填入资源路径；当前没有贴纸文件。
    public static readonly string[] Happy = [];

    public static readonly string[] Sad = [];

    public static Uri? PickHappy() => Pick(Happy);

    public static Uri? PickSad() => Pick(Sad);

    private static Uri? Pick(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return null;
        }

        var path = paths[Random.Shared.Next(paths.Count)];
        var relative = path.StartsWith('/') ? path : "/" + path;
        return new Uri("pack://application:,,," + relative, UriKind.Absolute);
    }
}
