using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HundredMilesSoftware.UltraID3Lib;
using System.Drawing;

namespace MeinBandCamp
{
    class Program
    {
        const string
            rgxIsArtistPath = "^http://[a-z0-9\\-]+?\\.bandcamp\\.com/?$",
            rgxIsAlbumPath = "^http://[a-z0-9\\-]+?\\.bandcamp\\.com/album/[a-z0-9\\-]+?/?$",
            rgxIsTrackPath = "^http://[a-z0-9\\-]+?\\.bandcamp\\.com/track/[a-z0-9\\-]+?/?$",
            rgxJsEscapedString = "\"([^\"]|(?<=\\\\)\")+?(?<!\\\\)\"",
            albumArtFilename = "Folder.jpg";
        static string
            downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeinBandCamp");
        static WebClient wc = new WebClient();

        static void Main(string[] args)
        {
            Console.BufferHeight = Int16.MaxValue - 1;
            List<string> urls = args.ToList();
            if (urls.Count == 0)
            {
                Console.Write("No URLs detected. Enter one here: ");
                urls.Add(Console.ReadLine());
            }
            if (!Directory.Exists(downloadsFolder))
                Directory.CreateDirectory(downloadsFolder);
            foreach (string path in urls)
            {
                string[] explode = path.Split(new char[] { '/', '.' });
                if (Regex.Match(path, rgxIsArtistPath).Success)
                    GetArtist(explode[2]);
                else if (Regex.Match(path, rgxIsAlbumPath).Success)
                    GetAlbum(explode[2], explode[6], -1);
                else if (Regex.Match(path, rgxIsTrackPath).Success)
                    GetTrack(explode[2], explode[6]);
                else
                    Console.WriteLine("Invalid bandcamp URL");
            }
            Console.WriteLine("All done! Press any key to finish.");
            Console.ReadKey(true);
        }
        static void GetArtist(string paramArtist)
        {
            string rgxAlbumAndArt = "\\<a href=\"/album/[a-z0-9\\-]+?\"\\>\\<img class=\"resizableArt\" src=\"http://bandcamp\\.com/files/\\d+?/\\d+?/\\d+?-\\d+?.jpg\"/\\>\\</a\\>";
            string pageUrl = "http://" + paramArtist + ".bandcamp.com/";
            string pageData = wc.DownloadString(pageUrl);
            MatchCollection mcAlbumAndArt = Regex.Matches(pageData, rgxAlbumAndArt);
            foreach (Match m in mcAlbumAndArt)
            {
                string[] explode = m.Value.Split('"');
                string paramAlbum = explode[1].Replace("/album/", "");
                GetAlbum(paramArtist, paramAlbum, -1);
            }
        }
        static void GetAlbum(string paramArtist, string paramAlbum, int track)
        {
            string
                rgxAlbumArt = "\\<img src=\"http://bandcamp\\.com/files/\\d+?/\\d+?/\\d+?-\\d+?.(jpg|png)\" alt=\".+?Cover Art\"/\\>",
                rgxAlbumTitle = "album_title : " + rgxJsEscapedString,
                rgxArtistName = "\"artist\":" + rgxJsEscapedString,
                rgxArtistName2 = "    name : " + rgxJsEscapedString,
                rgxStream = "\"http:\\\\/\\\\/[a-z0-9\\-]+?\\.bandcamp\\.com\\\\/download\\\\/track\\?enc=mp3-128&id=\\d+?&stream=\\d+?&ts=\\d+?\\.\\d+?&tsig=[0-9a-f]+?\"",
                rgxTrackTitle = "\"title\":" + rgxJsEscapedString;
            string pageUrl = "http://" + paramArtist + ".bandcamp.com/album/" + paramAlbum;
            string pageData = wc.DownloadString(pageUrl);
            Match mAlbumArt = Regex.Match(pageData, rgxAlbumArt);
            Match mAlbumTitle = Regex.Match(pageData, rgxAlbumTitle);
            MatchCollection mcStream = Regex.Matches(pageData, rgxStream);
            MatchCollection mcTrackTitle = Regex.Matches(pageData, rgxTrackTitle);
            string albumArtRemoteUrl = mAlbumArt.Value.Split('"')[1];
            string artistName = "";
            Match mArtistName = Regex.Match(pageData, rgxArtistName);
            if (mArtistName.Success == true)
                artistName = Regex.Unescape(mArtistName.Value.Substring(10, mArtistName.Value.Length - 11));
            else
            {
                Match mArtistName2 = Regex.Match(pageData, rgxArtistName2);
                artistName = Regex.Unescape(mArtistName2.Value.Substring(12, mArtistName2.Value.Length - 13));
            }
            string albumTitle = Regex.Unescape(mAlbumTitle.Value.Substring(15, mAlbumTitle.Value.Length - 16));
            List<Song> songs = new List<Song>();
            for (int i = 0; i < mcStream.Count; i++)
            {
                if (track == -1 || track == i + 1)
                {
                    Song song = new Song();
                    song.Album = albumTitle;
                    song.Artist = artistName;
                    song.RemoteUrl = Regex.Unescape(mcStream[i].Value.Substring(1, mcStream[i].Value.Length - 2));
                    song.Title = Regex.Unescape(mcTrackTitle[i + 1].Value.Substring(9, mcTrackTitle[i + 1].Value.Length - 10));
                    song.TrackNum = (short)(i + 1);
                    songs.Add(song);
                }
            }
            Console.WriteLine("Downloading " + albumTitle + " by " + artistName);
            int[] startPoint = new int[] { Console.CursorLeft, Console.CursorTop };
            Console.WriteLine(" " + albumArtFilename);
            foreach (Song song in songs)
                Console.WriteLine(" " + song.TrackNum.ToString("00") + " " + song.Title);
            Console.SetCursorPosition(startPoint[0], startPoint[1]);
            string albumArtLocalDir = Path.Combine(downloadsFolder, PathSanitise(artistName), PathSanitise(albumTitle));
            string albumArtLocalUrl = Path.Combine(albumArtLocalDir, albumArtFilename);
            Console.ForegroundColor = ConsoleColor.Green;
            if (!Directory.Exists(albumArtLocalDir))
                Directory.CreateDirectory(albumArtLocalDir);
            if (!File.Exists(albumArtLocalUrl))
            {
                try { wc.DownloadFile(albumArtRemoteUrl, albumArtLocalUrl); }
                catch { Console.ForegroundColor = ConsoleColor.Red; }
            }
            //IconizeAlbumArt(albumArtLocalDir);
            Console.WriteLine(" " + albumArtFilename);
            foreach (Song song in songs)
            {
                Console.ForegroundColor = song.Download() ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine(" " + song.TrackNum.ToString("00") + " " + song.Title);
            }
            Console.ForegroundColor = ConsoleColor.Gray;
        }
        static void GetTrack(string paramArtist, string paramTrack)
        {
            string
                rgxAlbumUrl = "album_url: " + rgxJsEscapedString,
                rgxTrackNum = "\"track_number\":\\d+?,";
            string pageUrl = "http://" + paramArtist + ".bandcamp.com/track/" + paramTrack;
            string pageData = wc.DownloadString(pageUrl);
            Match mAlbumUrl = Regex.Match(pageData, rgxAlbumUrl);
            Match mTrackNum = Regex.Match(pageData, rgxTrackNum);
            string albumUrl = Regex.Unescape(mAlbumUrl.Value.Substring(12, mAlbumUrl.Value.Length - 13));
            int trackNum = int.Parse(mTrackNum.Value.Substring(15, mTrackNum.Value.Length - 16));
            GetAlbum(paramArtist, albumUrl.Replace("/album/", ""), trackNum);
        }
        private class Song
        {
            private short _TrackNum = 1;
            private string _Title = "Unknown Title";
            private string _Artist = "Unknown Artist";
            private string _Album = "Unknown Album";
            private string _RemoteUrl;
            private string _LocalDir
            { get { return Path.Combine(downloadsFolder, PathSanitise(_Artist), PathSanitise(_Album)); } }
            private string _LocalUrl
            { get { return Path.Combine(_LocalDir, _TrackNum.ToString("00") + " " + PathSanitise(_Title) + ".mp3"); } }
            public short TrackNum
            {
                get { return _TrackNum; }
                set { _TrackNum = value; }
            }
            public string Title
            {
                get { return _Title; }
                set { _Title = value; }
            }
            public string Artist
            {
                get { return _Artist; }
                set { _Artist = value; }
            }
            public string Album
            {
                get { return _Album; }
                set { _Album = value; }
            }
            public string RemoteUrl
            {
                get { return _RemoteUrl; }
                set { _RemoteUrl = value; }
            }
            public string LocalDir
            { get { return _LocalDir; } }
            public string LocalUrl
            { get { return _LocalUrl; } }
            public bool Download()
            {
                bool success = false;
                UltraID3 u = new UltraID3();
                if (!Directory.Exists(_LocalDir))
                    Directory.CreateDirectory(_LocalDir);
                if (!File.Exists(_LocalUrl))
                {
                    try { wc.DownloadFile(_RemoteUrl, _LocalUrl); success = true; }
                    catch { Console.ForegroundColor = ConsoleColor.Red; }
                }
                else
                    success = true;
                u.Read(_LocalUrl);
                u.ID3v2Tag.Album = _Album;
                u.ID3v2Tag.Artist = _Artist;
                u.ID3v2Tag.Title = _Title;
                u.ID3v2Tag.TrackNum = _TrackNum;
                AddAlbumArt(Path.Combine(_LocalDir, albumArtFilename), u);
                u.Write();
                return success;
            }
        }
        static string PathSanitise(string s)
        {
            string comparison = "\\/:*?\"<>|";
            string output = "";
            for (int i = 0; i < s.Length; i++)
                if (!comparison.Contains(s[i].ToString()))
                    output += s[i];
            return output;
        }
        static void AddAlbumArt(string albumArtPath, UltraID3 u)
        {
            using (Image img = Image.FromFile(albumArtPath))
            {
                ID3FrameCollection myArtworkCollection = u.ID3v2Tag.Frames.GetFrames(MultipleInstanceID3v2FrameTypes.ID3v23Picture);
                ID3v23PictureFrame AlbumArt = new ID3v23PictureFrame(new Bitmap(img), PictureTypes.CoverFront, "", TextEncodingTypes.ISO88591);
                u.ID3v2Tag.Frames.Add(AlbumArt);
                u.Write();
            }
        }
        static void IconizeAlbumArt(string albumFolder)
        {
            string icoFileName = "Folder.ico";
            string iniFileName = "Desktop.ini";
            BitmapToIcon(Path.Combine(albumFolder, albumArtFilename), Path.Combine(albumFolder, icoFileName));
            using (StreamWriter sw = new StreamWriter(Path.Combine(albumFolder, iniFileName)))
                sw.WriteLine("[.ShellClassInfo]" + Environment.NewLine + "IconFile=" + icoFileName + Environment.NewLine + "IconIndex=0");
            File.SetAttributes(Path.Combine(albumFolder, iniFileName), FileAttributes.Hidden);
        }
        static void BitmapToIcon(string sourceFileName, string destFileName)
        {
            using (Bitmap bmp = new Bitmap(sourceFileName))
                bmp.Save(destFileName, System.Drawing.Imaging.ImageFormat.Icon);
        }
    }
}