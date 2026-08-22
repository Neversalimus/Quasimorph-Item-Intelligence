using System.Collections.Generic;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void AppendOverviewBaronHabitat(IList<LootBaronSpecialSource> sources)
        {
            List<BaronHabitatNode> roots = ResolveBaronHabitatTree(sources);
            if (roots == null || roots.Count == 0) return;

            BrowserLines.Add(BrowserLine.Header(Ui("ui.baron_habitat"), string.Empty));
            for (int i = 0; i < roots.Count; i++)
                AppendOverviewBaronHabitatNode(roots[i], 0);
        }



        private static void AppendOverviewBaronHabitatNode(BaronHabitatNode node, int depth)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Label)) return;
            string indent = depth <= 0 ? string.Empty : new string(' ', depth * 4);
            if (node.IsHabitat)
                BrowserLines.Add(BrowserLine.Normal(indent + "• " + node.Label, string.Empty));
            else
                BrowserLines.Add(BrowserLine.Note(indent + node.Label));

            for (int i = 0; i < node.Children.Count; i++)
                AppendOverviewBaronHabitatNode(node.Children[i], depth + 1);
        }
    }
}
