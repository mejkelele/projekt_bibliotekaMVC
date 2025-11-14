using Biblioteka.Models;
using Microsoft.EntityFrameworkCore;

namespace Biblioteka.Data;

public class BibliotekaContext : DbContext
{
    public BibliotekaContext(DbContextOptions<BibliotekaContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Ksiazka> Ksiazki { get; set; }
    // DODANE: Nowy DbSet dla Kategorii
    public DbSet<Kategoria> Kategorie { get; set; }
    public DbSet<Wypozyczenie> Wypozyczenia { get; set; }
    public DbSet<Rezerwacja> Rezerwacje { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Konfiguracja relacji drzewiastej dla modelu Kategoria
        modelBuilder.Entity<Kategoria>()
            .HasOne(k => k.Parent)              // Kategoria ma jednego Parent (rodzica)
            .WithMany(k => k.Children)          // Rodzic może mieć wiele Children (dzieci)
            .HasForeignKey(k => k.ParentId)     // Klucz obcy to ParentId
            .IsRequired(false)                  // ParentId jest opcjonalny (może być nullem)
            .OnDelete(DeleteBehavior.Restrict); // Zapobiega kaskadowemu usuwaniu (aby nie usuwać całej gałęzi przypadkiem)

        // Usuwanie starej kolumny 'dzial' z tabeli Ksiazki (jeśli używasz mapowania jawnego)
        // Jeśli używasz Code First, wystarczy, że usuniesz pole z modelu Ksiazka.
        modelBuilder.Entity<Wypozyczenie>()
            .HasOne(w => w.User)
            .WithMany() // Możesz dodać kolekcję Wypozyczen do modelu User w przyszłości
            .HasForeignKey(w => w.UserId);

        modelBuilder.Entity<Wypozyczenie>()
            .HasOne(w => w.Ksiazka)
            .WithMany() // Możesz dodać kolekcję Wypozyczen do modelu Ksiazka w przyszłości
            .HasForeignKey(w => w.KsiazkaId);

        modelBuilder.Entity<Rezerwacja>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId);

        modelBuilder.Entity<Rezerwacja>()
            .HasOne(r => r.Ksiazka)
            .WithMany()
            .HasForeignKey(r => r.KsiazkaId);



    }
}