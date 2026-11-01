using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Falak.Interop;

public sealed record AccentChoice(string Id, string NameAr, Color Light, Color Dark)
{
	public static readonly AccentChoice Emerald = new AccentChoice("emerald", "زمر\u0651دي", ParseColor("#0E6B53"), ParseColor("#22A57C"));

	public static readonly AccentChoice Garnet = new AccentChoice("garnet", "عن\u0651ابي", ParseColor("#8C2342"), ParseColor("#D9637F"));

	public static readonly AccentChoice Oud = new AccentChoice("oud", "عودي", ParseColor("#86562A"), ParseColor("#CC9A5E"));

	public static readonly AccentChoice Night = new AccentChoice("night", "كحلي", ParseColor("#1F3A68"), ParseColor("#7C9BDB"));

	public static readonly AccentChoice Plum = new AccentChoice("plum", "ملكي", ParseColor("#5A2D82"), ParseColor("#A985D6"));

	public static readonly AccentChoice Graphite = new AccentChoice("graphite", "فحمي", ParseColor("#30363B"), ParseColor("#B4BCC2"));

	public static IReadOnlyList<AccentChoice> All { get; } = System.Array.AsReadOnly(new AccentChoice[]
    {
        Emerald, Garnet, Oud, Night, Plum, Graphite
    });

    // Avoid depending on BrandPalette static initialization while initializing accents.
    private static Color ParseColor(string value) => (Color)ColorConverter.ConvertFromString(value);

	public static AccentChoice ById(string? id)
	{
		return All.FirstOrDefault((AccentChoice a) => a is not null && string.Equals(a.Id, id, System.StringComparison.OrdinalIgnoreCase)) ?? Emerald;
	}
}
