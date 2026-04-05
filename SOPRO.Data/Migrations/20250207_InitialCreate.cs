using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SOPRO.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ═══════════════════════════════════════════════════════════
            // TABLA: Proyectos
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "Proyectos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: true),
                    Ubicacion = table.Column<string>(type: "TEXT", nullable: true),
                    Convocante = table.Column<string>(type: "TEXT", nullable: true),
                    Contratista = table.Column<string>(type: "TEXT", nullable: true),
                    ApoderadoLegal = table.Column<string>(type: "TEXT", nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaTermino = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlazoEjecucion = table.Column<int>(type: "INTEGER", nullable: false),
                    PorcentajeIndirectosCentral = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PorcentajeIndirectosCampo = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PorcentajeFinanciamiento = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PorcentajeUtilidad = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PorcentajeIVA = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FactorSalarioReal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    FechaCalculoFSR = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proyectos", x => x.Id);
                });

            // ═══════════════════════════════════════════════════════════
            // TABLA: Materiales
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "Materiales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Clave = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Origen = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialMaestroId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProyectoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notas = table.Column<string>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materiales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materiales_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materiales_ProyectoId_Clave",
                table: "Materiales",
                columns: new[] { "ProyectoId", "Clave" },
                unique: true);

            // ═══════════════════════════════════════════════════════════
            // TABLA: ManoDeObra
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "ManoDeObra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Clave = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SalarioBase = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FactorSalarioReal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    SalarioReal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Origen = table.Column<int>(type: "INTEGER", nullable: false),
                    ManoDeObraMaestraId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProyectoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notas = table.Column<string>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManoDeObra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManoDeObra_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManoDeObra_ProyectoId_Clave",
                table: "ManoDeObra",
                columns: new[] { "ProyectoId", "Clave" },
                unique: true);

            // ═══════════════════════════════════════════════════════════
            // TABLA: Maquinaria
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "Maquinaria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Clave = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PotenciaNominal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TipoCombustible = table.Column<int>(type: "INTEGER", nullable: false),
                    ValorAdquisicion = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ValorLlantas = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ValorPiezasEspeciales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FactorRescate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    VidaEconomica = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TasaInteres = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    HorasEfectivasAnio = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PrimaSeguro = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FactorMantenimiento = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    CantidadCombustible = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PrecioCombustible = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CantidadAceite = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PrecioAceite = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    NumeroLlantas = table.Column<int>(type: "INTEGER", nullable: false),
                    VidaEconomicaLlantas = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    VidaPiezasEspeciales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SalarioOperador = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FactorSalarioReal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    HorasEfectivasTurno = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CostoHorario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    EsCostoCalculado = table.Column<bool>(type: "INTEGER", nullable: false),
                    Origen = table.Column<int>(type: "INTEGER", nullable: false),
                    MaquinariaMaestraId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProyectoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notas = table.Column<string>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaCalculoCosto = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maquinaria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Maquinaria_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Maquinaria_ProyectoId_Clave",
                table: "Maquinaria",
                columns: new[] { "ProyectoId", "Clave" },
                unique: true);

            // ═══════════════════════════════════════════════════════════
            // TABLA: Matrices (APU y Básicos)
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "Matrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Clave = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    CostoDirecto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Origen = table.Column<int>(type: "INTEGER", nullable: false),
                    MatrizMaestraId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProyectoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notas = table.Column<string>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaUltimoCalculo = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matrices_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matrices_ProyectoId_Clave",
                table: "Matrices",
                columns: new[] { "ProyectoId", "Clave" },
                unique: true);

            // ═══════════════════════════════════════════════════════════
            // TABLA: ComponentesMatriz
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "ComponentesMatriz",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatrizId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoComponente = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: true),
                    ManoDeObraId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaquinariaId = table.Column<int>(type: "INTEGER", nullable: true),
                    AuxiliarId = table.Column<int>(type: "INTEGER", nullable: true),
                    Cantidad = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    Notas = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentesMatriz", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComponentesMatriz_Materiales_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materiales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComponentesMatriz_ManoDeObra_ManoDeObraId",
                        column: x => x.ManoDeObraId,
                        principalTable: "ManoDeObra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComponentesMatriz_Maquinaria_MaquinariaId",
                        column: x => x.MaquinariaId,
                        principalTable: "Maquinaria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComponentesMatriz_Matrices_AuxiliarId",
                        column: x => x.AuxiliarId,
                        principalTable: "Matrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComponentesMatriz_Matrices_MatrizId",
                        column: x => x.MatrizId,
                        principalTable: "Matrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesMatriz_MatrizId",
                table: "ComponentesMatriz",
                column: "MatrizId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesMatriz_MaterialId",
                table: "ComponentesMatriz",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesMatriz_ManoDeObraId",
                table: "ComponentesMatriz",
                column: "ManoDeObraId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesMatriz_MaquinariaId",
                table: "ComponentesMatriz",
                column: "MaquinariaId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesMatriz_AuxiliarId",
                table: "ComponentesMatriz",
                column: "AuxiliarId");

            // ═══════════════════════════════════════════════════════════
            // TABLA: ConceptosPresupuesto
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "ConceptosPresupuesto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProyectoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Clave = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Nivel = table.Column<int>(type: "INTEGER", nullable: false),
                    EsAgrupador = table.Column<bool>(type: "INTEGER", nullable: false),
                    PadreId = table.Column<int>(type: "INTEGER", nullable: true),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    MatrizId = table.Column<int>(type: "INTEGER", nullable: true),
                    CostoDirectoUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CostoDirectoTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Indirectos = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Financiamiento = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Utilidad = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CargosAdicionales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ColumnasPersonalizadasJSON = table.Column<string>(type: "TEXT", nullable: true),
                    Notas = table.Column<string>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptosPresupuesto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConceptosPresupuesto_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConceptosPresupuesto_ConceptosPresupuesto_PadreId",
                        column: x => x.PadreId,
                        principalTable: "ConceptosPresupuesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConceptosPresupuesto_Matrices_MatrizId",
                        column: x => x.MatrizId,
                        principalTable: "Matrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptosPresupuesto_ProyectoId_Orden",
                table: "ConceptosPresupuesto",
                columns: new[] { "ProyectoId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptosPresupuesto_PadreId",
                table: "ConceptosPresupuesto",
                column: "PadreId");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptosPresupuesto_MatrizId",
                table: "ConceptosPresupuesto",
                column: "MatrizId");

            // ═══════════════════════════════════════════════════════════
            // TABLA: CargosAdicionales
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "CargosAdicionales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProyectoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BaseCalculo = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoCargo = table.Column<int>(type: "INTEGER", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notas = table.Column<string>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargosAdicionales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CargosAdicionales_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CargosAdicionales_ProyectoId",
                table: "CargosAdicionales",
                column: "ProyectoId");

            // ═══════════════════════════════════════════════════════════
            // TABLA: ColumnasPersonalizadas
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "ColumnasPersonalizadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProyectoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NombreInterno = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TipoColumna = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoDato = table.Column<int>(type: "INTEGER", nullable: false),
                    Formula = table.Column<string>(type: "TEXT", nullable: true),
                    Visible = table.Column<bool>(type: "INTEGER", nullable: false),
                    Imprimible = table.Column<bool>(type: "INTEGER", nullable: false),
                    AnchoColumna = table.Column<int>(type: "INTEGER", nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    Totalizar = table.Column<bool>(type: "INTEGER", nullable: false),
                    CondicionTotalizacion = table.Column<string>(type: "TEXT", nullable: true),
                    FormatoNumerico = table.Column<string>(type: "TEXT", nullable: true),
                    FormatoFecha = table.Column<string>(type: "TEXT", nullable: true),
                    Alineacion = table.Column<int>(type: "INTEGER", nullable: false),
                    NombreFuente = table.Column<string>(type: "TEXT", nullable: true),
                    TamanoFuente = table.Column<int>(type: "INTEGER", nullable: false),
                    ColorFuente = table.Column<string>(type: "TEXT", nullable: true),
                    ColorFondo = table.Column<string>(type: "TEXT", nullable: true),
                    Negrita = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cursiva = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnasPersonalizadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColumnasPersonalizadas_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnasPersonalizadas_ProyectoId_NombreInterno",
                table: "ColumnasPersonalizadas",
                columns: new[] { "ProyectoId", "NombreInterno" },
                unique: true);

            // ═══════════════════════════════════════════════════════════
            // TABLA: VistasPresupuesto
            // ═══════════════════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "VistasPresupuesto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProyectoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: true),
                    EsVistaPorDefecto = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfiguracionColumnasJSON = table.Column<string>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VistasPresupuesto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VistasPresupuesto_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VistasPresupuesto_ProyectoId",
                table: "VistasPresupuesto",
                column: "ProyectoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VistasPresupuesto");
            migrationBuilder.DropTable(name: "ColumnasPersonalizadas");
            migrationBuilder.DropTable(name: "CargosAdicionales");
            migrationBuilder.DropTable(name: "ConceptosPresupuesto");
            migrationBuilder.DropTable(name: "ComponentesMatriz");
            migrationBuilder.DropTable(name: "Matrices");
            migrationBuilder.DropTable(name: "Maquinaria");
            migrationBuilder.DropTable(name: "ManoDeObra");
            migrationBuilder.DropTable(name: "Materiales");
            migrationBuilder.DropTable(name: "Proyectos");
        }
    }
}
