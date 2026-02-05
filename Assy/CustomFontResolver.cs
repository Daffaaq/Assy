using PdfSharp.Fonts;
using System;
using System.IO;
using System.Reflection;

namespace Assy
{
    public class CustomFontResolver : IFontResolver
    {
        // Singleton instance
        private static CustomFontResolver? _instance = null;

        public static CustomFontResolver Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new CustomFontResolver();
                return _instance;
            }
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            Console.WriteLine($"Resolving font: {familyName}, Bold: {isBold}, Italic: {isItalic}");

            // Selalu gunakan Arial
            string fontFace = "Arial";

            if (isBold && isItalic)
                return new FontResolverInfo($"{fontFace}#bi");
            else if (isBold)
                return new FontResolverInfo($"{fontFace}#b");
            else if (isItalic)
                return new FontResolverInfo($"{fontFace}#i");
            else
                return new FontResolverInfo($"{fontFace}#");
        }

        public byte[] GetFont(string faceName)
        {
            Console.WriteLine($"Loading font: {faceName}");

            switch (faceName)
            {
                case "Arial#":
                    return LoadFontData("ARIAL.TTF");  // PERUBAHAN DI SINI!
                case "Arial#b":
                    return LoadFontData("ARIALBD.TTF"); // PERUBAHAN DI SINI!
                case "Arial#i":
                    return LoadFontData("ARIALI.TTF");  // PERUBAHAN DI SINI!
                case "Arial#bi":
                    return LoadFontData("ARIALBI.TTF"); // PERUBAHAN DI SINI!
                default:
                    Console.WriteLine($"Font not found: {faceName}, using default");
                    return LoadFontData("ARIAL.TTF");   // PERUBAHAN DI SINI!
            }
        }

        private byte[] LoadFontData(string fontFileName)
        {
            try
            {
                string resourceName = $"Assy.Fonts.{fontFileName}";
                Console.WriteLine($"Loading font resource: {resourceName}");

                var assembly = Assembly.GetExecutingAssembly();

                // Debug: List semua resources
                //foreach (var name in assembly.GetManifestResourceNames())
                //    Console.WriteLine($"Resource: {name}");

                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Console.WriteLine($"Font resource '{resourceName}' not found in assembly!");

                        // Fallback: coba load dari file system
                        string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", fontFileName);
                        if (File.Exists(fontPath))
                        {
                            Console.WriteLine($"Loading font from file system: {fontPath}");
                            return File.ReadAllBytes(fontPath);
                        }

                        throw new FileNotFoundException($"Font '{resourceName}' not found in resources or file system.");
                    }

                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    Console.WriteLine($"Font loaded successfully: {fontFileName}, Size: {data.Length} bytes");
                    return data;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading font {fontFileName}: {ex.Message}");
                throw;
            }
        }
    }

    public static class FontInitializer
    {
        private static bool _initialized = false;
        private static object _lock = new object();

        public static void Initialize()
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    try
                    {
                        Console.WriteLine("Initializing PDF font resolver...");

                        // Set font resolver
                        GlobalFontSettings.FontResolver = CustomFontResolver.Instance;

                        // Optional: Set default font encoding
                        GlobalFontSettings.DefaultFontEncoding = PdfSharp.Pdf.PdfFontEncoding.WinAnsi;

                        Console.WriteLine("Font resolver initialized successfully.");
                        _initialized = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error initializing font resolver: {ex.Message}");
                        throw;
                    }
                }
            }
        }
    }
}