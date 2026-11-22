using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using gMKVToolNix.Log;
using gMKVToolNix.MkvInfo;
using gMKVToolNix.MkvMerge;
using gMKVToolNix.Segments;
using Microsoft.Win32;

namespace gMKVToolNix
{
    public static class gMKVHelper
    {
        private static string _mkvMergeGuiFilename = null;

        /// <summary>
        /// Gets the mkvmerge GUI executable filename
        /// </summary>
        public static string MKV_MERGE_GUI_FILENAME 
        {
            get 
            { 
                if (_mkvMergeGuiFilename != null)
                {
                    return _mkvMergeGuiFilename;
                }

                _mkvMergeGuiFilename = PlatformExtensions.IsOnLinux ? "mmg" : "mmg.exe";

                return _mkvMergeGuiFilename;
            }
        }

        private static string _mkvMergeNewGuiFilename = null;

        /// <summary>
        /// Gets the new mkvmerge GUI executable filename
        /// </summary>
        public static string MKV_MERGE_NEW_GUI_FILENAME
        {
            get 
            { 
                if (_mkvMergeNewGuiFilename != null)
                {
                    return _mkvMergeNewGuiFilename;
                }

                _mkvMergeNewGuiFilename = PlatformExtensions.IsOnLinux ? "mkvmerge" : "mkvmerge.exe";

                return _mkvMergeNewGuiFilename;
            }
        }

