# Build Your Own JSON Serializer

A custom JSON serializer and deserializer written from scratch in C#.

## Restrictions

The core implementation does not use:

- `System.Text.Json`
- Newtonsoft.Json / Json.NET
- Any third-party JSON library

It uses only the .NET base class library, reflection, generics, collections, `StringBuilder`, and standard parsing/conversion APIs.

## Features

### Serialization

Supported:

- `string`
- `int`, `long`, `float`, `double`, `decimal`
- `bool`
- `null`
- nested objects
- arrays
- `List<T>`
- `IEnumerable<T>`-style collections that can be instantiated and have `Add`
- `Dictionary<string, T>` and other string-keyed `IDictionary`
- `DateTime`
- `DateTimeOffset`
- `Guid`
- `enum`
- nullable value types such as `int?`

Objects are inspected dynamically using reflection.

### Deserialization

The parser reads:

- objects
- arrays
- strings
- numbers
- booleans
- null

It then converts the parsed structure into the requested C# type using reflection and recursive conversion.

## Design decisions

### DateTime

`DateTime` is serialized as an ISO-8601 round-trip string using the `"O"` format.

Example:

```json
"2026-09-11T18:30:00.0000000Z"
```

### Guid

`Guid` is serialized as its standard string representation.

### Enum

Enums are serialized by name:

```json
"Student"
```

### Circular references

The serializer keeps a set of objects currently being traversed.

If an object appears again while it is already being traversed, a circular reference is detected and `JsonSerializationException` is thrown.

This avoids infinite recursion.

Repeated references that are not circular are allowed and serialized normally.

### Unknown JSON object properties

During deserialization, JSON properties that do not exist on the target C# class are ignored.

## Performance

Reflection metadata is cached in:

```csharp
ConcurrentDictionary<Type, PropertyInfo[]>
```

Without caching, `GetProperties()` would be repeated for the same type. With caching, property metadata is discovered once and reused.

For the final report, run the same serialization operation thousands of times and compare a cached implementation against a deliberately uncached version.

## Limitations

- Only string-keyed dictionaries are supported because JSON object property names are strings.
- Classes being deserialized need an accessible parameterless constructor.
- Read-only properties cannot be populated during deserialization.
- Polymorphic object graphs are not handled.
- Circular references throw an exception rather than being converted to `$id` / `$ref` metadata.
- The parser intentionally supports standard JSON rather than JavaScript-specific extensions such as comments or trailing commas.
- The implementation is an educational serializer, not a replacement for production JSON libraries.

## Run

```bash
dotnet run
```

## Main API

```csharp
string json = JsonSerializer.Serialize(user);

User? user = JsonSerializer.Deserialize<User>(json);
```
