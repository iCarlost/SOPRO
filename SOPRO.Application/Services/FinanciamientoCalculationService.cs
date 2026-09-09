using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Calcula el financiamiento del proyecto a partir del flujo de caja del programa de obra.
    ///
    /// Criterio oficial para SOPRO:
    /// - Egresos del periodo = CD programado + CI del periodo
    /// - Ingresos del periodo = Anticipo + Estimaciones cobradas - Amortización del anticipo
    /// - Costo financiero = aplicación de la tasa efectiva al saldo acumulado deficitario
    /// - % Financiamiento = Costo financiero total / Base (CD o CD+CI)
    ///
    /// Desde N7-17c la aritmética del flujo delega en <see cref="FinancingCalculationAdapter"/>,
    /// que mapea la entidades a <c>Sopro.Calculation.Financing</c> y persiste el resultado.
    /// </summary>
    public sealed class FinanciamientoCalculationService
    {
        public ConfiguracionFinanciamiento ObtenerOCrear(SOPROContext context, int proyectoId)
        {
            var config = context.ConfiguracionesFinanciamiento
                .Include(c => c.FilasFlujo)
                .FirstOrDefault(c => c.ProyectoId == proyectoId);

            if (config != null)
                return config;

            try
            {
                config = new ConfiguracionFinanciamiento
                {
                    ProyectoId = proyectoId,
                    TasaTIIE = 11.0m,
                    PuntosAdicionales = 3.0m,
                    PorcentajeAnticipo = 30.0m,
                    PeriodosAmortizacionAnticipo = 1,
                    DesfaseCobro = 1,
                    BaseCalculo = "Acumulable",
                    InteresesNegativos = 0m,
                    InteresesPositivos = 0m,
                    FinanciamientoNeto = 0m,
                    PorcentajeCalculado = 0m,
                    FechaCalculo = null
                };

                context.ConfiguracionesFinanciamiento.Add(config);
                context.SaveChanges();
            }
            catch (DbUpdateException)
            {
                EnsureLegacyCompatibleInsert(context, proyectoId);
                config = context.ConfiguracionesFinanciamiento
                    .Include(c => c.FilasFlujo)
                    .FirstOrDefault(c => c.ProyectoId == proyectoId);

                if (config == null)
                    throw;
            }

            return config;
        }


        private void EnsureLegacyCompatibleInsert(SOPROContext context, int proyectoId)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                connection.Open();

            try
            {
                var columns = ReadTableInfo(connection, "ConfiguracionesFinanciamiento");
                if (columns.Count == 0)
                    throw new InvalidOperationException("No se pudo inspeccionar la tabla ConfiguracionesFinanciamiento.");

                var insertable = columns
                    .Where(c => !c.IsPrimaryKey && !string.Equals(c.Name, "Id", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                using var cmd = connection.CreateCommand();
                var colNames = new List<string>();
                var paramNames = new List<string>();

                int i = 0;
                foreach (var col in insertable)
                {
                    colNames.Add(col.Name);
                    var paramName = "@p" + i++;
                    paramNames.Add(paramName);

                    var p = cmd.CreateParameter();
                    p.ParameterName = paramName;
                    p.Value = GetDefaultValueForColumn(col, proyectoId) ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }

                cmd.CommandText = $"INSERT INTO ConfiguracionesFinanciamiento ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)})";
                cmd.ExecuteNonQuery();
            }
            finally
            {
                if (shouldClose)
                    connection.Close();
            }
        }

        private static List<TableColumnInfo> ReadTableInfo(System.Data.Common.DbConnection connection, string tableName)
        {
            var list = new List<TableColumnInfo>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TableColumnInfo
                {
                    Name = Convert.ToString(reader[1], CultureInfo.InvariantCulture) ?? string.Empty,
                    Type = Convert.ToString(reader[2], CultureInfo.InvariantCulture) ?? string.Empty,
                    NotNull = Convert.ToInt32(reader[3], CultureInfo.InvariantCulture) == 1,
                    DefaultValueSql = reader.IsDBNull(4) ? null : Convert.ToString(reader[4], CultureInfo.InvariantCulture),
                    IsPrimaryKey = Convert.ToInt32(reader[5], CultureInfo.InvariantCulture) == 1
                });
            }
            return list;
        }

        private static object? GetDefaultValueForColumn(TableColumnInfo col, int proyectoId)
        {
            switch (col.Name)
            {
                case "ProyectoId": return proyectoId;
                case "TasaTIIE": return 11.0m;
                case "PuntosAdicionales": return 3.0m;
                case "PorcentajeAnticipo": return 30.0m;
                case "PeriodosAmortizacionAnticipo": return 1;
                case "DesfaseCobro": return 1;
                case "BaseCalculo": return "Acumulable";
                case "InteresesNegativos": return 0m;
                case "InteresesPositivos": return 0m;
                case "FinanciamientoNeto": return 0m;
                case "PorcentajeCalculado": return 0m;
                case "FechaCalculo": return DBNull.Value;
                // Compatibilidad con esquemas anteriores / importadores
                case "TasaInteresAnual": return 14.0m;
                case "TasaInteresMensual": return decimal.Round(14.0m / 12.0m, 6, MidpointRounding.AwayFromZero);
                case "UsaTasaDiaria": return 0;
                case "DiasCobro": return 0;
                case "PeriodoEntregaAnticipo": return 1;
                case "PeriodosAmortizacion": return 1;
                case "PorcentajeAmortizacion": return 30.0m;
            }

            if (!col.NotNull)
                return DBNull.Value;

            if (!string.IsNullOrWhiteSpace(col.DefaultValueSql))
                return DBNull.Value;

            var type = (col.Type ?? string.Empty).ToUpperInvariant();
            if (type.Contains("CHAR") || type.Contains("TEXT") || type.Contains("CLOB"))
                return string.Empty;
            if (type.Contains("INT"))
                return 0;
            if (type.Contains("REAL") || type.Contains("FLOA") || type.Contains("DOUB"))
                return 0.0;
            if (type.Contains("NUM") || type.Contains("DEC"))
                return 0m;
            if (type.Contains("DATE") || type.Contains("TIME"))
                return DateTime.Now;

            return 0;
        }

        private sealed class TableColumnInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public bool NotNull { get; set; }
            public string? DefaultValueSql { get; set; }
            public bool IsPrimaryKey { get; set; }
        }

        public decimal Calcular(SOPROContext context, ConfiguracionFinanciamiento config, Proyecto proyecto, bool modeloDual = false)
            => FinancingCalculationAdapter.Calcular(context, config, proyecto, modeloDual);
    }
}