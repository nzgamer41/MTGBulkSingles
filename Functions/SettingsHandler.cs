using System.Text.Json;

namespace MTGBulkSingles.Functions
{
    internal class SettingsHandler
    {
        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

        /// <summary>
        /// Writes the given object instance to a Json file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the file.</param>
        public static void WriteToJsonFile<T>(string filePath, T objectToWrite) where T : new()
        {
            var contentsToWriteToFile = JsonSerializer.Serialize(objectToWrite, _writeOptions);
            File.WriteAllText(filePath, contentsToWriteToFile);
        }

        /// <summary>
        /// Reads an object instance from an Json file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object to read from the file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the Json file.</returns>
        public static T ReadFromJsonFile<T>(string filePath) where T : new()
        {
            var fileContents = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(fileContents) ?? new T();
        }
    }
}
