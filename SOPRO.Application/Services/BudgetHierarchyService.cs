using SOPRO.Application.Models.Presupuesto;

namespace SOPRO.Application.Services
{
    public static class BudgetHierarchyService
    {
        public static int GetLevelFromType(string? tipo)
        {
            return tipo switch
            {
                "Capitulo" => 0,
                "Subcapitulo" => 1,
                "Nivel 1" => 2,
                "Nivel 2" => 3,
                "Nivel 3" => 4,
                _ => 5
            };
        }

        public static int GetIndentLevel(string? tipo) => GetLevelFromType(tipo);

        public static int FindParentIndex(IReadOnlyList<BudgetHierarchyRow> rows, int rowIndex)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (rowIndex < 0 || rowIndex >= rows.Count) return -1;

            int currentLevel = GetLevelFromType(rows[rowIndex].Tipo);
            for (int i = rowIndex - 1; i >= 0; i--)
            {
                if (!rows[i].HasContent) continue;
                int candidateLevel = GetLevelFromType(rows[i].Tipo);
                if (candidateLevel < currentLevel)
                    return i;
            }

            return -1;
        }

        public static List<int> GetAncestorIndexes(IReadOnlyList<BudgetHierarchyRow> rows, int rowIndex)
        {
            var indexes = new List<int>();
            int parentIndex = FindParentIndex(rows, rowIndex);
            while (parentIndex >= 0)
            {
                indexes.Add(parentIndex);
                parentIndex = FindParentIndex(rows, parentIndex);
            }

            return indexes;
        }

        public static decimal CalculateAggregatorTotal(IReadOnlyList<BudgetHierarchyRow> rows, int aggregatorIndex)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (aggregatorIndex < 0 || aggregatorIndex >= rows.Count) return 0m;

            var aggregator = rows[aggregatorIndex];
            if (string.Equals(aggregator.Tipo, "Concepto", StringComparison.OrdinalIgnoreCase))
                return 0m;

            int aggregatorLevel = GetLevelFromType(aggregator.Tipo);
            decimal total = 0m;

            for (int i = aggregatorIndex + 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.HasContent) continue;

                int level = GetLevelFromType(row.Tipo);
                if (level <= aggregatorLevel)
                    break;

                if (row.IsConcept)
                    total += row.Importe;
            }

            return total;
        }

        public static Dictionary<int, decimal> CalculateAggregatorTotals(IReadOnlyList<BudgetHierarchyRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var totals = new Dictionary<int, decimal>();
            for (int level = 4; level >= 0; level--)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (!row.HasContent || row.IsConcept) continue;
                    if (GetLevelFromType(row.Tipo) != level) continue;

                    totals[i] = CalculateAggregatorTotal(rows, i);
                }
            }

            return totals;
        }

        public static Dictionary<int, int?> BuildConceptSequenceMap(IReadOnlyList<BudgetHierarchyRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var map = new Dictionary<int, int?>();
            int sequence = 1;

            foreach (var row in rows.OrderBy(r => r.RowIndex))
            {
                if (row.IsConcept && row.HasContent)
                {
                    map[row.RowIndex] = sequence;
                    sequence++;
                }
                else
                {
                    map[row.RowIndex] = null;
                }
            }

            return map;
        }

        public static List<int> CollectHierarchicalBlock(IReadOnlyList<BudgetHierarchyRow> rows, int originRow)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var indexes = new List<int>();
            if (originRow < 0 || originRow >= rows.Count) return indexes;

            indexes.Add(originRow);
            var origin = rows[originRow];
            if (!origin.HasContent || origin.IsConcept) return indexes;

            int parentLevel = GetIndentLevel(origin.Tipo);
            for (int i = originRow + 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.HasContent) continue;

                int childLevel = GetIndentLevel(row.Tipo);
                if (childLevel <= parentLevel)
                    break;

                indexes.Add(i);
            }

            return indexes;
        }

        public static List<int> GetIntermediateEmptyRowIndexes(IReadOnlyList<BudgetHierarchyRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            int lastContentRow = -1;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (rows[i].HasContent)
                {
                    lastContentRow = i;
                    break;
                }
            }

            var indexes = new List<int>();
            if (lastContentRow <= 0) return indexes;

            for (int i = 0; i < lastContentRow; i++)
            {
                if (!rows[i].HasContent)
                    indexes.Add(i);
            }

            return indexes;
        }
    }
}
