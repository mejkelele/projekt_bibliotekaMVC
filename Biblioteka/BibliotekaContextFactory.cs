using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Biblioteka.Data
{
    public class BibliotekaContextFactory : IDesignTimeDbContextFactory<BibliotekaContext>
    {
        public BibliotekaContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<BibliotekaContext>();
            optionsBuilder.UseSqlite("Data Source=Biblioteka.db");
            return new BibliotekaContext(optionsBuilder.Options);
        }
    }
}
