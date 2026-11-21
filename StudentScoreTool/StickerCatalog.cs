namespace StudentScoreTool;

public static class StickerCatalog
{
    public static readonly string[] Happy =
    [
        "/Assets/stickers/happy-01.png",
        "/Assets/stickers/happy-02.png",
        "/Assets/stickers/happy-03.png",
        "/Assets/stickers/happy-04.png",
        "/Assets/stickers/happy-05.png",
        "/Assets/stickers/happy-06.png",
        "/Assets/stickers/happy-07.png",
        "/Assets/stickers/happy-08.png",
        "/Assets/stickers/happy-09.png",
        "/Assets/stickers/happy-10.png",
        "/Assets/stickers/happy-11.png",
        "/Assets/stickers/happy-12.png",
        "/Assets/stickers/happy-13.png",
        "/Assets/stickers/happy-14.png",
        "/Assets/stickers/happy-15.png",
        "/Assets/stickers/happy-16.png",
        "/Assets/stickers/happy-17.png",
        "/Assets/stickers/happy-18.png",
        "/Assets/stickers/happy-19.png",
        "/Assets/stickers/happy-20.png",
        "/Assets/stickers/happy-21.png",
        "/Assets/stickers/happy-22.png",
        "/Assets/stickers/happy-23.png",
        "/Assets/stickers/happy-24.png",
        "/Assets/stickers/happy-25.png",
        "/Assets/stickers/happy-26.png",
        "/Assets/stickers/happy-27.png"
    ];

    public static readonly string[] Sad =
    [
        "/Assets/stickers/sad-01.png",
        "/Assets/stickers/sad-02.png",
        "/Assets/stickers/sad-03.png",
        "/Assets/stickers/sad-04.png",
        "/Assets/stickers/sad-05.png",
        "/Assets/stickers/sad-06.png",
        "/Assets/stickers/sad-07.png",
        "/Assets/stickers/sad-08.png",
        "/Assets/stickers/sad-09.png",
        "/Assets/stickers/sad-10.png",
        "/Assets/stickers/sad-11.png",
        "/Assets/stickers/sad-12.png",
        "/Assets/stickers/sad-13.png",
        "/Assets/stickers/sad-14.png",
        "/Assets/stickers/sad-15.png",
        "/Assets/stickers/sad-16.png",
        "/Assets/stickers/sad-17.png",
        "/Assets/stickers/sad-18.png",
        "/Assets/stickers/sad-19.png",
        "/Assets/stickers/sad-20.png",
        "/Assets/stickers/sad-21.png",
        "/Assets/stickers/sad-22.png"
    ];

    public static IReadOnlyList<Uri> PickHappy() => PickMany(Happy);

    public static IReadOnlyList<Uri> PickSad() => PickMany(Sad);

    private static IReadOnlyList<Uri> PickMany(IReadOnlyList<string> paths, int minCount = 2, int maxCount = 3)
    {
        if (paths.Count == 0)
        {
            return [];
        }

        var count = Math.Min(paths.Count, Random.Shared.Next(minCount, maxCount + 1));
        return paths
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .Select(ToPackUri)
            .ToList();
    }

    private static Uri ToPackUri(string path)
    {
        var relative = path.StartsWith('/') ? path : "/" + path;
        return new Uri("pack://application:,,," + relative, UriKind.Absolute);
    }
}
