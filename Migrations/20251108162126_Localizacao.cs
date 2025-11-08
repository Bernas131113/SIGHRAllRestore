using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGHR.Migrations
{
    /// <inheritdoc />
    public partial class Localizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LatitudeEntrada",
                table: "Horarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LatitudeEntradaAlmoco",
                table: "Horarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LatitudeSaida",
                table: "Horarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LatitudeSaidaAlmoco",
                table: "Horarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudeEntrada",
                table: "Horarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudeEntradaAlmoco",
                table: "Horarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudeSaida",
                table: "Horarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudeSaidaAlmoco",
                table: "Horarios",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatitudeEntrada",
                table: "Horarios");

            migrationBuilder.DropColumn(
                name: "LatitudeEntradaAlmoco",
                table: "Horarios");

            migrationBuilder.DropColumn(
                name: "LatitudeSaida",
                table: "Horarios");

            migrationBuilder.DropColumn(
                name: "LatitudeSaidaAlmoco",
                table: "Horarios");

            migrationBuilder.DropColumn(
                name: "LongitudeEntrada",
                table: "Horarios");

            migrationBuilder.DropColumn(
                name: "LongitudeEntradaAlmoco",
                table: "Horarios");

            migrationBuilder.DropColumn(
                name: "LongitudeSaida",
                table: "Horarios");

            migrationBuilder.DropColumn(
                name: "LongitudeSaidaAlmoco",
                table: "Horarios");
        }
    }
}
