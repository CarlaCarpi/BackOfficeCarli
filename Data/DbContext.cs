using Microsoft.EntityFrameworkCore;
using SantaRamona.Backoffice.Models;
using System;
using System.Collections.Generic;

namespace SantaRamona.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Raza> Raza { get; set; }
        public DbSet<Especie> Especie { get; set; }
        public DbSet<Estado_Animal> Estado_Animal { get; set; }
        public DbSet<Estado_Usuario> Estado_Usuario { get; set; }
        public DbSet<Estado_Persona> Estado_Persona { get; set; }
        //public DbSet<Estado_Formulario> Estado_Formulario { get; set; }
        public DbSet<Estado_Pension> Estado_Pension { get; set; }
        //public DbSet<Tipo_Telefono> Tipo_Telefono { get; set; }
        public DbSet<Tipo_Formulario> Tipo_Formulario { get; set; }
        //public DbSet<Permiso> Permiso { get; set; }
        public DbSet<Rol> Rol { get; set; }
        public DbSet<Provincia> Provincia { get; set; }
        public DbSet<Localidad> Localidad { get; set; }
        public DbSet<Tamano> Tamano { get; set; }
        public DbSet<Animal> Animal { get; set; }
        //public DbSet<Historial_Medico> Historial_Medico { get; set; }
        public DbSet<Persona> Persona { get; set; }
        public DbSet<Usuario> Usuario { get; set; }
        public DbSet<Pension> Pension { get; set; }
        //public DbSet<Seguimiento> Seguimiento { get; set; }
        public DbSet<Formulario> Formulario { get; set; }
        public DbSet<Pregunta> Pregunta { get; set; }
        public DbSet<Respuesta> Respuesta { get; set; }
        public DbSet<Donacion> Donacion { get; set; }


    }
}