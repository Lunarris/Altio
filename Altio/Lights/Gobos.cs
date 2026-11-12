using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Altio.Lights;

public sealed record Gobo(string Name, string Path);

public static class Gobos
{
    public static IReadOnlyList<Gobo> All { get; } = Load();

    private static List<Gobo> Load()
    {
        var list = new List<Gobo>();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Altio.gobos.csv");
        if (stream == null)
            return list;

        using var reader = new StreamReader(stream);
        reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            var split = line.Split(',', 2);
            if (split.Length == 2 && split[0].Length > 0)
                list.Add(new Gobo(split[1].Trim(), split[0].Trim()));
        }
        return list;
    }
}
