using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGHR.Migrations
{
    /// <inheritdoc />
    public partial class FixPostgresTimeMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- CORREÇÕES PARA O MÉTODO UP() ---
            // Alterando colunas de 'interval' (TimeSpan) para 'timestamp with time zone' (DateTime)
            // USANDO UMA DATA BASE PARA A CONVERSÃO (CAST) EXPLÍCITA

            migrationBuilder.Sql(
                "ALTER TABLE \"Horarios\" ALTER COLUMN \"SaidaAlmoco\" TYPE timestamp with time zone USING (timestamp '0001-01-01 00:00:00' + \"SaidaAlmoco\");"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Horarios\" ALTER COLUMN \"HoraSaida\" TYPE timestamp with time zone USING (timestamp '0001-01-01 00:00:00' + \"HoraSaida\");"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Horarios\" ALTER COLUMN \"HoraEntrada\" TYPE timestamp with time zone USING (timestamp '0001-01-01 00:00:00' + \"HoraEntrada\");"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Horarios\" ALTER COLUMN \"EntradaAlmoco\" TYPE timestamp with time zone USING (timestamp '0001-01-01 00:00:00' + \"EntradaAlmoco\");"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Faltas\" ALTER COLUMN \"Inicio\" TYPE timestamp with time zone USING (timestamp '0001-01-01 00:00:00' + \"Inicio\");"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Faltas\" ALTER COLUMN \"Fim\" TYPE timestamp with time zone USING (timestamp '0001-01-01 00:00:00' + \"Fim\");"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // --- CORREÇÕES PARA O MÉTODO DOWN() ---
            // Revertendo colunas de 'timestamp with time zone' (DateTime) para 'interval' (TimeSpan)
            // A conversão inversa é mais direta, mas usamos CAST explícito para consistência.

            migrationBuilder.Sql(
                "ALTER TABLE \"Horarios\" ALTER COLUMN \"SaidaAlmoco\" TYPE interval USING \"SaidaAlmoco\"::time;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Horarios\" ALTER COLUMN \"HoraSaida\" TYPE interval USING \"HoraSaida\"::time;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Horarios\" ALTER COLUMN \"HoraEntrada\" TYPE interval USING \"HoraEntrada\"::time;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Horarios\" ALTER COLUMN \"EntradaAlmoco\" TYPE interval USING \"EntradaAlmoco\"::time;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Faltas\" ALTER COLUMN \"Inicio\" TYPE interval USING \"Inicio\"::time;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE \"Faltas\" ALTER COLUMN \"Fim\" TYPE interval USING \"Fim\"::time;"
            );
        }
    }
}