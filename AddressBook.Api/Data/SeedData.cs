using AddressBook.Api.Models;

namespace AddressBook.Api.Data;

/// <summary>
/// Sample contacts loaded on startup so the live Swagger page is never empty.
/// The data is in memory, so it comes back every time the app restarts.
/// </summary>
public static class SeedData
{
    public static void Initialize(AddressBookContext db)
    {
        if (db.Contacts.Any())
        {
            return;
        }

        Contact[] samples =
        [
            C("Alan", "Turing", "alan.turing@auning.com", "+1 555 123 4567", true, "Work"),
            C("Marie", "Curie", "marie.curie@mail.com", null, false, "Family"),
            C("Katherine", "Johnson", "katherine.johnson@abcc.com", "+1 555 987 6543", true, "Phone"),
            C("Charles", "Darwin", "charles.darwin@adl.gov", null, false, "Tag"),
            C("Ada", "Lovelace", "ada.lovelace@example.com", null, true, "Work", "Friends"),
            C("Grace", "Hopper", "grace.hopper@example.com", "+1 555 222 3333", false, "Work"),
            C("Nikola", "Tesla", "nikola.tesla@example.com", null, false, "Friends"),
            C("Albert", "Einstein", "albert.einstein@example.com", null, false, "Family"),
            C("Isaac", "Newton", "isaac.newton@example.com", null, false, "Work"),
            C("Rosalind", "Franklin", "rosalind.franklin@example.com", null, true, "Friends"),
            C("Cahit", "Arf", "cahit.arf@example.com", "+90 532 000 0001", false, "Work"),
            C("Aziz", "Sancar", "aziz.sancar@example.com", "+90 532 000 0002", true, "Work", "Family"),
            C("Gazi", "Yaşargil", "gazi.yasargil@example.com", null, false, "Doctor"),
            C("Canan", "Dağdeviren", "canan.dagdeviren@example.com", null, false, "Work"),
            C("İhsan", "Doğramacı", "ihsan.dogramaci@example.com", null, false, "Doctor"),
            C("Feryal", "Özel", "feryal.ozel@example.com", null, false, "Friends"),
            C("Linus", "Torvalds", "linus.torvalds@example.com", null, false, "Work"),
            C("Margaret", "Hamilton", "margaret.hamilton@example.com", null, true, "Work"),
            C("Tim", "Berners-Lee", "tim.bernerslee@example.com", null, false, "Friends"),
            C("Hedy", "Lamarr", "hedy.lamarr@example.com", null, false, "Family"),
            C("Richard", "Feynman", "richard.feynman@example.com", null, false, "Friends"),
            C("Dennis", "Ritchie", "dennis.ritchie@example.com", null, false, "Work"),
            C("Barbara", "Liskov", "barbara.liskov@example.com", null, false, "Work"),
            C("Oktay", "Sinanoğlu", "oktay.sinanoglu@example.com", null, false, "Family"),
        ];

        // Space the timestamps a minute apart so sorting by createdAt is meaningful.
        var start = DateTime.UtcNow.AddMinutes(-samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i].CreatedAt = samples[i].UpdatedAt = start.AddMinutes(i);
        }

        db.Contacts.AddRange(samples);
        db.SaveChanges();
    }

    private static Contact C(string firstName, string lastName, string? email, string? phone,
        bool isFavorite, params string[] tags) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Phone = phone,
        IsFavorite = isFavorite,
        Tags = [.. tags],
    };
}
