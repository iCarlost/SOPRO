using System;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Convierte números decimales a su representación en texto (español de México)
    /// </summary>
    public static class NumeroALetrasHelper
    {
        private static readonly string[] Unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE" };
        private static readonly string[] Decenas = { "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE" };
        private static readonly string[] DecenasBase = { "", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
        private static readonly string[] Centenas = { "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };

        /// <summary>
        /// Convierte un número decimal a su representación en letras
        /// </summary>
        /// <param name="numero">Número a convertir</param>
        /// <param name="moneda">Nombre de la moneda (ej: "PESOS", "DÓLARES")</param>
        /// <param name="centavos">Nombre de los centavos (ej: "M.N.", "CENTAVOS")</param>
        /// <returns>Número en letras</returns>
        public static string ConvertirALetras(decimal numero, string moneda = "PESOS", string centavos = "M.N.")
        {
            if (numero == 0)
                return $"CERO {moneda} 00/100 {centavos}";

            long parteEntera = (long)Math.Floor(numero);
            int parteDecimal = (int)Math.Round((numero - parteEntera) * 100);

            string letras = ConvertirEnteroALetras(parteEntera);
            
            return $"{letras} {moneda} {parteDecimal:00}/100 {centavos}";
        }

        private static string ConvertirEnteroALetras(long numero)
        {
            if (numero == 0)
                return "CERO";

            if (numero == 1)
                return "UN";

            string resultado = "";

            // Billones
            if (numero >= 1000000000000)
            {
                long billones = numero / 1000000000000;
                resultado += ConvertirGrupo((int)billones) + " ";
                resultado += billones == 1 ? "BILLÓN " : "BILLONES ";
                numero %= 1000000000000;
            }

            // Millones
            if (numero >= 1000000)
            {
                long millones = numero / 1000000;
                resultado += ConvertirGrupo((int)millones) + " ";
                resultado += millones == 1 ? "MILLÓN " : "MILLONES ";
                numero %= 1000000;
            }

            // Miles
            if (numero >= 1000)
            {
                int miles = (int)(numero / 1000);
                if (miles == 1)
                    resultado += "MIL ";
                else
                    resultado += ConvertirGrupo(miles) + " MIL ";
                numero %= 1000;
            }

            // Centenas, decenas, unidades
            if (numero > 0)
            {
                resultado += ConvertirGrupo((int)numero);
            }

            return resultado.Trim();
        }

        private static string ConvertirGrupo(int numero)
        {
            string resultado = "";

            // Centenas
            int centenas = numero / 100;
            int resto = numero % 100;

            if (centenas > 0)
            {
                if (numero == 100)
                    return "CIEN";
                resultado += Centenas[centenas] + " ";
            }

            // Decenas y unidades
            if (resto >= 10 && resto < 20)
            {
                resultado += Decenas[resto - 10];
            }
            else
            {
                int decenas = resto / 10;
                int unidades = resto % 10;

                if (decenas > 0)
                {
                    if (decenas == 2 && unidades > 0)
                        resultado += "VEINTI" + Unidades[unidades];
                    else
                    {
                        resultado += DecenasBase[decenas];
                        if (unidades > 0)
                            resultado += " Y " + Unidades[unidades];
                    }
                }
                else if (unidades > 0)
                {
                    resultado += Unidades[unidades];
                }
            }

            return resultado.Trim();
        }
    }
}