        /// <summary>
        /// Unescapes string from mkvtoolnix output
        /// </summary>
        /// <param name="argString"></param>
        /// <returns></returns>
        public static string UnescapeString(string argString)
        {
            return argString.
                Replace(@"\s", " ").
                Replace(@"\2", "\"").
                Replace(@"\c", ":").
                Replace(@"\h", "#").
                Replace(@"\\", @"\").
                Replace(@"\b", "[").
                Replace(@"\B", "]");
        }

        /// <summary>
        /// Escapes string from mkvtoolnix output
        /// </summary>
        /// <param name="argString"></param>
        /// <returns></returns>
        public static string EscapeString(string argString)
        {
            return argString.
                Replace(" ", @"\s").
                Replace("\"", @"\2").
                Replace(":", @"\c").
                Replace("#", @"\h").
                Replace(@"\", @"\\").
                Replace("[", @"\b").
                Replace("]", @"\B");
        }

        /// <summary>
        /// Returns the path from MKVToolnix.
        /// It tries to find it via the registry keys.
        /// If it doesn't find it, it throws an exception.
        /// </summary>
        /// <returns></returns>
        public static string GetMKVToolnixPathViaRegistry()
        {
            // Check if we are on Linux, so we don't have to check the registry
            if (PlatformExtensions.IsOnLinux)
            {
                throw new Exception("Running on Linux...");
            }

            RegistryKey regMkvToolnix = null;
            string valuePath = "";
            bool subKeyFound = false;
            bool valueFound = false;

            // First check for Installed MkvToolnix
            // First check Win32 registry
            RegistryKey regUninstall = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Microsoft").
                OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Uninstall");

            foreach (string subKeyName in regUninstall.GetSubKeyNames())
            {
                if (subKeyName.Equals("MKVToolNix", StringComparison.OrdinalIgnoreCase))
                {
                    subKeyFound = true;
                    regMkvToolnix = regUninstall.OpenSubKey("MKVToolNix");
                    break;
                }
            }

            // if sub key was found, try to get the executable path
            if (subKeyFound)
            {
                foreach (string valueName in regMkvToolnix.GetValueNames())
                {
                    if (valueName.Equals("DisplayIcon", StringComparison.OrdinalIgnoreCase))
                    {
                        valueFound = true;
                        valuePath = (string)regMkvToolnix.GetValue(valueName);
                        gMKVLogger.Log($"Found MKVToolNix in registry (Win32): {valuePath}");
                        break;
                    }
                }
            }

            // if value was not found, search Win64 registry
            if (!valueFound)
            {
                subKeyFound = false;

                regUninstall = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Wow6432Node").OpenSubKey("Microsoft").
                    OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Uninstall");

                foreach (string subKeyName in regUninstall.GetSubKeyNames())
                {
                    if (subKeyName.Equals("MKVToolNix", StringComparison.OrdinalIgnoreCase))
                    {
                        subKeyFound = true;
                        regMkvToolnix = regUninstall.OpenSubKey("MKVToolNix");
                        break;
                    }
                }

                // if sub key was found, try to get the executable path
                if (subKeyFound)
                {
                    foreach (string valueName in regMkvToolnix.GetValueNames())
                    {
                        if (valueName.Equals("DisplayIcon", StringComparison.OrdinalIgnoreCase))
                        {
                            valueFound = true;
                            valuePath = (string)regMkvToolnix.GetValue(valueName);
                            gMKVLogger.Log($"Found MKVToolNix in registry (Win64): {valuePath}");
                            break;
                        }
                    }
                }
            }

            // if value was still not found, we may have portable installation
            // let's try the CURRENT_USER registry
            if (!valueFound)
            {
                RegistryKey regSoftware = Registry.CurrentUser.OpenSubKey("Software");
                subKeyFound = false;
                foreach (string subKey in regSoftware.GetSubKeyNames())
                {
                    if (subKey.Equals("mkvmergeGUI", StringComparison.OrdinalIgnoreCase))
                    {
                        subKeyFound = true;
                        regMkvToolnix = regSoftware.OpenSubKey("mkvmergeGUI");
                        break;
                    }
                }

                // if we didn't find the MkvMergeGUI key, all hope is lost
                if (!subKeyFound)
                {
                    throw new Exception($"Couldn't find MKVToolNix in your system!{Environment.NewLine}Please download and install it or provide a manual path!");
                }

                RegistryKey regGui = null;
                bool foundGuiKey = false;
                foreach (string subKey in regMkvToolnix.GetSubKeyNames())
                {
                    if (subKey.Equals("GUI", StringComparison.OrdinalIgnoreCase))
                    {
                        foundGuiKey = true;
                        regGui = regMkvToolnix.OpenSubKey("GUI");
                        break;
                    }
                }
                // if we didn't find the GUI key, all hope is lost
                if (!foundGuiKey)
                {
                    throw new Exception("Found MKVToolNix in your system but not the registry Key GUI!");
                }

                foreach (string valueName in regGui.GetValueNames())
                {
                    if (valueName.Equals("mkvmerge_executable", StringComparison.OrdinalIgnoreCase))
                    {
                        valueFound = true;
                        valuePath = (string)regGui.GetValue("mkvmerge_executable");
                        gMKVLogger.Log($"Found MKVToolNix in registry (CURRENT_USER): {valuePath}");
                        break;
                    }
                }
                // if we didn't find the mkvmerge_executable value, all hope is lost
                if (!valueFound)
                {
                    throw new Exception("Found MKVToolNix in your system but not the registry value mkvmerge_executable!");
                }
            }

            // Now that we found a value (otherwise we would not be here, an exception would have been thrown)
            // let's check if it's valid
            if (!File.Exists(valuePath))
            {
                throw new Exception($"Found a registry value ({valuePath}) for MKVToolNix in your system but it is not valid!");
            }

            // Everything is A-OK! Return the valid Directory value! :)
            return Path.GetDirectoryName(valuePath);
        }

        /// <summary>
        /// Creates a merged list of MKV segments using both MKVMerge and MKVInfo tools.
        /// This is a convenience method that constructs the necessary tool instances and delegates
        /// to the more detailed overload.
        /// </summary>
        /// <param name="argMkvToolnixPath">The path to the MKVToolnix installation directory</param>
        /// <param name="argInputFile">The input MKV file to analyze</param>
        /// <returns>A complete list of gMKVSegment objects with merged information from both tools</returns>
        public static List<gMKVSegment> GetMergedMkvSegmentList(string argMkvToolnixPath, string argInputFile)
        {
            gMKVMerge gMerge = new gMKVMerge(argMkvToolnixPath);
            gMKVInfo gInfo = new gMKVInfo(argMkvToolnixPath);

            return GetMergedMkvSegmentList(gMerge, gInfo, argInputFile);
        }

        /// <summary>
        /// Gets a merged list of MKV segments by combining information from both mkvmerge and mkvinfo.
        /// This method retrieves segment data from mkvmerge first, then supplements missing information 
        /// (segment info, codec private data, delays, etc.) from mkvinfo if needed.
        /// </summary>
        /// <param name="gMerge">The gMKVMerge instance to use for primary segment extraction</param>
        /// <param name="gInfo">The gMKVInfo instance to use for supplementary information</param>
        /// <param name="argInputFile">The input MKV file path to analyze</param>
        /// <returns>A complete list of gMKVSegment objects with merged information from both tools</returns>
        public static List<gMKVSegment> GetMergedMkvSegmentList(gMKVMerge gMerge, gMKVInfo gInfo, string argInputFile)
        {
            if (gContainerSupport.IsMatroskaFile(argInputFile))
            {
                return GetMergedMkvSegmentListCore(gMerge, gInfo, argInputFile);
            }

            return GetNonMatroskaSegmentList(gMerge, gInfo, argInputFile);
        }

        /// <summary>
        /// Gets the segment list of a non-Matroska file (MP4, AVI, TS, ...).
        /// mkvmerge can identify these files, but it doesn't report Matroska codec IDs, codec private data
        /// or delays for them, which the rest of the application relies on. So we remux a small part
        /// (a "probe") of the file to a temporary Matroska file, analyze that, and map its tracks back
        /// to the original file's track IDs.
        /// </summary>
        private static List<gMKVSegment> GetNonMatroskaSegmentList(gMKVMerge gMerge, gMKVInfo gInfo, string argInputFile)
        {
            // Identify the original file (track IDs, chapters, attachments)
            List<gMKVSegment> originalSegments = gMerge.GetMKVSegments(argInputFile);
            List<gMKVTrack> originalTracks = originalSegments.OfType<gMKVTrack>().OrderBy(t => t.TrackID).ToList();

            if (originalTracks.Count == 0)
            {
                throw new Exception(string.Format("No tracks were found in the file {0}!", argInputFile));
            }

            string probeFile = gContainerSupport.GetTemporaryMatroskaFilename(null);
            try
            {
                // Remux only the first seconds of the file, which is enough to get all the track information
                // (mkvmerge stops reading the input after the last split part)
                gMerge.Remux(argInputFile, probeFile, new[] { "--split", "parts:00:00:00-00:00:10" });

                List<gMKVSegment> probeSegments = GetMergedMkvSegmentListCore(gMerge, gInfo, probeFile);
                List<gMKVTrack> probeTracks = probeSegments.OfType<gMKVTrack>().OrderBy(t => t.TrackID).ToList();

                // mkvmerge keeps the order of the tracks, so map the probe's tracks to the original track IDs by position.
                // Verify that assumption, so that we never show (and extract) the wrong track.
                if (!TrackTypesMatch(probeTracks, originalTracks))
                {
                    throw new Exception(string.Format(
                        "Could not analyze the file {0}!{1}The tracks reported by mkvmerge ({2}) don't match the remuxed tracks ({3}).",
                        argInputFile, Environment.NewLine,
                        string.Join(", ", originalTracks.Select(t => t.TrackType.ToString())),
                        string.Join(", ", probeTracks.Select(t => t.TrackType.ToString()))));
                }

                for (int i = 0; i < probeTracks.Count; i++)
                {
                    probeTracks[i].TrackID = originalTracks[i].TrackID;
                    if (string.IsNullOrWhiteSpace(probeTracks[i].TrackName))
                    {
                        probeTracks[i].TrackName = originalTracks[i].TrackName;
                    }
                }

                List<gMKVSegment> finalList = new List<gMKVSegment>();

                // Segment info: use the original file's info (the probe's duration isn't the real one)
                gMKVSegmentInfo probeInfo = probeSegments.OfType<gMKVSegmentInfo>().FirstOrDefault();
                gMKVSegmentInfo segInfo = originalSegments.OfType<gMKVSegmentInfo>().FirstOrDefault() ?? new gMKVSegmentInfo();
                if (probeInfo != null && string.IsNullOrWhiteSpace(segInfo.TimecodeScale))
                {
                    segInfo.TimecodeScale = probeInfo.TimecodeScale;
                }
                segInfo.Directory = Path.GetDirectoryName(argInputFile);
                segInfo.Filename = Path.GetFileName(argInputFile);
                finalList.Add(segInfo);

                // Tracks from the probe (full information)
                finalList.AddRange(probeTracks);

                // Chapters and attachments from the original (the probe only has part of the file)
                finalList.AddRange(originalSegments.Where(s => !(s is gMKVTrack) && !(s is gMKVSegmentInfo)));

                return finalList;
            }
            finally
            {
                gContainerSupport.TryDeleteFile(probeFile);
            }
        }

        /// <summary>
        /// Returns true if both track lists have the same number of tracks, with the same types in the same order.
        /// </summary>
        public static bool TrackTypesMatch(IList<gMKVTrack> argTracks, IList<gMKVTrack> argOtherTracks)
        {
            if (argTracks.Count != argOtherTracks.Count)
            {
                return false;
            }

            for (int i = 0; i < argTracks.Count; i++)
            {
                if (argTracks[i].TrackType != argOtherTracks[i].TrackType)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<gMKVSegment> GetMergedMkvSegmentListCore(gMKVMerge gMerge, gMKVInfo gInfo, string argInputFile)
        {
            List<gMKVSegment> segmentListFromMkvMerge = gMerge.GetMKVSegments(argInputFile);

            // Check if information was found in mkvmerge output
            bool segmentInfoWasFoundInMkvMerge = false;
            foreach (gMKVSegment seg in segmentListFromMkvMerge)
            {
                if (seg is gMKVSegmentInfo)
                {
                    segmentInfoWasFoundInMkvMerge = true;
                    break;
                }
            }

            // Check if codec_private_data was found in mkvmerge output
            bool codecPrivateDataWasFoundInMkvMerge = false;
            foreach (gMKVTrack segTrackFromMkvMerge in segmentListFromMkvMerge.OfType<gMKVTrack>())
            {
                if (!string.IsNullOrWhiteSpace(segTrackFromMkvMerge.CodecPrivateData))
                {
                    codecPrivateDataWasFoundInMkvMerge = true;
                    break;
                }
            }

            if (!segmentInfoWasFoundInMkvMerge || !codecPrivateDataWasFoundInMkvMerge)
            {
                List<gMKVSegment> segmentListFromMkvInfo = gInfo.GetMKVSegments(argInputFile);
                foreach (gMKVSegment segFromMkvInfo in segmentListFromMkvInfo)
                {
                    if (!segmentInfoWasFoundInMkvMerge && segFromMkvInfo is gMKVSegmentInfo)
                    {
                        // Segment info should aleays be first in the list
                        segmentListFromMkvMerge.Insert(0, segFromMkvInfo);
                    }
                    else if (!codecPrivateDataWasFoundInMkvMerge && segFromMkvInfo is gMKVTrack trackFromMkvInfo)
                    {
                        // Update CodecPrivate info from mkvinfo to mkvextract segments
                        foreach (gMKVTrack trackFromMkvMerge in segmentListFromMkvMerge.OfType<gMKVTrack>())
                        {
                            if (trackFromMkvMerge.TrackID == trackFromMkvInfo.TrackID)
                            {
                                if (!string.IsNullOrWhiteSpace(trackFromMkvInfo.CodecPrivate))
                                {
                                    trackFromMkvMerge.CodecPrivate = trackFromMkvInfo.CodecPrivate;
                                }

                                if (trackFromMkvMerge.TrackType == MkvTrackType.video)
                                {
                                    if (trackFromMkvMerge.VideoPixelWidth < trackFromMkvInfo.VideoPixelWidth)
                                    {
                                        trackFromMkvMerge.VideoPixelWidth = trackFromMkvInfo.VideoPixelWidth;
                                    }

                                    if (trackFromMkvMerge.VideoPixelHeight < trackFromMkvInfo.VideoPixelHeight)
                                    {
                                        trackFromMkvMerge.VideoPixelHeight = trackFromMkvInfo.VideoPixelHeight;
                                    }

                                    if (!string.IsNullOrWhiteSpace(trackFromMkvInfo.ExtraInfo))
                                    {
                                        trackFromMkvMerge.ExtraInfo = trackFromMkvInfo.ExtraInfo;
                                    }
                                }
                                else if (trackFromMkvMerge.TrackType == MkvTrackType.audio)
                                {
                                    if (trackFromMkvMerge.AudioChannels < trackFromMkvInfo.AudioChannels)
                                    {
                                        trackFromMkvMerge.AudioChannels = trackFromMkvInfo.AudioChannels;
                                    }

                                    if (trackFromMkvMerge.AudioSamplingFrequency < trackFromMkvInfo.AudioSamplingFrequency)
                                    {
                                        trackFromMkvMerge.AudioSamplingFrequency = trackFromMkvInfo.AudioSamplingFrequency;
                                    }

                                    if (!string.IsNullOrWhiteSpace(trackFromMkvInfo.ExtraInfo))
                                    {
                                        trackFromMkvMerge.ExtraInfo = trackFromMkvInfo.ExtraInfo;
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }

            // Try to determine the delays from mkvmerge info
            if (!gMerge.FindAndSetDelays(segmentListFromMkvMerge))
            {
                // If we couldn't determine the delays from mkvmerge info, then we use mkvinfo
                gInfo.FindAndSetDelays(segmentListFromMkvMerge, argInputFile);
            }

            // Translate codec_private_data in codec_private information
            gMerge.FindAndSetCodecPrivate(segmentListFromMkvMerge);

            return segmentListFromMkvMerge;
        }
    }
}
