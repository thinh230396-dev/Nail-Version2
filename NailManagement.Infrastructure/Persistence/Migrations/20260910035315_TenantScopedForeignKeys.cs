using NailManagement.Domain.Salon.StaffMembers;

﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NailManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopedForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Branches_BranchId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Customers_CustomerId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Staff_StaffId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Appointments_AppointmentId",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Branches_BranchId",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Customers_CustomerId",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Staff_StaffId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_AppointmentId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_StaffId",
                table: "AppUsers");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Staff_Id_TenantId",
                table: "Staff",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Customers_Id_TenantId",
                table: "Customers",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Branches_Id_TenantId",
                table: "Branches",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Appointments_Id_TenantId",
                table: "Appointments",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_AppointmentId_TenantId",
                table: "SalesInvoices",
                columns: new[] { "AppointmentId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_BranchId_TenantId",
                table: "SalesInvoices",
                columns: new[] { "BranchId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CustomerId_TenantId",
                table: "SalesInvoices",
                columns: new[] { "CustomerId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_StaffId_TenantId",
                table: "SalesInvoices",
                columns: new[] { "StaffId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_StaffId",
                table: "AppUsers",
                column: "StaffId",
                unique: true,
                filter: "[StaffId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BranchId_TenantId",
                table: "Appointments",
                columns: new[] { "BranchId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId_TenantId",
                table: "Appointments",
                columns: new[] { "CustomerId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StaffId_TenantId",
                table: "Appointments",
                columns: new[] { "StaffId", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Branches_BranchId_TenantId",
                table: "Appointments",
                columns: new[] { "BranchId", "TenantId" },
                principalTable: "Branches",
                principalColumns: new[] { "Id", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Customers_CustomerId_TenantId",
                table: "Appointments",
                columns: new[] { "CustomerId", "TenantId" },
                principalTable: "Customers",
                principalColumns: new[] { "Id", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Staff_StaffId_TenantId",
                table: "Appointments",
                columns: new[] { "StaffId", "TenantId" },
                principalTable: "Staff",
                principalColumns: new[] { "Id", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Appointments_AppointmentId_TenantId",
                table: "SalesInvoices",
                columns: new[] { "AppointmentId", "TenantId" },
                principalTable: "Appointments",
                principalColumns: new[] { "Id", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Branches_BranchId_TenantId",
                table: "SalesInvoices",
                columns: new[] { "BranchId", "TenantId" },
                principalTable: "Branches",
                principalColumns: new[] { "Id", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Customers_CustomerId_TenantId",
                table: "SalesInvoices",
                columns: new[] { "CustomerId", "TenantId" },
                principalTable: "Customers",
                principalColumns: new[] { "Id", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Staff_StaffId_TenantId",
                table: "SalesInvoices",
                columns: new[] { "StaffId", "TenantId" },
                principalTable: "Staff",
                principalColumns: new[] { "Id", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Branches_BranchId_TenantId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Customers_CustomerId_TenantId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Staff_StaffId_TenantId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Appointments_AppointmentId_TenantId",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Branches_BranchId_TenantId",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Customers_CustomerId_TenantId",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Staff_StaffId_TenantId",
                table: "SalesInvoices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Staff_Id_TenantId",
                table: "Staff");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_AppointmentId_TenantId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_BranchId_TenantId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_CustomerId_TenantId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_StaffId_TenantId",
                table: "SalesInvoices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Customers_Id_TenantId",
                table: "Customers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Branches_Id_TenantId",
                table: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_StaffId",
                table: "AppUsers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Appointments_Id_TenantId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_BranchId_TenantId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerId_TenantId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_StaffId_TenantId",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_AppointmentId",
                table: "SalesInvoices",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_StaffId",
                table: "AppUsers",
                column: "StaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Branches_BranchId",
                table: "Appointments",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Customers_CustomerId",
                table: "Appointments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Staff_StaffId",
                table: "Appointments",
                column: "StaffId",
                principalTable: "Staff",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Appointments_AppointmentId",
                table: "SalesInvoices",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Branches_BranchId",
                table: "SalesInvoices",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Customers_CustomerId",
                table: "SalesInvoices",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Staff_StaffId",
                table: "SalesInvoices",
                column: "StaffId",
                principalTable: "Staff",
                principalColumn: "Id");
        }
    }
}
