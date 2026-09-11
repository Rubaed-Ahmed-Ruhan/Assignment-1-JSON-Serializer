using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace CustomJson;

public static class JsonSerializer
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public static string Serialize<T>(T value) => Serialize((object?)value);

    public static string Serialize(object? value)
    {
        var sb = new StringBuilder();
        var active = new HashSet<object>(ReferenceEqualityComparer.Instance);
        WriteValue(value, sb, active, "$");
        return sb.ToString();
    }

    public static T? Deserialize<T>(string json)
        => (T?)Deserialize(json, typeof(T));

    public static object? Deserialize(string json, Type targetType)
    {
        if (json is null)
            throw new ArgumentNullException(nameof(json));
        if (targetType is null)
            throw new ArgumentNullException(nameof(targetType));

        var parser = new JsonParser(json);
        var raw = parser.Parse();
        return ConvertToType(raw, targetType, "$");
    }

    private static void WriteValue(object? value, StringBuilder sb, HashSet<object> active, string path)
    {
        if (value is null)
        {
            sb.Append("null");
            return;
        }

        switch (value)
        {
            case string s:
                WriteString(s, sb);
                return;

            case char c:
                WriteString(c.ToString(), sb);
                return;

            case bool b:
                sb.Append(b ? "true" : "false");
                return;

            case byte or sbyte or short or ushort or int or uint or long or ulong:
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;

            case float f:
                if (float.IsNaN(f) || float.IsInfinity(f))
                    throw new JsonSerializationException($"Cannot serialize non-finite float at {path}.");
                sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                return;

            case double d:
                if (double.IsNaN(d) || double.IsInfinity(d))
                    throw new JsonSerializationException($"Cannot serialize non-finite double at {path}.");
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;

            case decimal m:
                sb.Append(m.ToString(CultureInfo.InvariantCulture));
                return;

            case DateTime dt:
                WriteString(dt.ToString("O", CultureInfo.InvariantCulture), sb);
                return;

            case DateTimeOffset dto:
                WriteString(dto.ToString("O", CultureInfo.InvariantCulture), sb);
                return;

            case Guid guid:
                WriteString(guid.ToString(), sb);
                return;
        }

        var type = value.GetType();

        if (type.IsEnum)
        {
            WriteString(value.ToString()!, sb);
            return;
        }

        if (!type.IsValueType)
        {
            if (!active.Add(value))
                throw new JsonSerializationException($"Circular reference detected at {path}.");
        }

        try
        {
            if (value is IDictionary dictionary)
            {
                WriteDictionary(dictionary, sb, active, path);
                return;
            }

            if (value is IEnumerable enumerable)
            {
                WriteArray(enumerable, sb, active, path);
                return;
            }

            WriteObject(value, sb, active, path);
        }
        finally
        {
            if (!type.IsValueType)
                active.Remove(value);
        }
    }

    private static void WriteDictionary(
        IDictionary dictionary,
        StringBuilder sb,
        HashSet<object> active,
        string path)
    {
        sb.Append('{');
        var first = true;

        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string key)
                throw new JsonSerializationException(
                    $"Dictionary keys must be strings at {path}. Found {entry.Key?.GetType().Name ?? "null"}.");

            if (!first)
                sb.Append(',');
            first = false;

            WriteString(key, sb);
            sb.Append(':');
            WriteValue(entry.Value, sb, active, path + "." + key);
        }

        sb.Append('}');
    }

    private static void WriteArray(
        IEnumerable enumerable,
        StringBuilder sb,
        HashSet<object> active,
        string path)
    {
        sb.Append('[');
        var first = true;
        var index = 0;

        foreach (var item in enumerable)
        {
            if (!first)
                sb.Append(',');
            first = false;

            WriteValue(item, sb, active, $"{path}[{index}]");
            index++;
        }

        sb.Append(']');
    }

    private static void WriteObject(
        object value,
        StringBuilder sb,
        HashSet<object> active,
        string path)
    {
        var type = value.GetType();
        var properties = GetSerializableProperties(type);

        sb.Append('{');
        var first = true;

        foreach (var property in properties)
        {
            if (!first)
                sb.Append(',');
            first = false;

            WriteString(property.Name, sb);
            sb.Append(':');

            var propertyValue = property.GetValue(value);
            WriteValue(propertyValue, sb, active, path + "." + property.Name);
        }

        sb.Append('}');
    }

    private static PropertyInfo[] GetSerializableProperties(Type type)
    {
        return PropertyCache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p =>
                 p.CanRead &&
                 p.GetIndexParameters().Length == 0 &&
                 p.GetMethod is not null &&
                 p.GetMethod.IsPublic)
             .OrderBy(p => p.MetadataToken)
             .ToArray());
    }

    private static void WriteString(string value, StringBuilder sb)
    {
        sb.Append('"');

        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (char.IsControl(c))
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        sb.Append('"');
    }

    private static object? ConvertToType(object? raw, Type targetType, string path)
    {
        if (raw is null)
        {
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null)
                return null;

            throw TypeError(path, "null", targetType);
        }

        var nullableUnderlying = Nullable.GetUnderlyingType(targetType);
        if (nullableUnderlying is not null)
            return ConvertToType(raw, nullableUnderlying, path);

        if (targetType == typeof(object))
            return raw;

        if (targetType == typeof(string))
        {
            if (raw is string s) return s;
            throw TypeError(path, raw, targetType);
        }

        if (targetType == typeof(char))
        {
            if (raw is string cs && cs.Length == 1) return cs[0];
            throw new JsonDeserializationException($"Expected a single-character string at {path}.");
        }

        if (targetType == typeof(bool))
        {
            if (raw is bool b) return b;
            throw TypeError(path, raw, targetType);
        }

        if (targetType == typeof(Guid))
        {
            if (raw is string gs && Guid.TryParse(gs, out var guid)) return guid;
            throw new JsonDeserializationException($"Expected a valid Guid string at {path}.");
        }

        if (targetType == typeof(DateTime))
        {
            if (raw is string ds && DateTime.TryParse(
                    ds, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var date))
                return date;

            throw new JsonDeserializationException($"Expected an ISO-compatible DateTime string at {path}.");
        }

        if (targetType == typeof(DateTimeOffset))
        {
            if (raw is string ds && DateTimeOffset.TryParse(
                    ds, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var date))
                return date;

            throw new JsonDeserializationException($"Expected a DateTimeOffset string at {path}.");
        }

        if (targetType.IsEnum)
        {
            if (raw is string enumName &&
                Enum.TryParse(targetType, enumName, ignoreCase: false, out var enumValue))
                return enumValue;

            throw new JsonDeserializationException(
                $"Expected enum name for {targetType.Name} at {path}.");
        }

        if (IsNumberType(targetType))
            return ConvertNumber(raw, targetType, path);

        if (targetType.IsArray)
        {
            if (raw is not List<object?> rawList)
                throw TypeError(path, raw, targetType);

            var elementType = targetType.GetElementType()!;
            var array = Array.CreateInstance(elementType, rawList.Count);

            for (var i = 0; i < rawList.Count; i++)
                array.SetValue(ConvertToType(rawList[i], elementType, $"{path}[{i}]"), i);

            return array;
        }

        if (IsDictionaryType(targetType))
            return ConvertDictionary(raw, targetType, path);

        if (IsEnumerableType(targetType))
            return ConvertCollection(raw, targetType, path);

        if (raw is not Dictionary<string, object?> rawObject)
            throw TypeError(path, raw, targetType);

        return ConvertObject(rawObject, targetType, path);
    }

    private static object ConvertObject(
        Dictionary<string, object?> rawObject,
        Type targetType,
        string path)
    {
        object instance;

        try
        {
            instance = Activator.CreateInstance(targetType)
                ?? throw new JsonDeserializationException(
                    $"Could not create {targetType.Name} at {path}. " +
                    "The type needs an accessible parameterless constructor.");
        }
        catch (MissingMethodException)
        {
            throw new JsonDeserializationException(
                $"Could not create {targetType.Name} at {path}. " +
                "The type needs an accessible parameterless constructor.");
        }

        foreach (var pair in rawObject)
        {
            var property = GetSerializableProperties(targetType)
                .FirstOrDefault(p => string.Equals(p.Name, pair.Key, StringComparison.Ordinal));

            if (property is null)
                continue;

            if (!property.CanWrite || property.SetMethod is null || !property.SetMethod.IsPublic)
                throw new JsonDeserializationException(
                    $"Property '{pair.Key}' on {targetType.Name} is not writable at {path}.");

            var converted = ConvertToType(pair.Value, property.PropertyType, path + "." + pair.Key);

            try
            {
                property.SetValue(instance, converted);
            }
            catch (Exception ex)
            {
                throw new JsonDeserializationException(
                    $"Could not set property '{pair.Key}' on {targetType.Name} at {path}: {ex.Message}", ex);
            }
        }

        return instance;
    }

    private static object ConvertCollection(object? raw, Type targetType, string path)
    {
        if (raw is not List<object?> rawList)
            throw TypeError(path, raw, targetType);

        var elementType = GetElementType(targetType);

        if (targetType.IsInterface || targetType.IsAbstract)
        {
            if (targetType.IsGenericType)
            {
                var genericDefinition = targetType.GetGenericTypeDefinition();
                if (genericDefinition == typeof(IEnumerable<>) ||
                    genericDefinition == typeof(ICollection<>) ||
                    genericDefinition == typeof(IList<>))
                {
                    targetType = typeof(List<>).MakeGenericType(elementType);
                }
            }
        }

        var collection = Activator.CreateInstance(targetType)
            ?? throw new JsonDeserializationException(
                $"Could not create collection type {targetType.Name} at {path}.");

        var addMethod = targetType.GetMethod("Add", new[] { elementType });

        if (addMethod is null)
            throw new JsonDeserializationException(
                $"Collection type {targetType.Name} must have an Add({elementType.Name}) method at {path}.");

        for (var i = 0; i < rawList.Count; i++)
        {
            var item = ConvertToType(rawList[i], elementType, $"{path}[{i}]");
            addMethod.Invoke(collection, new[] { item });
        }

        return collection;
    }

    private static object ConvertDictionary(object? raw, Type targetType, string path)
    {
        if (raw is not Dictionary<string, object?> rawObject)
            throw TypeError(path, raw, targetType);

        if (!targetType.IsGenericType)
            throw new JsonDeserializationException(
                $"Dictionary type {targetType.Name} must be generic at {path}.");

        var args = targetType.GetGenericArguments();
        var keyType = args[0];
        var valueType = args[1];

        if (keyType != typeof(string))
            throw new JsonDeserializationException(
                $"Only Dictionary<string, T> is supported at {path}.");

        var dictionary = Activator.CreateInstance(targetType)
            ?? throw new JsonDeserializationException(
                $"Could not create dictionary type {targetType.Name} at {path}.");

        var addMethod = targetType.GetMethod("Add", new[] { keyType, valueType })
            ?? throw new JsonDeserializationException(
                $"Dictionary type {targetType.Name} does not expose the expected Add method at {path}.");

        foreach (var pair in rawObject)
        {
            var value = ConvertToType(pair.Value, valueType, path + "." + pair.Key);
            addMethod.Invoke(dictionary, new object?[] { pair.Key, value });
        }

        return dictionary;
    }

    private static Type GetElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType()!;

        if (type.IsGenericType)
            return type.GetGenericArguments()[0];

        var enumerable = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0]
            ?? throw new JsonDeserializationException(
                $"Cannot determine collection element type for {type.Name}.");
    }

    private static bool IsEnumerableType(Type type)
    {
        if (type == typeof(string))
            return false;

        return type != typeof(byte[]) &&
               (typeof(IEnumerable).IsAssignableFrom(type) ||
                type.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEnumerable<>)));
    }

    private static bool IsDictionaryType(Type type)
        => typeof(IDictionary).IsAssignableFrom(type) &&
           type.IsGenericType &&
           type.GetGenericArguments()[0] == typeof(string);

    private static bool IsNumberType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    private static object ConvertNumber(object raw, Type targetType, string path)
    {
        if (raw is not IConvertible)
            throw TypeError(path, raw, targetType);

        try
        {
            return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture)!;
        }
        catch (Exception ex)
        {
            throw new JsonDeserializationException(
                $"Cannot convert number '{raw}' to {targetType.Name} at {path}.", ex);
        }
    }

    private static JsonDeserializationException TypeError(
        string path, object raw, Type targetType)
    {
        var actual = raw.GetType().Name;
        return new JsonDeserializationException(
            $"Type mismatch at {path}: JSON value '{raw}' ({actual}) cannot be assigned to {targetType.Name}.");
    }

    private sealed class JsonParser
    {
        private readonly string _json;
        private int _position;

        public JsonParser(string json) => _json = json;

        public object? Parse()
        {
            SkipWhitespace();
            var value = ParseValue();
            SkipWhitespace();

            if (!End)
                Error("Unexpected characters after the JSON value.");

            return value;
        }

        private object? ParseValue()
        {
            SkipWhitespace();

            if (End)
                Error("Unexpected end of JSON.");

            return Current switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '"' => ParseString(),
                't' => ParseLiteral("true", true),
                'f' => ParseLiteral("false", false),
                'n' => ParseLiteral("null", null),
                '-' or >= '0' and <= '9' => ParseNumber(),
                _ => throw Error($"Unexpected character '{Current}'.")
            };
        }

        private Dictionary<string, object?> ParseObject()
        {
            Expect('{');
            SkipWhitespace();

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (TryConsume('}'))
                return result;

            while (true)
            {
                SkipWhitespace();

                if (Current != '"')
                    Error("Expected a property name string.");

                var key = ParseString();
                SkipWhitespace();
                Expect(':');

                var value = ParseValue();
                result[key] = value;

                SkipWhitespace();

                if (TryConsume('}'))
                    break;

                Expect(',');
                SkipWhitespace();

                if (Current == '}')
                    Error("Trailing comma is not allowed.");
            }

            return result;
        }

        private List<object?> ParseArray()
        {
            Expect('[');
            SkipWhitespace();

            var result = new List<object?>();

            if (TryConsume(']'))
                return result;

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();

                if (TryConsume(']'))
                    break;

                Expect(',');
                SkipWhitespace();

                if (Current == ']')
                    Error("Trailing comma is not allowed.");
            }

            return result;
        }

        private string ParseString()
        {
            Expect('"');
            var sb = new StringBuilder();

            while (!End)
            {
                var c = Current;
                _position++;

                if (c == '"')
                    return sb.ToString();

                if (c == '\\')
                {
                    if (End)
                        Error("Unterminated escape sequence.");

                    var escaped = Current;
                    _position++;

                    switch (escaped)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append(ParseUnicodeEscape());
                            break;
                        default:
                            Error($"Invalid escape sequence '\\{escaped}'.");
                            break;
                    }
                }
                else
                {
                    if (c < 0x20)
                        Error("Control characters are not allowed inside JSON strings.");

                    sb.Append(c);
                }
            }

            Error("Unterminated JSON string.");
            return "";
        }

        private char ParseUnicodeEscape()
        {
            if (_position + 4 > _json.Length)
                Error("Incomplete Unicode escape sequence.");

            var hex = _json.Substring(_position, 4);

            if (!ushort.TryParse(
                    hex, NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture, out var value))
                Error($"Invalid Unicode escape '\\u{hex}'.");

            _position += 4;
            return (char)value;
        }

        private object ParseNumber()
        {
            var start = _position;

            if (Current == '-')
                _position++;

            if (End)
                Error("Malformed number.");

            if (Current == '0')
            {
                _position++;
                if (!End && char.IsDigit(Current))
                    Error("Leading zeros are not allowed in JSON numbers.");
            }
            else
            {
                if (!char.IsDigit(Current) || Current == '0')
                    Error("Malformed number.");

                while (!End && char.IsDigit(Current))
                    _position++;
            }

            var isFloating = false;

            if (!End && Current == '.')
            {
                isFloating = true;
                _position++;

                if (End || !char.IsDigit(Current))
                    Error("A decimal point must be followed by digits.");

                while (!End && char.IsDigit(Current))
                    _position++;
            }

            if (!End && (Current == 'e' || Current == 'E'))
            {
                isFloating = true;
                _position++;

                if (!End && (Current == '+' || Current == '-'))
                    _position++;

                if (End || !char.IsDigit(Current))
                    Error("An exponent must contain digits.");

                while (!End && char.IsDigit(Current))
                    _position++;
            }

            var text = _json[start.._position];

            if (!isFloating &&
                long.TryParse(text, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var integer))
                return integer;

            if (decimal.TryParse(text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var decimalValue))
                return decimalValue;

            if (double.TryParse(text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var doubleValue) &&
                !double.IsInfinity(doubleValue))
                return doubleValue;

            Error($"Malformed or unsupported number '{text}'.");
            return 0L;
        }

        private object? ParseLiteral(string literal, object? value)
        {
            if (_position + literal.Length > _json.Length ||
                !_json.AsSpan(_position, literal.Length).SequenceEqual(literal))
            {
                Error($"Expected '{literal}'.");
            }

            _position += literal.Length;
            return value;
        }

        private void SkipWhitespace()
        {
            while (!End && char.IsWhiteSpace(Current))
                _position++;
        }

        private void Expect(char expected)
        {
            SkipWhitespace();

            if (End || Current != expected)
                Error($"Expected '{expected}'.");
            _position++;
        }

        private bool TryConsume(char value)
        {
            SkipWhitespace();

            if (!End && Current == value)
            {
                _position++;
                return true;
            }

            return false;
        }

        private bool End => _position >= _json.Length;
        private char Current => End ? '\0' : _json[_position];

        private JsonDeserializationException Error(string message)
            => throw new JsonDeserializationException(
                $"{message} Position: {_position}.");
    }
}

public sealed class JsonSerializationException : Exception
{
    public JsonSerializationException(string message) : base(message) { }
    public JsonSerializationException(string message, Exception innerException)
        : base(message, innerException) { }
}

public sealed class JsonDeserializationException : Exception
{
    public JsonDeserializationException(string message) : base(message) { }
    public JsonDeserializationException(string message, Exception innerException)
        : base(message, innerException) { }
}
