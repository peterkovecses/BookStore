namespace BookStore.Catalog.Infrastructure.Seeding;

internal sealed class CatalogDbContextInitialiser(
    CatalogDbContext context,
    ILogger<CatalogDbContextInitialiser> logger)
{
    public async Task InitialiseAsync()
    {
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogInitialisationError(ex);
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            logger.LogSeedingError(ex);
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        if (await context.Books.AnyAsync())
        {
            return;
        }

        var tags = GetTags();
        var authors = GetAuthors();
        var books = GetBooks(tags, authors);

        context.Tags.AddRange(tags.Values);
        context.Authors.AddRange(authors.Values);
        context.Books.AddRange(books.Values);

        await context.SaveChangesAsync();
    }

    private static Dictionary<string, Tag> GetTags() => new()
    {
        ["Software Development"] = new() { Id = "Software Development" },
        ["Mystery"] = new() { Id = "Mystery" },
        ["Thriller"] = new() { Id = "Thriller" },
        ["Science Fiction"] = new() { Id = "Science Fiction" },
        ["Classic Literature"] = new() { Id = "Classic Literature" },
        ["Historical"] = new() { Id = "Historical" },
        ["Dystopian"] = new() { Id = "Dystopian" },
        ["Fantasy"] = new() { Id = "Fantasy" },
        ["Magic"] = new() { Id = "Magic" },
        ["Cooking"] = new() { Id = "Cooking" },
        ["Politics"] = new() { Id = "Politics" },
        ["Lifestyle"] = new() { Id = "Lifestyle" }
    };

    private static Dictionary<int, Author> GetAuthors() => new()
    {
        [1] = new() { Name = "Robert C. Martin" },
        [2] = new() { Name = "Andrew Hunt" },
        [3] = new() { Name = "David Thomas" },
        [4] = new() { Name = "Martin Fowler" },
        [5] = new() { Name = "Kent Beck" },
        [6] = new() { Name = "Stieg Larsson" },
        [7] = new() { Name = "Frank Herbert" },
        [8] = new() { Name = "Brian Herbert" },
        [9] = new() { Name = "Erich Gamma" },
        [10] = new() { Name = "Richard Helm" },
        [11] = new() { Name = "Ralph Johnson" },
        [12] = new() { Name = "John Vlissides" },
        [13] = new() { Name = "Frederick P. Brooks Jr." },
        [14] = new() { Name = "Harper Lee" },
        [15] = new() { Name = "Ken Follett" },
        [16] = new() { Name = "George Orwell" },
        [17] = new() { Name = "J.K. Rowling" },
        [18] = new() { Name = "George R.R. Martin" },
        [19] = new() { Name = "J.R.R. Tolkien" },
        [20] = new() { Name = "Timothy Zahn" },
        [21] = new() { Name = "Isaac Asimov" },
        [22] = new() { Name = "Julia Child" },
        [23] = new() { Name = "Irma S. Rombauer" },
        [24] = new() { Name = "Samin Nosrat" },
        [25] = new() { Name = "The Culinary Institute of America" },
        [26] = new() { Name = "Marcella Hazan" },
        [30] = new() { Name = "Barack Obama" },
        [31] = new() { Name = "James Clear" },
        [32] = new() { Name = "Mark Manson" },
        [33] = new() { Name = "H\u00E9ctor Garc\u00EDa" },
        [34] = new() { Name = "Francesc Miralles" },
        [35] = new() { Name = "Dan Buettner" },
        [36] = new() { Name = "Robin Sharma" }
    };

    private static Dictionary<int, Book> GetBooks(Dictionary<string, Tag> tags, Dictionary<int, Author> authors)
    {
        var books = new Dictionary<int, Book>
        {
            [1] = new()
            {
                Title = "Clean Code",
                PublishedOn = new DateTime(2008, 8, 11),
                Publisher = "Prentice Hall",
                Price = 30.99m,
                Description = "Noted software expert Robert C. Martin, presents a revolutionary paradigm with Clean Code: A Handbook of Agile Software Craftsmanship. Martin, who has helped bring agile principles from a practitioner\u2019s point of view to tens of thousands of programmers, has teamed up with his colleagues from Object Mentor to distill their best agile practice of cleaning code \u201Con the fly\u201D into a book that will instill within you the values of software craftsman, and make you a better programmer\u2015but only if you work at it.",
                ImageName = "clean-code.jpeg" },
            [2] = new()
            {
                Title = "The Pragmatic Programmer",
                PublishedOn = new DateTime(1999, 10, 30),
                Publisher = "Addison-Wesley",
                Price = 45.00m,
                Description = "The Pragmatic Programmer is one of those rare books that you\u2019ll read, re-read, and read again over the years. Whether you\u2019re new to the field or an experienced practitioner, you\u2019ll come away with fresh insights and guidance.",
                ImageName = "pragmatic-programmer.jpeg" },
            [3] = new()
            {
                Title = "Refactoring: Improving the Design of Existing Code",
                PublishedOn = new DateTime(1999, 7, 8),
                Publisher = "Addison-Wesley",
                Price = 50.00m,
                Description = "Refactoring is the process of changing a software system in such a way that it does not alter the external behavior of the code yet improves its internal structure. This book explains how to refactor code.",
                ImageName = "refactoring.jpeg" },
            [4] = new()
            {
                Title = "The Girl with the Dragon Tattoo",
                PublishedOn = new DateTime(2005, 8, 1),
                Publisher = "Norstedts F\u00F6rlag",
                Price = 35.99m,
                Description = "A spellbinding amalgam of murder mystery, family saga, love story, and financial intrigue.",
                ImageName = "dragon-tattoo.jpeg" },
            [5] = new()
            {
                Title = "Dune",
                PublishedOn = new DateTime(1965, 8, 1),
                Publisher = "Chilton Books",
                Price = 40.00m,
                Description = "Set on the desert planet Arrakis, Dune is the story of the boy Paul Atreides, who would become the mysterious man known as Muad'Dib.",
                ImageName = "dune.jpeg" },
            [6] = new()
            {
                Title = "Design Patterns: Elements of Reusable Object-Oriented Software",
                PublishedOn = new DateTime(1994, 10, 31),
                Publisher = "Addison-Wesley",
                Price = 55.00m,
                Description = "Capturing a wealth of experience about the design of object-oriented software, Design Patterns condenses the most effective solutions to recurring design problems into a guidebook that will help developers solve common challenges.",
                ImageName = "design-patterns.jpeg" },
            [7] = new()
            {
                Title = "The Mythical Man-Month: Essays on Software Engineering",
                PublishedOn = new DateTime(1975, 1, 1),
                Publisher = "Addison-Wesley",
                Price = 39.99m,
                Description = "Few books on software project management have been as influential and timeless as The Mythical Man-Month. This collection of essays on software engineering and project management is based on Brooks' experiences as project manager for the development of IBM's System/360 family of computers.",
                ImageName = "mythical-man-month.jpeg" },
            [8] = new()
            {
                Title = "To Kill a Mockingbird",
                PublishedOn = new DateTime(1960, 7, 11),
                Publisher = "J.B. Lippincott & Co.",
                Price = 24.99m,
                Description = "A gripping, heart-wrenching, and wholly remarkable tale of coming-of-age in a South poisoned by virulent prejudice, it views a world of great beauty and savage inequities through the eyes of a young girl.",
                ImageName = "to-kill-a-mockingbird.jpeg" },
            [9] = new()
            {
                Title = "The Pillars of the Earth",
                PublishedOn = new DateTime(1989, 10, 23),
                Publisher = "Penguin",
                Price = 29.99m,
                Description = "Ken Follett\u2019s historical masterpiece about the construction of a cathedral in the fictional town of Kingsbridge, set in 12th century England.",
                ImageName = "pillars-of-the-earth.jpeg" },
            [10] = new()
            {
                Title = "1984",
                PublishedOn = new DateTime(1949, 6, 8),
                Publisher = "Secker & Warburg",
                Price = 19.99m,
                Description = "A dystopian social science fiction novel and cautionary tale about the dangers of totalitarianism.",
                ImageName = "1984.jpg" },
            [11] = new()
            {
                Title = "Harry Potter and the Philosopher's Stone",
                PublishedOn = new DateTime(1997, 6, 26),
                Publisher = "Bloomsbury",
                Price = 14.99m,
                Description = "The first book in the Harry Potter series, introducing Harry and his adventures at Hogwarts.",
                ImageName = "philosophers_stone.jpg" },
            [12] = new()
            {
                Title = "Harry Potter and the Chamber of Secrets",
                PublishedOn = new DateTime(1998, 7, 2),
                Publisher = "Bloomsbury",
                Price = 15.99m,
                Description = "Harry returns to Hogwarts and uncovers the secrets of the Chamber of Secrets.",
                ImageName = "chamber_of_secrets.jpg" },
            [13] = new()
            {
                Title = "Harry Potter and the Prisoner of Azkaban",
                PublishedOn = new DateTime(1999, 7, 8),
                Publisher = "Bloomsbury",
                Price = 16.99m,
                Description = "Harry learns about Sirius Black and his connection to Harry's past.",
                ImageName = "prisoner_of_azkaban.jpg" },
            [14] = new()
            {
                Title = "Harry Potter and the Goblet of Fire",
                PublishedOn = new DateTime(2000, 7, 8),
                Publisher = "Bloomsbury",
                Price = 18.99m,
                Description = "Harry competes in the Triwizard Tournament and faces new challenges.",
                ImageName = "goblet_of_fire.jpg" },
            [15] = new()
            {
                Title = "Harry Potter Paperback Box Set (Books 1-3)",
                PublishedOn = new DateTime(2023, 7, 15),
                Publisher = "Bloomsbury",
                Price = 39.99m,
                Description = "A special box set containing the first three Harry Potter books in paperback edition.",
                ImageName = "paperback_box_set.jpg"
            },
            [16] = new()
            {
                Title = "A Game of Thrones",
                PublishedOn = new DateTime(1996, 8, 6),
                Publisher = "Bantam Books",
                Price = 19.99m,
                Description = "The first book in the epic fantasy series A Song of Ice and Fire by George R.R. Martin.",
                ImageName = "game_of_thrones.jpg" },
            [17] = new()
            {
                Title = "A Clash of Kings",
                PublishedOn = new DateTime(1998, 11, 16),
                Publisher = "Bantam Books",
                Price = 21.99m,
                Description = "The second book in the series, continuing the political and military conflicts in Westeros.",
                ImageName = "clash_of_kings.jpg" },
            [18] = new()
            {
                Title = "A Storm of Swords",
                PublishedOn = new DateTime(2000, 8, 8),
                Publisher = "Bantam Books",
                Price = 24.99m,
                Description = "The third book in the series, featuring epic battles and shocking betrayals.",
                ImageName = "storm_of_swords.jpg" },
            [19] = new()
            {
                Title = "The Fellowship of the Ring",
                PublishedOn = new DateTime(1954, 7, 29),
                Publisher = "George Allen & Unwin",
                Price = 14.99m,
                Description = "The first book in The Lord of the Rings trilogy, introducing the epic quest to destroy the One Ring.",
                ImageName = "fellowship_of_the_ring.jpg" },
            [20] = new()
            {
                Title = "The Two Towers",
                PublishedOn = new DateTime(1954, 11, 11),
                Publisher = "George Allen & Unwin",
                Price = 16.99m,
                Description = "The second book in the trilogy, chronicling the journey of Frodo and the battle against Sauron's forces.",
                ImageName = "the_two_towers.jpg" },
            [21] = new()
            {
                Title = "The Return of the King",
                PublishedOn = new DateTime(1955, 10, 20),
                Publisher = "George Allen & Unwin",
                Price = 18.99m,
                Description = "The final book in the trilogy, depicting the climactic battle for Middle-earth and the destruction of the One Ring.",
                ImageName = "return_of_the_king.jpg" },
            [22] = new()
            {
                Title = "The Lord of the Rings 3-Book Paperback Box Set",
                PublishedOn = new DateTime(2020, 10, 15),
                Publisher = "Mariner Books",
                Price = 39.99m,
                Description = "A beautifully designed box set containing all three books in The Lord of the Rings trilogy by J.R.R. Tolkien.",
                ImageName = "lotr_box_set.jpg" },
            [23] = new()
            {
                Title = "Star Wars: Heir to the Empire",
                PublishedOn = new DateTime(1991, 5, 1),
                Publisher = "Bantam Spectra",
                Price = 12.99m,
                Description = "The first book in Timothy Zahn's epic Thrawn Trilogy, set in the Star Wars Expanded Universe.",
                ImageName = "heir_to_the_empire.jpg" },
            [24] = new()
            {
                Title = "Star Wars: Dark Force Rising",
                PublishedOn = new DateTime(1992, 5, 1),
                Publisher = "Bantam Spectra",
                Price = 14.99m,
                Description = "The second book in Timothy Zahn's Thrawn Trilogy, continuing the battle for the galaxy.",
                ImageName = "dark_force_rising.jpg" },
            [25] = new()
            {
                Title = "Star Wars: The Last Command",
                PublishedOn = new DateTime(1993, 4, 1),
                Publisher = "Bantam Spectra",
                Price = 16.99m,
                Description = "The thrilling conclusion to Timothy Zahn's Thrawn Trilogy, with the fate of the galaxy hanging in the balance.",
                ImageName = "the_last_command.jpg" },
            [26] = new()
            {
                Title = "The Thrawn Trilogy Boxed Set: Star Wars Legends",
                PublishedOn = new DateTime(2022, 5, 31),
                Publisher = "Del Rey",
                Price = 39.99m,
                Description = "A stunning paperback boxed set of the Thrawn Trilogy by Timothy Zahn, part of the Star Wars Legends universe.",
                ImageName = "thrawn_trilogy_boxed_set.jpg" },
            [27] = new()
            {
                Title = "Foundation",
                PublishedOn = new DateTime(1951, 6, 1),
                Publisher = "Gnome Press",
                Price = 15.99m,
                Description = "The first book in Asimov's epic Foundation series, exploring the rise and fall of a galactic empire.",
                ImageName = "foundation.jpg" },
            [28] = new()
            {
                Title = "Foundation and Empire",
                PublishedOn = new DateTime(1952, 10, 1),
                Publisher = "Gnome Press",
                Price = 16.99m,
                Description = "The second book in the Foundation series, chronicling the struggle between the Foundation and the remnants of the Empire.",
                ImageName = "foundation_and_empire.jpg" },
            [29] = new()
            {
                Title = "Second Foundation",
                PublishedOn = new DateTime(1953, 1, 1),
                Publisher = "Gnome Press",
                Price = 17.99m,
                Description = "The third book in the Foundation series, revealing the secretive Second Foundation and its role in shaping galactic history.",
                ImageName = "second_foundation.jpg" },
            [30] = new()
            {
                Title = "I, Robot",
                PublishedOn = new DateTime(1950, 12, 2),
                Publisher = "Gnome Press",
                Price = 14.99m,
                Description = "A collection of interconnected short stories exploring the Three Laws of Robotics.",
                ImageName = "i_robot.jpg" },
            [31] = new()
            {
                Title = "The Gods Themselves",
                PublishedOn = new DateTime(1972, 3, 1),
                Publisher = "Doubleday",
                Price = 18.99m,
                Description = "A Hugo and Nebula Award-winning novel about humanity's discovery of an alien energy source and its implications.",
                ImageName = "the_gods_themselves.jpg" },
            [32] = new()
            {
                Title = "Mastering the Art of French Cooking",
                PublishedOn = new DateTime(1961, 8, 15),
                Publisher = "Knopf",
                Price = 29.99m,
                Description = "A classic French cookbook by Julia Child, offering timeless recipes and techniques.",
                ImageName = "mastering_french_cooking.jpg" },
            [33] = new()
            {
                Title = "The Joy of Cooking",
                PublishedOn = new DateTime(1931, 11, 1),
                Publisher = "Scribner",
                Price = 24.99m,
                Description = "An American classic by Irma S. Rombauer, featuring recipes for every occasion.",
                ImageName = "joy_of_cooking.jpg" },
            [34] = new()
            {
                Title = "Salt, Fat, Acid, Heat",
                PublishedOn = new DateTime(2017, 4, 25),
                Publisher = "Simon & Schuster",
                Price = 32.99m,
                Description = "Samin Nosrat's groundbreaking guide to mastering the four elements of cooking.",
                ImageName = "salt_fat_acid_heat.jpg" },
            [35] = new()
            {
                Title = "The Professional Chef",
                PublishedOn = new DateTime(2011, 10, 18),
                Publisher = "Wiley",
                Price = 49.99m,
                Description = "The Culinary Institute of America's definitive guide for chefs and culinary professionals.",
                ImageName = "professional_chef.jpg" },
            [36] = new()
            {
                Title = "Essentials of Classic Italian Cooking",
                PublishedOn = new DateTime(1992, 10, 27),
                Publisher = "Knopf",
                Price = 27.99m,
                Description = "Marcella Hazan's definitive guide to authentic Italian cuisine.",
                ImageName = "italian_cooking.jpg" },
            [40] = new()
            {
                Title = "A Promised Land",
                PublishedOn = new DateTime(2020, 11, 17),
                Publisher = "Crown",
                Price = 39.99m,
                Description = "Barack Obama's memoir covering his early political career and presidency.",
                ImageName = "promised_land.jpeg" },
            [41] = new()
            {
                Title = "The Audacity of Hope",
                PublishedOn = new DateTime(2006, 10, 17),
                Publisher = "Crown",
                Price = 24.99m,
                Description = "Barack Obama's vision for the future of America, written before his presidency.",
                ImageName = "audacity_of_hope.jpg" },
            [42] = new()
            {
                Title = "Atomic Habits",
                PublishedOn = new DateTime(2018, 10, 16),
                Publisher = "Avery",
                Price = 22.99m,
                Description = "An easy and proven way to build good habits and break bad ones by James Clear.",
                ImageName = "atomic_habits.jpg" },
            [43] = new()
            {
                Title = "The Subtle Art of Not Giving a F*ck",
                PublishedOn = new DateTime(2016, 9, 13),
                Publisher = "HarperOne",
                Price = 18.99m,
                Description = "A counterintuitive approach to living a good life by Mark Manson.",
                ImageName = "subtle_art.jpg" },
            [44] = new()
            {
                Title = "Ikigai: The Japanese Secret to a Long and Happy Life",
                PublishedOn = new DateTime(2017, 8, 29),
                Publisher = "Penguin Books",
                Price = 16.99m,
                Description = "A book by H\u00E9ctor Garc\u00EDa and Francesc Miralles exploring the Japanese concept of finding your purpose.",
                ImageName = "ikigai.jpg" },
            [45] = new()
            {
                Title = "The Blue Zones Solution",
                PublishedOn = new DateTime(2015, 4, 7),
                Publisher = "National Geographic",
                Price = 21.99m,
                Description = "Dan Buettner shares secrets of the world's healthiest and happiest communities.",
                ImageName = "blue_zones.jpg" },
            [46] = new()
            {
                Title = "The 5 AM Club",
                PublishedOn = new DateTime(2018, 12, 4),
                Publisher = "HarperCollins",
                Price = 19.99m,
                Description = "Robin Sharma's guide to transforming your mornings and your life.",
                ImageName = "5am_club.jpg" }
        };

        // Tags Many-to-Many
        books[1].Tags.Add(tags["Software Development"]);
        books[2].Tags.Add(tags["Software Development"]);
        books[3].Tags.Add(tags["Software Development"]);
        books[4].Tags.Add(tags["Mystery"]);
        books[4].Tags.Add(tags["Thriller"]);
        books[5].Tags.Add(tags["Science Fiction"]);
        books[6].Tags.Add(tags["Software Development"]);
        books[7].Tags.Add(tags["Software Development"]);
        books[8].Tags.Add(tags["Classic Literature"]);
        books[9].Tags.Add(tags["Historical"]);
        books[10].Tags.Add(tags["Dystopian"]);
        books[11].Tags.Add(tags["Fantasy"]);
        books[11].Tags.Add(tags["Magic"]);
        books[12].Tags.Add(tags["Fantasy"]);
        books[12].Tags.Add(tags["Magic"]);
        books[13].Tags.Add(tags["Fantasy"]);
        books[14].Tags.Add(tags["Fantasy"]);
        books[15].Tags.Add(tags["Fantasy"]);
        books[15].Tags.Add(tags["Magic"]);
        books[16].Tags.Add(tags["Fantasy"]);
        books[17].Tags.Add(tags["Fantasy"]);
        books[18].Tags.Add(tags["Fantasy"]);
        books[19].Tags.Add(tags["Fantasy"]);
        books[20].Tags.Add(tags["Fantasy"]);
        books[21].Tags.Add(tags["Fantasy"]);
        books[22].Tags.Add(tags["Fantasy"]);
        books[23].Tags.Add(tags["Science Fiction"]);
        books[24].Tags.Add(tags["Science Fiction"]);
        books[25].Tags.Add(tags["Science Fiction"]);
        books[26].Tags.Add(tags["Science Fiction"]);
        books[27].Tags.Add(tags["Science Fiction"]);
        books[28].Tags.Add(tags["Science Fiction"]);
        books[29].Tags.Add(tags["Science Fiction"]);
        books[30].Tags.Add(tags["Science Fiction"]);
        books[31].Tags.Add(tags["Science Fiction"]);
        books[32].Tags.Add(tags["Cooking"]);
        books[33].Tags.Add(tags["Cooking"]);
        books[34].Tags.Add(tags["Cooking"]);
        books[35].Tags.Add(tags["Cooking"]);
        books[36].Tags.Add(tags["Cooking"]);
        books[40].Tags.Add(tags["Politics"]);
        books[41].Tags.Add(tags["Politics"]);
        books[42].Tags.Add(tags["Lifestyle"]);
        books[43].Tags.Add(tags["Lifestyle"]);
        books[44].Tags.Add(tags["Lifestyle"]);
        books[45].Tags.Add(tags["Lifestyle"]);
        books[46].Tags.Add(tags["Lifestyle"]);

        // Authors Many-to-Many
        AddAuthor(books[1], authors[1], 0);
        AddAuthor(books[2], authors[2], 0);
        AddAuthor(books[2], authors[3], 1);
        AddAuthor(books[3], authors[4], 0);
        AddAuthor(books[3], authors[5], 1);
        AddAuthor(books[4], authors[6], 0);
        AddAuthor(books[5], authors[7], 0);
        AddAuthor(books[5], authors[8], 1);
        AddAuthor(books[6], authors[9], 0);
        AddAuthor(books[6], authors[10], 1);
        AddAuthor(books[6], authors[11], 2);
        AddAuthor(books[6], authors[12], 3);
        AddAuthor(books[7], authors[13], 0);
        AddAuthor(books[8], authors[14], 0);
        AddAuthor(books[9], authors[15], 0);
        AddAuthor(books[10], authors[16], 0);
        AddAuthor(books[11], authors[17], 0);
        AddAuthor(books[12], authors[17], 0);
        AddAuthor(books[13], authors[17], 0);
        AddAuthor(books[14], authors[17], 0);
        AddAuthor(books[15], authors[17], 0);
        AddAuthor(books[16], authors[18], 0);
        AddAuthor(books[17], authors[18], 0);
        AddAuthor(books[18], authors[18], 0);
        AddAuthor(books[19], authors[19], 0);
        AddAuthor(books[20], authors[19], 0);
        AddAuthor(books[21], authors[19], 0);
        AddAuthor(books[22], authors[19], 0);
        AddAuthor(books[23], authors[20], 0);
        AddAuthor(books[24], authors[20], 0);
        AddAuthor(books[25], authors[20], 0);
        AddAuthor(books[26], authors[20], 0);
        AddAuthor(books[27], authors[21], 0);
        AddAuthor(books[28], authors[21], 0);
        AddAuthor(books[29], authors[21], 0);
        AddAuthor(books[30], authors[21], 0);
        AddAuthor(books[31], authors[21], 0);
        AddAuthor(books[32], authors[22], 0);
        AddAuthor(books[33], authors[23], 0);
        AddAuthor(books[34], authors[24], 0);
        AddAuthor(books[35], authors[25], 0);
        AddAuthor(books[36], authors[26], 0);
        AddAuthor(books[40], authors[30], 0);
        AddAuthor(books[41], authors[30], 0);
        AddAuthor(books[42], authors[31], 0);
        AddAuthor(books[43], authors[32], 0);
        AddAuthor(books[44], authors[33], 0);
        AddAuthor(books[44], authors[34], 1);
        AddAuthor(books[45], authors[35], 0);
        AddAuthor(books[46], authors[36], 0);

        // Reviews
        books[1].Reviews.Add(new Review { Voter = "John Doe", Stars = 5, Comment = "Excellent book!" });
        books[1].Reviews.Add(new Review { Voter = "Jane Smith", Stars = 4, Comment = "Very informative." });
        books[2].Reviews.Add(new Review { Voter = "Alice Johnson", Stars = 5, Comment = "A must-read for any programmer!" });
        books[2].Reviews.Add(new Review { Voter = "Bob Brown", Stars = 4, Comment = "Great practical advice." });
        books[3].Reviews.Add(new Review { Voter = "Sarah Green", Stars = 5, Comment = "A must-have for developers working with legacy code." });
        books[3].Reviews.Add(new Review { Voter = "Tom White", Stars = 4 });
        books[4].Reviews.Add(new Review { Voter = "Michael Lee", Stars = 5, Comment = "Incredibly thrilling! Couldn't put it down." });
        books[4].Reviews.Add(new Review { Voter = "Laura King", Stars = 4, Comment = "Dark, but captivating." });
        books[4].Reviews.Add(new Review { Voter = "James Bond", Stars = 5, Comment = "One of the best crime novels I've ever read." });
        books[5].Reviews.Add(new Review { Voter = "Emma Stone", Stars = 5, Comment = "An epic masterpiece." });
        books[5].Reviews.Add(new Review { Voter = "Chris Evans", Stars = 4, Comment = "A rich and complex universe." });
        books[6].Reviews.Add(new Review { Voter = "Chris Redfield", Stars = 5, Comment = "Incredible book on software design patterns!" });
        books[6].Reviews.Add(new Review { Voter = "Jill Valentine", Stars = 4, Comment = "A bit dense but highly valuable." });
        books[7].Reviews.Add(new Review { Voter = "Rebecca Chambers", Stars = 4, Comment = "Insightful and still relevant today!" });
        books[7].Reviews.Add(new Review { Voter = "Leon Kennedy", Stars = 3, Comment = "A bit outdated, but has valuable lessons." });
        books[8].Reviews.Add(new Review { Voter = "Atticus Finch", Stars = 5, Comment = "A powerful portrayal of moral integrity." });
        books[8].Reviews.Add(new Review { Voter = "Scout Finch", Stars = 5, Comment = "An unforgettable story." });
        books[8].Reviews.Add(new Review { Voter = "Tom Robinson", Stars = 4, Comment = "A masterpiece of American literature." });
        books[8].Reviews.Add(new Review { Voter = "Jem Finch", Stars = 5, Comment = "Timeless and relevant." });
        books[8].Reviews.Add(new Review { Voter = "Calpurnia", Stars = 5, Comment = "A must-read for everyone." });
        books[10].Reviews.Add(new Review { Voter = "James Bond", Stars = 5, Comment = "A timeless and thought-provoking novel that remains relevant in the modern era." });
        books[11].Reviews.Add(new Review { Voter = "Alice Johnson", Stars = 5, Comment = "A magical start to an unforgettable series!" });
        books[12].Reviews.Add(new Review { Voter = "Bob Smith", Stars = 4, Comment = "Loved the twists in the Chamber of Secrets!" });
        books[13].Reviews.Add(new Review { Voter = "Charlie Brown", Stars = 5, Comment = "Prisoner of Azkaban is a masterpiece!" });
        books[15].Reviews.Add(new Review { Voter = "Emma Watson", Stars = 5, Comment = "A perfect collection for any Harry Potter fan! The new editions are stunning." });
        books[15].Reviews.Add(new Review { Voter = "Daniel Radcliffe", Stars = 4, Comment = "Great value for the first three books. A must-have for collectors." });
        books[16].Reviews.Add(new Review { Voter = "Jon Snow", Stars = 5, Comment = "An incredible start to the series. The depth of characters is unmatched." });
        books[17].Reviews.Add(new Review { Voter = "Daenerys Targaryen", Stars = 4, Comment = "A gripping continuation, though some parts felt a bit slow." });
        books[18].Reviews.Add(new Review { Voter = "Tyrion Lannister", Stars = 5, Comment = "A Storm of Swords kept me on the edge of my seat. Amazing!" });
        books[18].Reviews.Add(new Review { Voter = "Arya Stark", Stars = 5, Comment = "One of the most thrilling books I\u2019ve ever read." });
        books[19].Reviews.Add(new Review { Voter = "Frodo Baggins", Stars = 5, Comment = "An amazing start to a timeless epic. Every page draws you into Middle-earth." });
        books[20].Reviews.Add(new Review { Voter = "Samwise Gamgee", Stars = 4, Comment = "An incredible continuation of the journey, though the pacing slows at times." });
        books[21].Reviews.Add(new Review { Voter = "Aragorn", Stars = 5, Comment = "A breathtaking finale to the series. Tolkien's masterpiece." });
        books[23].Reviews.Add(new Review { Voter = "Luke Skywalker", Stars = 5, Comment = "An amazing continuation of the Star Wars saga. Thrawn is a phenomenal character!" });
        books[24].Reviews.Add(new Review { Voter = "Han Solo", Stars = 4, Comment = "Great book, but I expected a bit more action in the middle." });
        books[25].Reviews.Add(new Review { Voter = "Leia Organa", Stars = 5, Comment = "A stunning conclusion to an epic trilogy. Highly recommended for Star Wars fans!" });
        books[26].Reviews.Add(new Review { Voter = "Yoda", Stars = 5, Comment = "Read this box set, you must. Enlightened about the Star Wars universe, you will be." });
        books[26].Reviews.Add(new Review { Voter = "Admiral Thrawn", Stars = 5, Comment = "An excellent depiction of strategy and leadership in the galaxy. A masterpiece." });
        books[26].Reviews.Add(new Review { Voter = "Mara Jade", Stars = 4, Comment = "The set is great, but I prefer the individual books for their unique covers." });
        books[26].Reviews.Add(new Review { Voter = "Darth Vader", Stars = 4, Comment = "Impressive. Most impressive. But it could use some hardcovers." });
        books[26].Reviews.Add(new Review { Voter = "Chewbacca", Stars = 5, Comment = "RRRRAWWRRR! *translation: Amazing collection!*" });
        books[26].Reviews.Add(new Review { Voter = "Han Solo", Stars = 3, Comment = "Not bad, but I\u2019ve seen better box sets." });
        books[26].Reviews.Add(new Review { Voter = "Leia Organa", Stars = 4, Comment = "Beautifully designed, but I prefer hardcover editions." });
        books[26].Reviews.Add(new Review { Voter = "Lando Calrissian", Stars = 4, Comment = "Stylish packaging. Fits perfectly in my library." });
        books[26].Reviews.Add(new Review { Voter = "Obi-Wan Kenobi", Stars = 5, Comment = "This box set is the chosen one. It brings balance to the library." });
        books[26].Reviews.Add(new Review { Voter = "Boba Fett", Stars = 4, Comment = "A solid collection, though I'd like an exclusive design for bounty hunters." });
        books[26].Reviews.Add(new Review { Voter = "Emperor Palpatine", Stars = 5, Comment = "Good. Gooood. Add this to your library, you should." });
        books[26].Reviews.Add(new Review { Voter = "Ahsoka Tano", Stars = 5, Comment = "A must-have for any Star Wars fan. Timeless and engaging." });
        books[26].Reviews.Add(new Review { Voter = "Jar Jar Binks", Stars = 3, Comment = "Meesa think it's okie-day, but meesa like more pictures." });
        books[26].Reviews.Add(new Review { Voter = "Kylo Ren", Stars = 4, Comment = "I feel the pull to the light and the dark with this collection. Well balanced." });
        books[26].Reviews.Add(new Review { Voter = "Rey Skywalker", Stars = 5, Comment = "Inspirational and filled with hope. A great way to revisit the legends." });
        books[27].Reviews.Add(new Review { Voter = "Hari Seldon", Stars = 5, Comment = "A masterpiece of predictive science and epic storytelling." });
        books[28].Reviews.Add(new Review { Voter = "R. Daneel Olivaw", Stars = 4, Comment = "Intriguing continuation of the Foundation saga. A must-read." });
        books[29].Reviews.Add(new Review { Voter = "The Mule", Stars = 5, Comment = "The climax of the original trilogy is a triumph of storytelling." });
        books[30].Reviews.Add(new Review { Voter = "Susan Calvin", Stars = 5, Comment = "A thought-provoking exploration of robotics and humanity." });
        books[31].Reviews.Add(new Review { Voter = "Ebling Mis", Stars = 4, Comment = "Complex and innovative. Asimov at his finest." });
        books[32].Reviews.Add(new Review { Voter = "Chef Ramsey", Stars = 5, Comment = "Brilliant guide to French cuisine!" });
        books[33].Reviews.Add(new Review { Voter = "Home Cook Jane", Stars = 4, Comment = "A must-have for every kitchen." });
        books[34].Reviews.Add(new Review { Voter = "Culinary Enthusiast", Stars = 5, Comment = "Innovative and inspirational!" });
        books[35].Reviews.Add(new Review { Voter = "Professional Chef Alex", Stars = 5, Comment = "A bible for professional chefs." });
        books[36].Reviews.Add(new Review { Voter = "Italian Cuisine Lover", Stars = 5, Comment = "Authentic and delightful recipes." });
        books[40].Reviews.Add(new Review { Voter = "History Buff", Stars = 5, Comment = "Inspiring and insightful." });
        books[41].Reviews.Add(new Review { Voter = "Obama Supporter", Stars = 5, Comment = "A hopeful and beautifully written book." });
        books[42].Reviews.Add(new Review { Voter = "Self-help Enthusiast", Stars = 5, Comment = "A practical guide to forming life-changing habits." });
        books[43].Reviews.Add(new Review { Voter = "Casual Reader", Stars = 4, Comment = "Entertaining and thought-provoking." });
        books[44].Reviews.Add(new Review { Voter = "Minimalist", Stars = 5, Comment = "A refreshing perspective on finding purpose." });
        books[45].Reviews.Add(new Review { Voter = "Health Advocate", Stars = 5, Comment = "Packed with insights for a healthier life." });
        books[46].Reviews.Add(new Review { Voter = "Early Riser", Stars = 4, Comment = "Motivating and inspiring." });

        books[1].Reviews.Add(new Review { Voter = "CodeCraftsman", Stars = 5, Comment = "A true classic on writing clean, readable code." });
        books[2].Reviews.Add(new Review { Voter = "DevLead", Stars = 5, Comment = "Changed the way I approach software engineering." });
        books[3].Reviews.Add(new Review { Voter = "RefactorMaster", Stars = 4, Comment = "Excellent step-by-step guides for refactoring code." });
        books[4].Reviews.Add(new Review { Voter = "BookWorm", Stars = 4, Comment = "A dark and gripping thriller, highly recommend." });
        books[5].Reviews.Add(new Review { Voter = "SciFiFan", Stars = 5, Comment = "The greatest science fiction novel of all time." });
        books[6].Reviews.Add(new Review { Voter = "PatternSeeker", Stars = 5, Comment = "An absolute necessity for object-oriented design." });
        books[7].Reviews.Add(new Review { Voter = "ProjectManager", Stars = 4, Comment = "A foundational book for project management." });
        books[8].Reviews.Add(new Review { Voter = "ClassicReader", Stars = 5, Comment = "Beautifully written and deeply moving." });
        books[10].Reviews.Add(new Review { Voter = "WinstonSmith", Stars = 5, Comment = "Chillingly prophetic and powerful." });
        books[11].Reviews.Add(new Review { Voter = "PotterHead", Stars = 5, Comment = "The beginning of a magical journey!" });
        books[12].Reviews.Add(new Review { Voter = "HermioneG", Stars = 4, Comment = "A wonderful sequel that expands the wizarding world." });
        books[13].Reviews.Add(new Review { Voter = "SiriusBlack", Stars = 5, Comment = "My personal favorite of the early books." });
        books[15].Reviews.Add(new Review { Voter = "RonWeasley", Stars = 5, Comment = "Bloody brilliant collection!" });
        books[16].Reviews.Add(new Review { Voter = "NedStark", Stars = 5, Comment = "A masterfully woven tale of politics and survival." });
        books[17].Reviews.Add(new Review { Voter = "JorahMormont", Stars = 4, Comment = "A great setup for the events to come." });
        books[18].Reviews.Add(new Review { Voter = "JonSnow", Stars = 5, Comment = "Unbelievable twists and turns. Incredible book." });
        books[19].Reviews.Add(new Review { Voter = "Gandalf", Stars = 5, Comment = "A masterpiece of world-building." });
        books[20].Reviews.Add(new Review { Voter = "BilboBaggins", Stars = 5, Comment = "The journey continues beautifully." });
        books[21].Reviews.Add(new Review { Voter = "Gimli", Stars = 5, Comment = "A perfect ending. Unforgettable." });
        books[23].Reviews.Add(new Review { Voter = "Chewbacca", Stars = 5, Comment = "HRRRR-WOOF! *translation: Incredible continuation!*" });
        books[24].Reviews.Add(new Review { Voter = "LandoC", Stars = 4, Comment = "Very solid writing and plot development." });
        books[25].Reviews.Add(new Review { Voter = "ObiWan", Stars = 5, Comment = "A classic Legends story done right." });
        books[26].Reviews.Add(new Review { Voter = "ThrawnFan", Stars = 5, Comment = "The ultimate Thrawn collection." });
        books[27].Reviews.Add(new Review { Voter = "EldonH", Stars = 5, Comment = "Fascinating vision of the far future." });
        books[28].Reviews.Add(new Review { Voter = "AsimovFan", Stars = 4, Comment = "Maintains the high bar set by the first book." });
        books[29].Reviews.Add(new Review { Voter = "GalacticCitizen", Stars = 5, Comment = "Superb execution of the trilogy's conclusion." });
        books[30].Reviews.Add(new Review { Voter = "Roboticist", Stars = 5, Comment = "The foundation of modern robot stories." });
        books[31].Reviews.Add(new Review { Voter = "ScienceLover", Stars = 5, Comment = "One of Asimov's best stand-alone novels." });
        books[32].Reviews.Add(new Review { Voter = "JuliaFan", Stars = 5, Comment = "Indispensable guide for French cooking." });
        books[33].Reviews.Add(new Review { Voter = "HomeChef", Stars = 5, Comment = "An absolute staple in my kitchen." });
        books[34].Reviews.Add(new Review { Voter = "NosratFan", Stars = 5, Comment = "Teaches you how to think about cooking." });
        books[35].Reviews.Add(new Review { Voter = "ChefInTraining", Stars = 5, Comment = "Crucial reference for any professional kitchen." });
        books[36].Reviews.Add(new Review { Voter = "MarcellaFan", Stars = 5, Comment = "The best Italian cookbook in English." });
        books[40].Reviews.Add(new Review { Voter = "ObamaReader", Stars = 5, Comment = "An incredibly reflective and engaging memoir." });
        books[41].Reviews.Add(new Review { Voter = "Hopeful", Stars = 4, Comment = "Inspiring perspective on politics and hope." });
        books[42].Reviews.Add(new Review { Voter = "HabitBuilder", Stars = 5, Comment = "Very actionable advice on habit formation." });
        books[43].Reviews.Add(new Review { Voter = "MansonFan", Stars = 4, Comment = "Blunt, honest, and very helpful." });
        books[44].Reviews.Add(new Review { Voter = "IkigaiSeeker", Stars = 5, Comment = "Beautifully explains the Japanese secret to long life." });
        books[45].Reviews.Add(new Review { Voter = "BlueZoneFan", Stars = 5, Comment = "Great tips on diet and longevity." });
        books[46].Reviews.Add(new Review { Voter = "5AMer", Stars = 4, Comment = "Great routine to start the day with focus." });

        // PriceOffers (Promotions)
        books[1].Promotion = new PriceOffer { NewPrice = 26.99m, Text = "Limited Time Offer" };
        books[5].Promotion = new PriceOffer { NewPrice = 35.00m, Text = "Special Edition Offer" };
        books[6].Promotion = new PriceOffer { NewPrice = 49.99m, Text = "Early Bird Discount" };
        books[11].Promotion = new PriceOffer { NewPrice = 12.99m, Text = "Limited Time Magic Deal" };
        books[12].Promotion = new PriceOffer { NewPrice = 13.99m, Text = "Chamber Special Offer" };
        books[15].Promotion = new PriceOffer { NewPrice = 34.99m, Text = "Holiday Box Set Discount" };
        books[16].Promotion = new PriceOffer { NewPrice = 16.99m, Text = "Winter Is Coming Discount" };
        books[18].Promotion = new PriceOffer { NewPrice = 19.99m, Text = "Epic Fantasy Special" };
        books[19].Promotion = new PriceOffer { NewPrice = 12.99m, Text = "Middle-earth Special" };
        books[20].Promotion = new PriceOffer { NewPrice = 15.99m, Text = "Adventure Continues Discount" };
        books[22].Promotion = new PriceOffer { NewPrice = 34.99m, Text = "Collector's Edition Discount" };
        books[23].Promotion = new PriceOffer { NewPrice = 10.99m, Text = "Galaxy Saver Discount" };
        books[25].Promotion = new PriceOffer { NewPrice = 12.99m, Text = "Force Awakens Special" };
        books[26].Promotion = new PriceOffer { NewPrice = 34.99m, Text = "Box Set Discount" };
        books[27].Promotion = new PriceOffer { NewPrice = 12.99m, Text = "Foundation Trilogy Sale" };
        books[30].Promotion = new PriceOffer { NewPrice = 14.99m, Text = "Sci-Fi Classics Discount" };
        books[32].Promotion = new PriceOffer { NewPrice = 25.99m, Text = "Limited Time Cooking Discount" };
        books[33].Promotion = new PriceOffer { NewPrice = 22.99m, Text = "Joy of Cooking Sale" };
        books[40].Promotion = new PriceOffer { NewPrice = 29.99m, Text = "Limited Time Political Discount" };
        books[41].Promotion = new PriceOffer { NewPrice = 19.99m, Text = "Special Price for Fans" };
        books[42].Promotion = new PriceOffer { NewPrice = 19.99m, Text = "New Year Lifestyle Promo" };
        books[44].Promotion = new PriceOffer { NewPrice = 14.99m, Text = "Limited Time Offer" };

        return books;
    }

    private static void AddAuthor(Book book, Author author, byte order) => book.AuthorsLink.Add(new BookAuthor
    {
        Book = book,
        Author = author,
        Order = order
    });
}

internal static partial class CatalogDbContextInitialiserLogging
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "An error occurred while initialising the database.")]
    public static partial void LogInitialisationError(this ILogger<CatalogDbContextInitialiser> logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "An error occurred while seeding the database.")]
    public static partial void LogSeedingError(this ILogger<CatalogDbContextInitialiser> logger, Exception ex);
}
