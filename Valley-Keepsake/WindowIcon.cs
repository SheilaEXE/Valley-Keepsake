namespace TradutorModsStardew;

internal static class WindowIcon
{
    public static void Apply(Form form)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (!File.Exists(path)) return;
        try { form.Icon = new Icon(path); }
        catch (ArgumentException) { }
    }
}
