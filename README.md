# Custom JSON Serializer & Deserializer

A lightweight JSON serialization and deserialization library built from scratch in C# without relying on built-in JSON serialization libraries.

This project demonstrates how JSON data is converted between C# objects and JSON strings using custom serialization logic, reflection, recursive parsing, and type conversion.

## Features

* Serialize primitive data types
* Serialize custom objects using reflection
* Deserialize JSON into strongly typed C# objects
* Support arrays, collections, and dictionaries
* Handle nullable values
* Support DateTime, Guid, enums, and numeric types
* Detect circular references
* Validate and parse JSON manually
* Custom serialization and deserialization exceptions
* Property caching for improved performance

## Technologies

* C#
* .NET
* Reflection
* Generics
* Collections
* Recursive JSON Parsing

## Purpose

The purpose of this project is to understand the internal working principles of JSON serializers and deserializers by implementing the core functionality from scratch rather than depending on external libraries.
