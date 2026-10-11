using AddressBook.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AddressBook.Api.Data;

public class AddressBookContext(DbContextOptions<AddressBookContext> options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();
}
