using CustomJson;

public enum Role
{
    Student,
    Admin
}

public class Address
{
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public int? Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UserId { get; set; }
    public Role Role { get; set; }
    public Address Address { get; set; } = new();
    public List<string> Skills { get; set; } = new();
}

public class Program
{
    public static void Main()
    {
        // Console.WriteLine();
        // Console.WriteLine("=== Basic Types Test ===");

        // Console.WriteLine(JsonSerializer.Serialize("Hello"));
        // Console.WriteLine(JsonSerializer.Serialize(123));
        // Console.WriteLine(JsonSerializer.Serialize(123456789L));
        // Console.WriteLine(JsonSerializer.Serialize(12.5f));
        // Console.WriteLine(JsonSerializer.Serialize(12.5));
        // Console.WriteLine(JsonSerializer.Serialize(99.99m));
        // Console.WriteLine(JsonSerializer.Serialize(true));
        // Console.WriteLine(JsonSerializer.Serialize(false));

        // string? nothing = null;
        // Console.WriteLine(JsonSerializer.Serialize(nothing));

        var user = new User
        {
            Id = 1,
            Name = "John",
            IsActive = true,
            Age = 24,
            CreatedAt = new DateTime(2026, 9, 11, 18, 30, 0, DateTimeKind.Utc),
            UserId = Guid.NewGuid(),
            Role = Role.Student,
            Address = new Address
            {
                City = "Dhaka",
                Country = "Bangladesh"
            },
            Skills = new List<string> { "C#", "C++", "ASP.NET" }
        };

        Console.WriteLine("=== Serialization ===");
        string json = JsonSerializer.Serialize(user);
        Console.WriteLine(json);

        Console.WriteLine();
        Console.WriteLine("=== Deserialization ===");

        User? restored = JsonSerializer.Deserialize<User>(json);

        Console.WriteLine($"Id: {restored?.Id}");
        Console.WriteLine($"Name: {restored?.Name}");
        Console.WriteLine($"Active: {restored?.IsActive}");
        Console.WriteLine($"Age: {restored?.Age}");
        Console.WriteLine($"City: {restored?.Address.City}");
        Console.WriteLine($"Role: {restored?.Role}");
        Console.WriteLine($"Skills: {string.Join(", ", restored?.Skills ?? new())}");

        Console.WriteLine();
        Console.WriteLine("=== Dictionary ===");

        var dictionary = new Dictionary<string, object>
        {
            ["Name"] = "Ruhan",
            ["Age"] = 25,
            ["Active"] = true
        };

        Console.WriteLine(JsonSerializer.Serialize(dictionary));

        Console.WriteLine();
        Console.WriteLine("=== Array ===");
        Console.WriteLine(JsonSerializer.Serialize(new[] { 10, 20, 30 }));

        Console.WriteLine();
        Console.WriteLine("=== Nullable ===");
        int? score = null;
        Console.WriteLine(JsonSerializer.Serialize(score));

        Console.WriteLine();
        Console.WriteLine("=== Circular Reference Test ===");

        var person = new Person { Name = "Ruhan" };
        person.Friend = person;

        try
        {
            Console.WriteLine(JsonSerializer.Serialize(person));
        }
        catch (JsonSerializationException ex)
        {
            Console.WriteLine(ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("=== Invalid JSON Test ===");

        try
        {
            JsonSerializer.Deserialize<User>("{\"Id\": 1,");
        }
        catch (JsonDeserializationException ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

public class Person
{
    public string Name { get; set; } = "";
    public Person? Friend { get; set; }

    
}
