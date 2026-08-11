// Partial: table rendering for MarkdownBlockRenderer.
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed partial class MarkdownBlockRenderer
    {
        private static VisualElement RenderTable(in MdBlock b)
        {
            var table = new VisualElement(); table.AddToClassList("md-table");

            if (b.TableRows == null || b.TableRows.Count == 0) return table;

            // First row is the header.
            var headerRow = new VisualElement(); headerRow.AddToClassList("md-table-row");
            for (int colIdx = 0; colIdx < b.TableRows[0].Length; colIdx++)
            {
                var lbl = ChatLabel.Selectable(MarkdownInline.ToRichText(b.TableRows[0][colIdx]), richText: true);
                lbl.AddToClassList("md-th");
                ApplyAlign(lbl, b.Aligns, colIdx);
                headerRow.Add(lbl);
            }
            table.Add(headerRow);

            // Remaining rows are data rows.
            for (int i = 1; i < b.TableRows.Count; i++)
            {
                var row = new VisualElement(); row.AddToClassList("md-table-row");
                for (int colIdx = 0; colIdx < b.TableRows[i].Length; colIdx++)
                {
                    var lbl = ChatLabel.Selectable(MarkdownInline.ToRichText(b.TableRows[i][colIdx]), richText: true);
                    lbl.AddToClassList("md-td");
                    ApplyAlign(lbl, b.Aligns, colIdx);
                    row.Add(lbl);
                }
                table.Add(row);
            }

            return table;
        }

        private static void ApplyAlign(Label lbl, string[] aligns, int colIdx)
        {
            if (aligns == null || colIdx >= aligns.Length) return;
            if (aligns[colIdx] == "center") lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            else if (aligns[colIdx] == "right") lbl.style.unityTextAlign = TextAnchor.MiddleRight;
            // "left" and "none" use the default alignment
        }
    }
}
