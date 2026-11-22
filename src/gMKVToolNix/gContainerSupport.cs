using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gMKVToolNix
{
    /// <summary>
    /// Helpers for supporting non-Matroska input files (MP4, MOV, AVI, TS, ...).
    /// mkvextract can only read Matroska, so non-Matroska inputs are transparently
    /// remuxed (copied without re-encoding) to a temporary Matroska file by mkvmerge.
    /// </summary>
    public static class gContainerSupport
    {
        private static readonly byte[] _ebmlMagic = { 0x1A, 0x45, 0xDF, 0xA3 };

        /// <summary>
        /// Native Matroska extensions (no remux needed).
        /// </summary>
        public static readonly string[] MatroskaExtensions =
        {
            ".mkv", ".mka", ".mks", ".mk3d", ".webm"
        };

        /// <summary>
        /// Other container extensions that mkvmerge can read.
        /// </summary>
        public static readonly string[] OtherContainerExtensions =
        {
            ".mp4", ".m4v", ".m4a", ".mov", ".3gp", ".3g2",
            ".avi",
            ".ts", ".m2ts", ".mts", ".tp", ".trp",
            ".mpg", ".mpeg", ".m2v", ".vob", ".evo", ".evob",
            ".ogg", ".ogm", ".ogv", ".oga", ".opus",
            ".flv", ".f4v",
            ".rm", ".rmvb",
            ".ivf"
        };

        /// <summary>
        /// All the input extensions accepted by the application.
        /// </summary>
        public static IEnumerable<string> SupportedInputExtensions
        {
            get { return MatroskaExtensions.Concat(OtherContainerExtensions); }
        }

        /// <summary>
        /// Returns true if the file's extension belongs to a supported container.
        /// </summary>
        public static bool IsSupportedInputFile(string argFilename)
        {
            if (string.IsNullOrWhiteSpace(argFilename))
            {
                return false;
            }

            string extension = Path.GetExtension(argFilename).ToLowerInvariant();
            return SupportedInputExtensions.Contains(extension);
        }

        /// <summary>
        /// Returns true if the file really is a Matroska/WebM file (checks the EBML header,
        /// not the extension).
        /// </summary>
        public static bool IsMatroskaFile(string argFilename)
        {
            try
            {
                using (FileStream fs = new FileStream(argFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] header = new byte[_ebmlMagic.Length];
                    int read = fs.Read(header, 0, header.Length);
                    if (read < header.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < header.Length; i++)
                    {
                        if (header[i] != _ebmlMagic[i])
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
            catch
            {
                // If we can't read it, fall back to the extension
                return MatroskaExtensions.Contains(Path.GetExtension(argFilename).ToLowerInvariant());
            }
        }

        /// <summary>
        /// Gets the file dialog pattern for all the supported files (e.g. "*.mkv;*.mp4;...").
        /// </summary>
        public static string GetDialogPattern()
        {
            return string.Join(";", SupportedInputExtensions.Select(x => "*" + x));
        }

        /// <summary>
        /// Builds the full file dialog filter: "all supported files" first, then the provided
        /// (localized) Matroska filter entries.
        /// </summary>
        public static string GetDialogFilter(string argAllSupportedLabel, string argMatroskaFilter)
        {
            string pattern = GetDialogPattern();
            string filter = string.Format("{0}|{1}", argAllSupportedLabel, pattern);
            if (!string.IsNullOrWhiteSpace(argMatroskaFilter))
            {
                filter = filter + "|" + argMatroskaFilter;
            }
            return filter;
        }

        /// <summary>
        /// Creates a unique temporary Matroska filename in the provided directory
        /// (or in the system temp directory if none was provided or it's not writable).
        /// </summary>
        public static string GetTemporaryMatroskaFilename(string argPreferredDirectory)
        {
            string fileName = string.Format(".gTrackExtract-{0}.tmp.mkv", Guid.NewGuid().ToString("N"));

            if (!string.IsNullOrWhiteSpace(argPreferredDirectory))
            {
                try
                {
                    if (!Directory.Exists(argPreferredDirectory))
                    {
                        Directory.CreateDirectory(argPreferredDirectory);
                    }

                    string candidate = Path.Combine(argPreferredDirectory, fileName);
                    // Check that the directory is writable
                    using (File.Create(candidate)) { }
                    File.Delete(candidate);
                    return candidate;
                }
                catch
                {
                    // fall back to the temp directory
                }
            }

            return Path.Combine(Path.GetTempPath(), fileName);
        }

        /// <summary>
        /// Deletes a temporary file, ignoring any errors.
        /// </summary>
        public static void TryDeleteFile(string argFilename)
        {
            if (string.IsNullOrWhiteSpace(argFilename))
            {
                return;
            }

            try
            {
                if (File.Exists(argFilename))
                {
                    File.Delete(argFilename);
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
