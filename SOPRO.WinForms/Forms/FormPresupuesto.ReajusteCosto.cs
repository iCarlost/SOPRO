using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Reajuste de costos: ajuste del agrupador seleccionado y tipos de presupuesto.
    /// </summary>
    public partial class FormPresupuesto
    {

        private void InicializarBotonReajustarCosto()
        {
            _btnReajustarCosto = new ToolStripButton
            {
                BackColor = SystemColors.Control,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black,
                Name = "btnReajustarCosto",
                Size = new Size(111, 19),
                Text = "🎯 Reajustar costo"
            };
            _btnReajustarCosto.Click += btnReajustarCosto_Click;

            var indexImportar = panelToolbar.Items.IndexOf(btnImportarExcel);
            if (indexImportar >= 0)
                panelToolbar.Items.Insert(indexImportar + 1, _btnReajustarCosto);
            else
                panelToolbar.Items.Add(_btnReajustarCosto);
        }

        private void btnReajustarCosto_Click(object? sender, EventArgs e)
        {
            try
            {
                if (dgvPresupuesto.CurrentRow == null)
                {
                    MessageBox.Show("Seleccione un concepto del presupuesto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var rowIndex = dgvPresupuesto.CurrentRow.Index;
                var concepto = dgvPresupuesto.CurrentRow.Tag as ConceptoPresupuesto;
                var tipo = ObtenerCeldaTexto(rowIndex, "Tipo", "Concepto");
                var currentInternalName = (dgvPresupuesto.CurrentCell?.OwningColumn?.Tag as ColumnaPersonalizada)?.NombreInterno ?? string.Empty;
                bool ajustarPorImporte = string.Equals(currentInternalName, "Importe", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(currentInternalName, "ImporteTotal", StringComparison.OrdinalIgnoreCase);

                if (concepto == null)
                {
                    MessageBox.Show("Seleccione un concepto o agrupador del presupuesto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (concepto.EsAgrupador || !string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ajustarPorImporte)
                    {
                        MessageBox.Show("Para agrupadores, el reajuste solo está disponible seleccionando la celda Importe.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    AjustarAgrupadorSeleccionado(rowIndex, concepto);
                    return;
                }

                if (!concepto.MatrizId.HasValue)
                {
                    MessageBox.Show("Por ahora el reajuste solo está disponible para conceptos con matriz asignada.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var matriz = _context.Matrices
                    .Include(m => m.Componentes).ThenInclude(c => c.Material)
                    .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                    .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                    .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                    .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .FirstOrDefault(m => m.Id == concepto.MatrizId.Value);

                if (matriz == null)
                {
                    MessageBox.Show("No se pudo cargar la matriz asignada al concepto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var motorAjuste = new MotorCalculoSopro(_proyecto);
                decimal cantidadConceptoOriginal = concepto.Cantidad;
                decimal factorPu = CalcularFactorPU();
                decimal actualMostrado = ajustarPorImporte
                    ? motorAjuste.Multiplicar(cantidadConceptoOriginal, CalcularPU(matriz.CostoDirecto))
                    : CalcularPU(matriz.CostoDirecto);
                string contexto = ajustarPorImporte
                    ? $"Concepto: {concepto.Descripcion} \n"+
"Se reajustará la matriz por rendimiento/cantidad para acercar el IMPORTE del concepto al monto objetivo. La cantidad del concepto en presupuesto no se modificará."
                    : $"Concepto: {concepto.Descripcion} \n"+
"Se reajustará la matriz por rendimiento/cantidad para acercar el P.U. del concepto al monto objetivo. La cantidad del concepto en presupuesto no se modificará.";

                using var frm = new FormReajustarCosto("Reajustar costo", actualMostrado, contexto);
                if (frm.ShowDialog(this) != DialogResult.OK)
                    return;

                if (factorPu <= 0m)
                {
                    MessageBox.Show("No se pudo calcular el factor del precio unitario del proyecto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                decimal targetCd = ajustarPorImporte
                    ? (cantidadConceptoOriginal > 0m ? frm.TargetAmount / cantidadConceptoOriginal / factorPu : 0m)
                    : (frm.TargetAmount / factorPu);

                var result = MatrixCostAdjustmentService.AdjustMatrixByTargetCost(matriz, _proyecto, frm.SelectedScopes, targetCd);
                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _context.SaveChanges();
                RefrescarPreciosDesdeDB();
                RecalcularTodosLosTotales();
                GuardarCambios();
                dgvPresupuesto.Refresh();

                decimal actualCoherente = ajustarPorImporte
                    ? motorAjuste.Multiplicar(cantidadConceptoOriginal, CalcularPU(result.CurrentCost))
                    : CalcularPU(result.CurrentCost);
                decimal logradoMostrado = ajustarPorImporte
                    ? motorAjuste.Multiplicar(cantidadConceptoOriginal, CalcularPU(result.AchievedCost))
                    : CalcularPU(result.AchievedCost);

                MessageBox.Show(
                    $"Monto actual: {actualCoherente:C2} \n" +
                    $"Objetivo: {frm.TargetAmount:C2} \n" +
                    $"Resultado: {logradoMostrado:C2} \n" +
                    $"Factor aplicado sobre rendimientos/cantidades de la matriz: {result.FactorApplied:N4}",
                    "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al reajustar costo:{ex.Message}", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AjustarAgrupadorSeleccionado(int rowIndex, ConceptoPresupuesto agrupador)
        {
            decimal currentImporte = ObtenerCeldaDecimal(rowIndex, "Importe");
            string contexto = $"Agrupador: {agrupador.Descripcion} \n"+
"Se reajustarán recursivamente los conceptos descendientes por rendimiento/cantidad dentro de sus matrices. La cantidad de los conceptos en presupuesto no se modificará.";
            using var frm = new FormReajustarCosto("Reajustar costo de agrupador", currentImporte, contexto);
            if (frm.ShowDialog(this) != DialogResult.OK)
                return;

            decimal factorPu = CalcularFactorPU();
            if (factorPu <= 0m)
            {
                MessageBox.Show("No se pudo calcular el factor del precio unitario del proyecto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var descendientes = ObtenerConceptosDescendientesHoja(rowIndex, agrupador);
            if (descendientes.Count == 0)
            {
                MessageBox.Show("El agrupador seleccionado no contiene conceptos hoja con matriz asignada.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var matrixIds = descendientes.Where(c => c.MatrizId.HasValue).Select(c => c.MatrizId!.Value).Distinct().ToList();
            var matrices = _context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => matrixIds.Contains(m.Id))
                .ToDictionary(m => m.Id);

            var candidatos = new List<(ConceptoPresupuesto Concepto, MatrixAdjustmentBasis Basis)>();
            int omitidos = 0;
            foreach (var conceptoHijo in descendientes)
            {
                if (!conceptoHijo.MatrizId.HasValue || !matrices.TryGetValue(conceptoHijo.MatrizId.Value, out var matriz))
                {
                    omitidos++;
                    continue;
                }

                var basis = MatrixCostAdjustmentService.GetAdjustmentBasis(matriz, _proyecto, frm.SelectedScopes);
                if (basis == null || !basis.HasAdjustableScope)
                {
                    omitidos++;
                    continue;
                }

                candidatos.Add((conceptoHijo, basis));
            }

            if (candidatos.Count == 0)
            {
                MessageBox.Show("No se encontraron conceptos ajustables con los rubros seleccionados dentro del agrupador.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal targetCdTotal = frm.TargetAmount / factorPu;
            decimal fixedTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.FixedCost);
            decimal adjustableTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.AdjustableCost);
            decimal minTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.MinCost);
            decimal currentTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.CurrentCost);

            if (adjustableTotal <= 0m)
            {
                MessageBox.Show("Los rubros seleccionados no tienen importe ajustable dentro del agrupador.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (targetCdTotal < minTotal - 0.01m)
            {
                MessageBox.Show($"No es posible bajar el agrupador al monto solicitado con los rubros seleccionados. El mínimo alcanzable es {(minTotal * factorPu):C2}.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal factor = (targetCdTotal - fixedTotal) / adjustableTotal;
            if (factor < 0m)
            {
                MessageBox.Show("Con los rubros seleccionados no es posible alcanzar el monto solicitado.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var matricesAjustables = candidatos
                .Select(x => x.Concepto.MatrizId!.Value)
                .Distinct()
                .Select(id => matrices[id])
                .ToList();

            foreach (var matriz in matricesAjustables)
            {
                var result = MatrixCostAdjustmentService.AdjustMatrixByFactor(matriz, _proyecto, frm.SelectedScopes, factor);
                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _context.SaveChanges();
            RefrescarPreciosDesdeDB();
            RecalcularTodosLosTotales();
            GuardarCambios();
            dgvPresupuesto.Refresh();

            decimal logrado = ObtenerCeldaDecimal(rowIndex, "Importe");
            var conceptosIds = descendientes.Select(x => x.Id).ToHashSet();
            int compartidasFuera = _context.ConceptosPresupuesto.Count(c => c.ProyectoId == _proyecto.Id && !c.EsAgrupador && c.MatrizId.HasValue && matrixIds.Contains(c.MatrizId.Value) && !conceptosIds.Contains(c.Id));
            string notaCompartidas = compartidasFuera > 0
                ? $"Nota: {compartidasFuera} concepto(s) fuera del agrupador comparten alguna matriz reajustada."
                : string.Empty;

            MessageBox.Show(
                $"Monto actual: {currentImporte:C2}\n" +
                $"Objetivo: {frm.TargetAmount:C2}\n" +
                $"Resultado: {logrado:C2}\n" +
                $"Conceptos hoja encontrados: {descendientes.Count}\n" +
                $"Conceptos ajustados: {candidatos.Count}\n" +
                $"Conceptos omitidos: {omitidos}\n" +
                $"Factor global aplicado sobre rendimientos/cantidades: {factor:N4}" +
                notaCompartidas,
                "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private List<ConceptoPresupuesto> ObtenerConceptosDescendientesHoja(int rowIndex, ConceptoPresupuesto agrupador)
        {
            var result = new List<ConceptoPresupuesto>();
            int nivelPadre = agrupador.Nivel;
            for (int i = rowIndex + 1; i < dgvPresupuesto.Rows.Count; i++)
            {
                var row = dgvPresupuesto.Rows[i];
                var concepto = row.Tag as ConceptoPresupuesto;
                string tipo = ObtenerCeldaTexto(i, "Tipo");
                string descripcion = ObtenerCeldaTexto(i, "Descripcion");
                string clave = ObtenerCeldaTexto(i, "Clave");
                bool hasContent = concepto != null || !string.IsNullOrWhiteSpace(tipo) || !string.IsNullOrWhiteSpace(descripcion) || !string.IsNullOrWhiteSpace(clave);
                if (!hasContent)
                    continue;

                int nivel = concepto?.Nivel ?? ObtenerNivelDesdeTipoPresupuesto(tipo);
                if (nivel <= nivelPadre)
                    break;

                if (concepto != null && !concepto.EsAgrupador && concepto.MatrizId.HasValue)
                    result.Add(concepto);
            }

            return result;
        }

        private int ObtenerNivelDesdeTipoPresupuesto(string? tipo)
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
    }
}
