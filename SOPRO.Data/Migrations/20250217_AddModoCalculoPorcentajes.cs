// =============================================================================
// LEGACY — Consolidado en SOPRO.Application.Services.SchemaManager
// Este archivo se conserva solo como referencia histórica.
// No invocar directamente. No agregar nueva lógica aquí.
// =============================================================================
using Microsoft.EntityFrameworkCore.Migrations;

namespace SOPRO.Data.Migrations
{
    [Obsolete("LEGACY: consolidado en SchemaManager. No invocar.", error: false)]
    public partial class AddModoCalculoPorcentajes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModoCalculoPorcentajes",
                table: "Proyectos",
                type: "TEXT",
                nullable: false,
                defaultValue: "Acumulables");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModoCalculoPorcentajes",
                table: "Proyectos");
        }
    }
}
